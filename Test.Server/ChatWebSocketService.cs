using System;
using System.Linq;
using Test.Common;
using WebSocketSharp;
using WebSocketWrapperLib;

namespace Test.Server
{
    public class ChatWebSocketService : WebSocketBehaviorEx
    {
        protected override void OnOpen()
        {
            Console.WriteLine("Client connected.");
        }

        protected override void OnClose(CloseEventArgs e)
        {
            Console.WriteLine("Client disconnected.");
        }

        protected override void OnMessage(Message msg)
        {
            if (msg.Type.Equals(TextMessage.MsgType))
            {
                var textMsg = new TextMessage(msg);
                Send(new AckMessage(textMsg.Id).ToBytes());
                var sessions = Sessions.Sessions.Where(x => !x.ID.Equals(ID));
                foreach (var session in sessions)
                {
                    session.Context.WebSocket.Send(new TextMessage()
                    {
                        Text = textMsg.Text
                    }.ToBytes());
                }
            }
        }
    }
}