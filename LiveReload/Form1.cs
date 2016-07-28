using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;

namespace LiveReload
{
    public partial class Form1 : Form
    {
        TcpHelper helper = new TcpHelper();
        string configFile;

        bool IsReset = false;

        public Form1()
        {
            configFile = Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + "/LiveReload.cfg";
            InitializeComponent();
        }

        private void Form1_Load(object sender, System.EventArgs e)
        {
            CheckForIllegalCrossThreadCalls = false;
            if(File.Exists(configFile)) txtFolderName.Text = File.ReadAllText(configFile);

            this.FormClosed += (ss, ee) =>
            {
                if(IsReset == false) System.Environment.Exit(0);
            };
        }

        private void p_Changed(object sender, FileSystemEventArgs e)
        {
            string s = e.Name;

            AddMsg(s + "文件被更改于");            
            ReloadPage();
        }

        private void p_Created(object sender, FileSystemEventArgs e)
        {
            string s = e.Name;

            AddMsg(s + "文件被创建于");            
        }

        private void p_Deleted(object sender, FileSystemEventArgs e)
        {
            string s = e.Name;

            AddMsg(s + "文件被删除于");            
        }

        DateTime tmpTime = DateTime.Now.AddDays(-1);
        void ReloadPage()
        {
            if (tmpTime.ToString("yyyy-MM-dd HH:mm:ss") != DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
            {
                tmpTime = DateTime.Now;

                AddMsg("重新加载页的消息发送" + (helper.SendMsg("reload") ? "成功" : "失败") + "！");
            }
        }

        void AddMsg(string msg) {
            this.listBox1.Items.Add(DateTime.Now + " " + msg); ;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog dialog = new FolderBrowserDialog();            
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                txtFolderName.Text = dialog.SelectedPath;
            }
        }

        private void btnTing_Click(object sender, EventArgs e)
        {
            string folder=txtFolderName.Text;
            if (Directory.Exists(folder))
            {
                FileSystemWatcher p = new FileSystemWatcher(folder);
                p.EnableRaisingEvents = true;
                p.IncludeSubdirectories = true;
                p.Changed += new FileSystemEventHandler(p_Changed);
                p.Created += new FileSystemEventHandler(p_Created);
                p.Deleted += new FileSystemEventHandler(p_Deleted);

                AddMsg("开始监听目录【" + folder + "】");


                if (txtPort.Enabled)
                {
                    string port = txtPort.Text;
                    helper.Run(Convert.ToInt32(port));
                    AddMsg("打开" + port + "端口监听：");
                    txtPort.Enabled = false;
                }

                File.WriteAllText(configFile, folder);
            }
            else
            {
                MessageBox.Show("文件夹路径不正确！");
            }
        }

        private void btnReset_Click(object sender, EventArgs e)
        {
            IsReset = true;
            Application.Restart();
            System.Environment.Exit(0);
        }
    }
}
