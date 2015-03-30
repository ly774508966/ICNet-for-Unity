using UnityEngine;
using System.Collections;
using ImonCloud;   // 意門 連線工具 
using Newtonsoft.Json.Linq; // JSON 工具

public class MainSocketExample : MonoBehaviour {

	/// <summary>連線 SERVER Name  </summary>
	public string _serveName = "lobby";
	/// <summary>連線 SERVER Port </summary>
	public int _linkPort = 99999 ;

	/// <summary>控制 連線位置 0 = DEV  1 = TEST  2 = PROD </summary>
	private int _ctrLinkIPTypeIndexInt = 0  ;
	public EM_CONNECT_SERVER _ctrLinkIPtype;
	public enum EM_CONNECT_SERVER
	{
		DevServer = 0,
		TestServer = 1 ,
		ProdServer = 2
	}
	/// <summary>連線IP Array 0 = DEV  1 = TEST  2 = PROD </summary>
	public string[] _linkIPArr = new string[3]{
		"dev.imoncloud.com",
		"test.imoncloud.com",
		"prod.imoncloud.com"};

	/// <summary>封包</summary>
	static MainSocket _serveSocket = null;


	void Awake(){
		_ctrLinkIPTypeIndexInt = (int)_ctrLinkIPtype ;

		dimServerSocket ();
	}

	void Start ()
	{
		// test Sendmag To Server
		test_sendTestToServer();	
	}
	
	// Unity 停止 關閉伺服器
	void OnDestroy()
	{
		_serveSocket.shutdown();
	}


	void dimServerSocket(){
		_serveSocket = new MainSocket(_serveName);
		_serveSocket.init(_linkIPArr[_ctrLinkIPTypeIndexInt] + ":30000" ,
		                  _linkIPArr[_ctrLinkIPTypeIndexInt] + ":" + _linkPort);
		_serveSocket._serverConnect.Register (handler_ConnectServer);
		_serveSocket._serverDisConnect.Register (handler_DisConnectServer);
		_serveSocket._severReceived.Register (handler_severReceived);

		Debug.Log ("Link Server Name = " + _serveName);
		Debug.Log ("Link Server Port = " + _linkPort);
		Debug.Log ("Link Server Address = " + _linkIPArr[_ctrLinkIPTypeIndexInt]);
	}


	// _serveSocket Connect Handler function 
	void handler_ConnectServer(){		
		Debug.Log ("handler_ConnectServer");
	}
	// _serveSocket DisConnect Handler function 
	void handler_DisConnectServer(){		
		Debug.Log ("handler_DisConnectServer");
	}
	// _serveSocket eceived Handler function 
	void handler_severReceived(PacketMessage i_msg){

		//  String   =  i_msg.packet_name  , Server Return PacketName
		Debug.Log ("packet_name = "  + i_msg.packet_name);
		//  String   =  i_msg.para  ,  Json String Type
		Debug.Log ("packet_data = "  + i_msg.para);
		Debug.Log ("-----");

		DataPacket_Test l_data = i_msg.toObject<DataPacket_Test>();
		Debug.Log (l_data.age);
		//  DATA 格式 範例1  DataPacket_Test 物件 資料結構
		//		public class DataPacket_Test 
		//		{
		//			public int age;
		//			public int reset_counter;
		//			public int persist_counter;
		//		}

	}

	/// <summary>
	/// 測試  發送資料 TO SERVER
	/// </summary>
	void test_sendTestToServer(){
		
		JObject para = new JObject(
			new JProperty("DataName", "DataVar")
			);
		_serveSocket.sendToServer("TEST_EVENT", ref para);
	}
	
	
	/// <summary> DATA 格式 範例1  DataPacket_Test 物件 資料結構 </summary>
	public class DataPacket_Test 
	{
		public int age;
		public int reset_counter;
		public int persist_counter;

	}

}

