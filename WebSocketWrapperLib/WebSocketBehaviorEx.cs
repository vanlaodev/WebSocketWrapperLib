using System;
using System.Threading.Tasks;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace WebSocketWrapperLib
{
    public abstract class WebSocketBehaviorEx : WebSocketBehavior
    {
        protected override void OnMessage(MessageEventArgs e)
        {
            Coordinator.OnMessage(Context.WebSocket, e, OnMessage);
        }

        protected virtual void OnMessage(Message msg)
        {

        }
    }
}