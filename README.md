# ICNet-for-Unity
IcNet for Untiy package 


# 檔案介紹

安裝 ICNet Unity 3D 封包檔
IcNet.unitypackage  

封包內容  與 ICNet-for-Unity / ICNet_folder 相同

ic_Net	
意門連線工具 -- 與意門SERVER 連線的工具。

MainSocket.cs
意門連線CLASS -- 簡化的意門SERVER 連線工具。

MainSocketExample.cs
範例程式 -- 使用 MainSocket 範例程式

SocketExample.unity
Unity 樣板介面 --  在Unity 宣告 GAMEOBJECT 掛上 MainSocketExample.cs 程式

使用後，可看到Socket_lobby 元件。
可在Socket_lobby UNITY 元件資訊上，設定相關資訊。

ServerName =  意門雲端上 RUNING 的SERVER名稱。

ServerPort =  SERVER 的PORT，在意門雲端上的 SETTING.JS 可找到。

ServerType = 選擇，開發伺服器，測試伺服器，正式伺服器。


# 使用說明 MainSocket.cs 

此為簡易版本。
需使用更多功能，要繼承 ICPacketLogic CLASS

宣告 socket 
------------------------------------------

MainSocket(string server_name)
serveName is 使用意門SERVER，開啟的SERVER。
預設: looby 。

MainSocketExample:
serveSocket = new MainSocket(_serveName);

初始化 socket 設定
-----------------------------------------

init(string gateway_IPport, string lobby_IPport)

gateway_IPport  is 使用意門SERVER，連線位置 + PORT 。
例如 : dev.imoncloud.com:30000 。

lobby_IPport is 使用意門LOBBY ，連線位置 + POR。
預設: dev.imoncloud.com:PORT 。

MainSocketExample:
serveSocket.init(_linkIPArr[_ctrLinkIPTypeIndexInt] + ":30000" , _linkIPArr[_ctrLinkIPTypeIndexInt] + ":" + _linkPort);

註冊事件 
------------------------------------------


連線成功  回傳事件
serveSocket._serverConnect.Register (handler_ConnectServer);

void handler_ConnectServer(){}

斷線  回傳事件
serveSocket._serverDisConnect.Register (handler_DisConnectServer);

void handler_DisConnectServer(){}

接收 封包 事件
serveSocket._severReceived.Register (handler_severReceived);

void handler_severReceived(PacketMessage i_msg){}

取消註冊事件 UnRegister
------------------------------------------

serveSocket._serverConnect.UnRegister(handler_ConnectServer) ;

serveSocket._serverDisConnect.UnRegister (handler_DisConnectServer);

serveSocket._severReceived.UnRegister (handler_severReceived);



發送 封包  方式   bool sendPacket(string packet_name, ref JObject para)
------------------------------------------

void test_sendTestToServer(){
		JObject para = new JObject(
			new JProperty("DataName", "DataVar")
			);
		serveSocket.sendToServer("TEST_EVENT", ref para);
}

# 接收封包 and 發送封包 相關  意門SERVER 

----------------------------
// 客戶端  CLINET SIDE   
----------------------------
void test_sendTestToServer(){
		JObject para = new JObject(
			new JProperty("DataName", "DataVar")
			);
		serveSocket.sendToServer("TEST_EVENT", ref para);
}
		serveSocket.sendToServer(處理封包名稱, 傳入的DATA JSON格式);

void handler_severReceived(PacketMessage i_msg){
		//	i_msg.packet_name			String
		//	i_msg.packet_data			String 

		//  i_msg.packet_name  = "TEST_EVENT_REPLY" 
		// i_msg.para  =	"{name: "abc" , age: 100 }"	
	
		//  i_msg.packet_name  = 回傳封包名稱  
		// i_msg.para  =	資料物件 JSON格式 ，純文字 STRING  "
		
		
}

----------------------------------
//伺服端   SERVER SIDE 
----------------------------------


l_handlers.TEST_EVENT = function (event) {
    
	// print some message
	LOG.debug('TEST_EVENT called');

	var Data_Name  = event.data.DataName ; // Data_Name = "DataVar"
	
	// send back response
	event.done('TEST_EVENT_REPLY', {name: "abc" , age: 100 }  );
}

l_handlers.處理封包名稱 = function (event) {

	event.data  = 傳入的DATA JSON格式

	event.done( 回傳封包名稱,資料物件 JSON格式 ，純文字 STRING  );

}

