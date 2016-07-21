using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Text;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace LiveReload
{
	public class SocketHelper
	{
		private Dictionary<Socket, byte[]> clientPool = new Dictionary<Socket, byte[]> ();
		private List<string> message = new List<string> ();
		private Boolean isClear = true;

		public int Port { get; set; }

		public void Start ()
		{
			Boardcast ();
			var socketThread = new Thread (() => {
				Socket socket = new Socket (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
				IPEndPoint iep = new IPEndPoint (IPAddress.Any, this.Port);  
				socket.Bind (iep);
				socket.Listen (5);
				socket.BeginAccept (new AsyncCallback (Accept), socket);
			});
			socketThread.Start ();
			Console.WriteLine ("Server Started");
		}

		private void Accept (IAsyncResult ia)
		{
			Socket socket = ia.AsyncState as Socket;
			var client = socket.EndAccept (ia);

			socket.BeginAccept (new AsyncCallback (Accept), socket);

			byte[] buf = new byte[1024];
			this.clientPool.Add (client, buf);

			try {
				client.BeginReceive (buf, 0, buf.Length, SocketFlags.None, new AsyncCallback (Receive), client);
				string sessionId = client.RemoteEndPoint.ToString () + " - " + client.Handle.ToString ();
				Console.WriteLine ("Client ({0}) Connected", sessionId);
			} catch (Exception ex) {
				Console.WriteLine ("Error：\r\n" + ex.ToString ());
			}
		}

		private void Receive (IAsyncResult ia)
		{
			var client = ia.AsyncState as Socket;

			if (client == null || !this.clientPool.ContainsKey (client)) {
				return;
			}
			int length = client.EndReceive (ia);

			byte[] buf = this.clientPool [client];

			if (length > 0) {
				try {
					client.BeginReceive (buf, 0, buf.Length, SocketFlags.None, new AsyncCallback (Receive), client);
					string context = Encoding.UTF8.GetString (buf, 0, length);
					if (context.Contains ("Sec-WebSocket-Key")) {
						client.Send (PackHandShakeData (GetSecKeyAccetp (buf, length)));
					} else {
						string sessionId = client.RemoteEndPoint.ToString () + " - " + client.Handle.ToString ();
						PushMessage (string.Format ("{0} {1}:<br/>&nbsp;&nbsp;{2}", sessionId, DateTime.Now.ToShortTimeString (), AnalyticData (buf, length)));
					}
				} catch (Exception ex) {
					Console.WriteLine ("Receive Error :{0}", ex.ToString ());
				}
			} else {
				try {
					string sessionId = client.RemoteEndPoint.ToString () + " - " + client.Handle.ToString ();
					client.Disconnect (true);
					this.clientPool.Remove (client);
					Console.WriteLine ("Client ({0}) Disconnet", sessionId);
				} catch (Exception ex) {
					Console.WriteLine ("Error: " + ex.ToString ());
				}
			}
		}

		private void PushMessage (string context)
		{
			Console.WriteLine ("Get : {0}", context);
			message.Add (context);
			isClear = false;
		}

		private void Boardcast ()
		{
			Thread boardcaseThread = new Thread (() => {
				while (true) {
					if (!isClear) {
						byte[] tmp = PackData (message [0]);
						foreach (KeyValuePair<Socket, byte[]> node in this.clientPool) {
							Socket client = node.Key;
							client.Send (tmp, tmp.Length, SocketFlags.None);
						}
						message.RemoveAt (0);
						isClear = message.Count > 0 ? false : true;
					}
				}
			});

			boardcaseThread.Start ();
		}

		private static byte[] PackHandShakeData (string secKeyAccept)
		{
			var responseBuilder = new StringBuilder ();
			responseBuilder.Append ("HTTP/1.1 101 Switching Protocols" + "\r\n");
			responseBuilder.Append ("Upgrade: websocket" + "\r\n");
			responseBuilder.Append ("Connection: Upgrade" + "\r\n");
			responseBuilder.Append ("Sec-WebSocket-Accept: " + secKeyAccept + "\r\n\r\n");

			return Encoding.UTF8.GetBytes (responseBuilder.ToString ());
		}

		private static string GetSecKeyAccetp (byte[] handShakeBytes, int bytesLength)
		{
			string handShakeText = Encoding.UTF8.GetString (handShakeBytes, 0, bytesLength);
			string key = string.Empty;
			Regex r = new Regex (@"Sec\-WebSocket\-Key:(.*?)\r\n");
			Match m = r.Match (handShakeText);
			if (m.Groups.Count != 0) {
				key = Regex.Replace (m.Value, @"Sec\-WebSocket\-Key:(.*?)\r\n", "$1").Trim ();
			}
			byte[] encryptionString = SHA1.Create ().ComputeHash (Encoding.ASCII.GetBytes (key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"));
			return Convert.ToBase64String (encryptionString);
		}

		private static string AnalyticData (byte[] recBytes, int recByteLength)
		{
			if (recByteLength < 2) {
				return string.Empty;
			}

			bool fin = (recBytes [0] & 0x80) == 0x80; // 1bit，1表示最后一帧  
			if (!fin) {
				return string.Empty;// 超过一帧暂不处理 
			}

			bool mask_flag = (recBytes [1] & 0x80) == 0x80; // 是否包含掩码  
			if (!mask_flag) {
				return string.Empty;// 不包含掩码的暂不处理
			}

			int payload_len = recBytes [1] & 0x7F; // 数据长度  

			byte[] masks = new byte[4];
			byte[] payload_data;

			if (payload_len == 126) {
				Array.Copy (recBytes, 4, masks, 0, 4);
				payload_len = (UInt16)(recBytes [2] << 8 | recBytes [3]);
				payload_data = new byte[payload_len];
				Array.Copy (recBytes, 8, payload_data, 0, payload_len);

			} else if (payload_len == 127) {
				Array.Copy (recBytes, 10, masks, 0, 4);
				byte[] uInt64Bytes = new byte[8];
				for (int i = 0; i < 8; i++) {
					uInt64Bytes [i] = recBytes [9 - i];
				}
				UInt64 len = BitConverter.ToUInt64 (uInt64Bytes, 0);

				payload_data = new byte[len];
				for (UInt64 i = 0; i < len; i++) {
					payload_data [i] = recBytes [i + 14];
				}
			} else {
				Array.Copy (recBytes, 2, masks, 0, 4);
				payload_data = new byte[payload_len];
				Array.Copy (recBytes, 6, payload_data, 0, payload_len);

			}

			for (var i = 0; i < payload_len; i++) {
				payload_data [i] = (byte)(payload_data [i] ^ masks [i % 4]);
			}

			return Encoding.UTF8.GetString (payload_data);
		}

		private static byte[] PackData (string message)
		{
			byte[] contentBytes = null;
			byte[] temp = Encoding.UTF8.GetBytes (message);

			if (temp.Length < 126) {
				contentBytes = new byte[temp.Length + 2];
				contentBytes [0] = 0x81;
				contentBytes [1] = (byte)temp.Length;
				Array.Copy (temp, 0, contentBytes, 2, temp.Length);
			} else if (temp.Length < 0xFFFF) {
				contentBytes = new byte[temp.Length + 4];
				contentBytes [0] = 0x81;
				contentBytes [1] = 126;
				contentBytes [2] = (byte)(temp.Length & 0xFF);
				contentBytes [3] = (byte)(temp.Length >> 8 & 0xFF);
				Array.Copy (temp, 0, contentBytes, 4, temp.Length);
			} else {
				// 暂不处理超长内容  
			}

			return contentBytes;
		}
	}
}

