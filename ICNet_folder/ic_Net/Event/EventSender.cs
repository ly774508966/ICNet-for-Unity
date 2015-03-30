using UnityEngine;
using System;
using System.Collections;

public class EventSender
{
    public delegate void PubEventHandler();  //定義委託
    private event PubEventHandler onPublish; //定義事件訪問器
 
    /// <summary>事件發佈</summary>
    public void DoSomething ()
    {
        PubEventHandler handler = onPublish; //防止多緒錯誤
        if (handler != null)
            handler();
    }

    /// <summary>訂閱事件</summary>
    /// <param name='in_onEvent'>觸發事件函式</param>
    public void Register (PubEventHandler in_onEvent)
    {
        onPublish += in_onEvent;
    }

    /// <summary>解除訂閱</summary>
    /// <param name='in_onEvent'>觸發事件函式</param>
    public void UnRegister (PubEventHandler in_onEvent)
    {
        onPublish -= in_onEvent;
    }
}