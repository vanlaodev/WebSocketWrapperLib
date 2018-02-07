using System;
using System.Linq;
using Test.Common;
using WebSocketSharp;
using WebSocketWrapperLib;

namespace Test.Server
{
    public class ChatWebSocketService : WebSocketBehaviorEx, IChatServerContract
    {
        private UserInfo _userInfo;

        protected override void OnOpen()
        {
            Console.WriteLine("Client connected.");
        }

        protected override void OnClose(CloseEventArgs e)
        {
            Console.WriteLine("Client disconnected.");
        }

        private void BroadcastExceptsSelf(string text)
        {
            var sessions = Sessions.Sessions.Where(x => !x.ID.Equals(ID));
            foreach (var session in sessions)
            {
                session.Context.WebSocket.Send(new TextMessage()
                {
                    Text = text
                }.ToBytes());
            }
        }

        public void Say(string text)
        {
            var formattedMsg = string.Format("{0}: {1}", _userInfo == null ? "Anonymous" : _userInfo.Username, text);
            Console.WriteLine(formattedMsg);
            BroadcastExceptsSelf(formattedMsg);
        }

        public void SetUserInfo(UserInfo userInfo)
        {
            _userInfo = userInfo;
            Console.WriteLine("User info set.");
        }

        public ServerInfo GetServerInfo()
        {
            return new ServerInfo();
        }

        public int Add(int i1, int i2)
        {
            return i1 + i2;
        }

        public int Add(int i1, int i2, int i3)
        {
            return i1 + i2 + i3;
        }
    }
}