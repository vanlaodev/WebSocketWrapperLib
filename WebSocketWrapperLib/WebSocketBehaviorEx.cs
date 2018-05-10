using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace WebSocketWrapperLib
{
    public abstract class WebSocketBehaviorEx : WebSocketBehavior, IPubSubContract
    {
        private readonly List<string> _subscribedTopics = new List<string>();

        public IReadOnlyList<string> SubscribedTopics
        {
            get
            {
                lock (_subscribedTopics)
                {
                    return new ReadOnlyCollection<string>(_subscribedTopics);
                }
            }
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            RequestResponseBehaviorCoordinator.OnMessage(Context.WebSocket, e, OnMessage, s => this);
        }

        protected virtual void OnMessage(Message msg)
        {

        }

        protected override void OnClose(CloseEventArgs e)
        {
            UnsubscribeAll();

            base.OnClose(e);

            RequestResponseBehaviorCoordinator.CancelAll();
        }

        public void Subscribe(string[] topics)
        {
            lock (_subscribedTopics)
            {
                foreach (var t in topics)
                {
                    if (!_subscribedTopics.Any(x => x.Equals(t)))
                    {
                        _subscribedTopics.Add(t);
                    }
                }
            }
        }

        public void Unsubscribe(string[] topics)
        {
            lock (_subscribedTopics)
            {
                foreach (var t in topics)
                {
                    if (_subscribedTopics.Any(x => x.Equals(t)))
                    {
                        _subscribedTopics.Remove(t);
                    }
                }
            }
        }

        public void UnsubscribeAll()
        {
            lock (_subscribedTopics)
            {
                _subscribedTopics.Clear();
            }
        }

        public void Publish(string topic, byte[] data)
        {
            Task.Run(() =>
            {
                InternalPublish(topic, data);
            });
        }

        protected void InternalPublish(string topic, byte[] data)
        {
            var sessions =
                Sessions.Sessions.Cast<WebSocketBehaviorEx>()
                    .Where(x => x.SubscribedTopics.Any(y => y.Equals(topic)))
                    .ToList();
            foreach (var session in sessions)
            {
                try
                {
                    session.Context.WebSocket.Send(new PublishMessage(topic, data).ToBytes());
                }
                catch (Exception ex)
                {
                    Log.Error(string.Format("Internal publish failed: " + ex.Message));
                }
            }
        }
    }
}