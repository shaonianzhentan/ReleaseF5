try{
	
	var ws=new WebSocket("ws://127.0.0.1:23333");
	ws.onopen=function(e){
		console.log('连接成功！');			
	};
	ws.onmessage=function(e){
		if(e.data=="reload"){
			location.reload();
		}
		console.log(e.data);	
	};
	ws.onerror=function(e){	
		console.log(e.data);	
	};
	ws.onclose=function(e){
		console.log('连接关闭！');	
	};
	
}catch(ex){
	console.log(ex);
}