using UnityEngine;
using System;
using System.Collections;
using ImonCloud;
using Newtonsoft.Json.Linq;

/*
 * 這邊要設計一個事件封包處理
 * 能不能夠自動化
 * 當新產生一個項目時 就自動增加該項目資料解析 與 事件觸發
 * 序列化處理封包事件
 * 動作處理:
 * 	1.將接收資料解析之後存放到資料結構之中.
 * 	2.產生事件發佈者
 * 
 * 外部使用:
 * 	傳入使用函式, 注冊此事件觸發
 * 	傳入使用函式, 取消注冊此事件
 * */
/// <summary>測試回覆</summary>
public class Packet
{
	public virtual void showLog() {
		Debug.Log("Data = {}");
	}
}
/// <summary>錯誤訊息</summary>
public class Packet_Message : Packet
{
	/// <summary>訊息內容</summary>
    public string strText = "";
}
/// <summary>一般訊息</summary>
public class Packet_Text : Packet_Message {}
/// <summary>警告訊息</summary>
public class Packet_Warning : Packet_Message {}
/// <summary>錯誤訊息</summary>
public class Packet_Error : Packet_Message {}

/// <summary>交換錢幣</summary>
public class Packet_ChangeMoney : Packet
{
	/// <summary>大廳金額</summary>
	public int iLobby;
	/// <summary>遊戲金額</summary>
	public int iGame;
}
/// <summary>接收封包事件處理</summary>
public class RecvEventT<T> : EventSenderT<T>
{
	/// <summary>發布訊息</summary>
	/// <param name='i_msg'>接收的訊息</param>
	public void publishData(PacketMessage i_msg)
	{
		Debug.Log("RecvEvent<T>::publishData packet_name = " + i_msg.packet_name
			+ " para = " + i_msg.para.ToString());
		T l_data = i_msg.toObject<T>();
		//顯示LOG
		//Packet l_packet = l_data as Packet;
		//if(l_pocket != null)
		//	l_pocket.showLog();
		//else
		//	Debug.Log("l_pocket == null");
		this.DoSomething(l_data);
	}
}
