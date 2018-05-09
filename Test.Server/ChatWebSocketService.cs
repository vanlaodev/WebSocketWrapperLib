using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Test.Common;
using WebSocketSharp;
using WebSocketWrapperLib;

namespace Test.Server
{
    public class ChatWebSocketService : WebSocketBehaviorEx, IChatServerContract
    {
        private UserInfo _userInfo;
        private IClientContract _clientContract;
        private static bool _pushingTime;
        private static Thread _pushTimeThread;

        private void StartPushTimeIfNeed()
        {
            if (_pushTimeThread != null) return;
            _pushingTime = true;
            _pushTimeThread = new Thread(() =>
            {
                while (_pushingTime)
                {
                    InternalPublish("Test", Encoding.UTF8.GetBytes(DateTime.Now.ToString()));
                    Thread.Sleep(1000);
                }
            });
            _pushTimeThread.Start();
        }

        private void StopPushTime()
        {
            if (_pushTimeThread == null) return;
            _pushingTime = false;
            _pushTimeThread.Join();
            _pushTimeThread = null;
        }

        protected override void OnOpen()
        {
            base.OnOpen();

            StartPushTimeIfNeed();

            Console.WriteLine("Client connected.");
            _clientContract = Context.WebSocket.GenerateContractWrapper<IClientContract>(2 * 60 * 1000);
            Task.Run(() =>
            {
                var clientTime = _clientContract.GetClientTime();
                Console.WriteLine("Client time: {0}", clientTime);
            });
        }

        protected override void OnClose(CloseEventArgs e)
        {
            Console.WriteLine("Client disconnected.");

            if (Sessions.Count == 0)
            {
                StopPushTime();
            }

            base.OnClose(e);
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

        public void Say(SayMultipleLinesModel model)
        {
            var formattedMsg = string.Join(Environment.NewLine,
                (from l in model.Lines
                 select string.Format("{0}: {1}", _userInfo == null ? "Anonymous" : _userInfo.Username, l)));
            Console.WriteLine(formattedMsg);
            BroadcastExceptsSelf(formattedMsg);
        }
    }
}