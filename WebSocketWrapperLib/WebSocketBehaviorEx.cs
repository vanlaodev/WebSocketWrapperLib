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
            RequestResponseBehaviorCoordinator.OnMessage(Context.WebSocket, e, OnMessage, s => this);
        }

        protected virtual void OnMessage(Message msg)
        {

        }
    }
}