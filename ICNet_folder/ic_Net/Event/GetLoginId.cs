using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;
using System.Collections;

/// <summary>取得LoginId</summary>
public class GetLoginId : MonoBehaviour {
	/// <summary>發佈LoginId</summary>
 	public EventSenderT<string> evtLoginId = new EventSenderT<string>();
	/// <summary>網址</summary>
   	public string fullUrl;
	/// <summary>解析資訊</summary>
    Dictionary<string, string> dict = new Dictionary<string, string>();
	// Use this for initialization
	void Awake()
	{
	}
	void Start()
	{
		Application.ExternalEval("var unity = unityObject.getObjectById(\"unityPlayer\");unity.SendMessage(\"" + name + "\", \"ReceiveURL\", document.URL);");
	}
	// Update is called once per frame
	//void FixedUpdate ()
	//{
	//	if(dict["login_id"] != null)
	//	{
	//		Debug.Log("GetLoginId::FixedUpdate() login_id = " + dict["login_id"]);
	//		eveSender.DoSomething( dict["login_id"] );
	//	}
	//}
	
	/// <summary>JavaScrip外部呼叫函式</summary>
	/// <param name='url'>URL資料</param>
    public void ReceiveURL(string url)
	{
		// this will include the full URL, including url parameters etc.
        fullUrl = url;
        Uri tmp = new Uri(url);
        string query = tmp.Query;
        dict = query.Split('?')[1].Split('&').Select(x => x.Split('=')).ToDictionary(y => y[0], y => y[1]);
		evtLoginId.DoSomething( dict["login_id"] );
	}
	/// <summary>將Url資料全部輸出</summary>
	public void passAll()
	{
		fullUrl = "";
        foreach (System.Collections.Generic.KeyValuePair<string, string> pair in dict) {
            fullUrl += "key: " + pair.Key + " value: " + pair.Value;
		}
	}
}