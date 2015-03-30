/*
 * 
 * MainSocket nameSocet = new MainSocket( serverName  ) ;
 *	nameSocet._serverConnect.Register (function ());
 *	nameSocet._serverDisConnect.Register (function ());
 *	nameSocet._severReceived.Register (function (PacketMessage data));
 *
 * 
 * 
 **/





using UnityEngine;
using System.Collections;
using ImonCloud;   // 意門 連線工具 
using Newtonsoft.Json.Linq; // JSON 工具

public class MainSocket : ICPacketLogic
{
	/// <summary>  
	/// 啟動 SERVER 事件  使用 ._serverConnect.Register( funcName ); 註冊
	/// 啟動 SERVER 事件  使用 ._serverConnect.UnRegister( funcName ); 取消 
	/// void funcName (){}
	/// </summary>
	public EventSender _serverConnect = new EventSender();
	public EventSender _serverDisConnect = new EventSender();

	/// <summary>
	///  收取封包事件  使用 ._severReceived.Register( funcName ); 註冊
	///   收取封包事件  使用 ._severReceived.UnRegister( funcName ); 取消
	/// void funcName ( PacketMessage i_data) {}
	/// </summary>
	public EventSenderT<PacketMessage> _severReceived = new EventSenderT<PacketMessage>();


	/// <summary> 
	/// 系統 TIMER  需特別注意 Unity 關閉後他還是會繼續執行  
	/// HINT: Unity StopRun , Timer always is runing statu ;
	/// </summary>
	System.Timers.Timer _timeTimer = null;
	int _timeTimerInterval = 100;
	/// <summary> 待機 時間  -- 變數    超過 N 秒 自動斷線 </summary>
	int _timeDeadlineSecIntVar = 0 ;
	/// <summary> 待機 時間  -- 最大值  超過 N 秒 自動斷線 </summary>
	int _timeDeadlineSecIntVarMax = 0 ;
	int _timeDeadlineSecIntMax = 180 ;



	/// <summary>建構式</summary>
	/// <param name='server_name'>Server名稱</param>
	public MainSocket(string server_name) : base(server_name)
	{
		_timeDeadlineSecIntVarMax = _timeDeadlineSecIntMax *1000/ _timeTimerInterval ;

		dimTimer ();
		startTimer ();
	}
	/// <summary>Server連線</summary>
	/// <param name='server_name'>Server名稱</param>
	protected override void serverConnectedHandler(string server_name)
	{
		_serverConnect.DoSomething();

	}
	/// <summary>Server斷線</summary>
	/// <param name='server_name'>Server名稱</param>
	protected override void serverDisconnectedHandler(string server_name)
	{
		_serverDisConnect.DoSomething();
		stopTimer ();
	}
	// handler to notify incoming packets 
	protected override void packetReceivedHandler(ref PacketMessage msg)
	{
		_severReceived.DoSomething (msg);
	}

	void dimTimer()
	{
		_timeTimer = new System.Timers.Timer();
		_timeTimer.Enabled = false;		
		_timeTimer.Interval = _timeTimerInterval;		
		_timeTimer.Elapsed += new System.Timers.ElapsedEventHandler(timersTimerHandlerUpateTick);
	}

	void startTimer()
	{
		_timeTimer.Enabled = true;	
	}
	void stopTimer()
	{
		_timeTimer.Enabled = false;		
		_timeTimer.Dispose ();

	}

	/// <summary>自動刷新  伺服器狀態 </summary>
	void timersTimerHandlerUpateTick(object sender, System.Timers.ElapsedEventArgs e)
	{
		tick();
		_timeDeadlineSecIntVar++;
		if (_timeDeadlineSecIntVar == _timeDeadlineSecIntVarMax) {
			shutdown ();
			stopTimer ();
		}

	}


	/// <summary>
	/// Sends the packet.
	/// </summary>
	/// <returns><c>true</c>, if packet was sent, <c>false</c> otherwise.</returns>
	/// <param name="i_packet_name"> HandlerPool.{i_packet_name}</param>
	/// <param name="i_para">JObject Type</param>
	public bool sendToServer(string i_packet_name , ref JObject i_para){
		return sendPacket( i_packet_name , ref i_para);
	}
}
