using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace WebSocketWrapperLib
{
    public abstract class WebSocketBehaviorEx : WebSocketBehavior, IPubSubContract
    {
        private static readonly Dictionary<string, List<string>> SubTopicsRegistry = new Dictionary<string, List<string>>();

        protected override void OnMessage(MessageEventArgs e)
        {
            RequestResponseBehaviorCoordinator.OnMessage(Context.WebSocket, e, OnMessage, s => this);
        }

        protected virtual void OnMessage(Message msg)
        {

        }

        protected override void OnOpen()
        {
            base.OnOpen();

            lock (SubTopicsRegistry)
            {
                SubTopicsRegistry.Add(ID, new List<string>());
            }
        }

        protected override void OnClose(CloseEventArgs e)
        {
            lock (SubTopicsRegistry)
            {
                SubTopicsRegistry.Remove(ID);
            }

            base.OnClose(e);
        }

        public void Subscribe(string[] topics)
        {
            lock (SubTopicsRegistry)
            {
                var pair = SubTopicsRegistry.SingleOrDefault(x => x.Key.Equals(ID));
                if (pair.Equals(default(KeyValuePair<string, List<string>>)))
                {
                    SubTopicsRegistry.Add(ID, new List<string>(topics));
                }
                else
                {
                    foreach (var t in topics)
                    {
                        if (!pair.Value.Any(x => x.Equals(t)))
                        {
                            pair.Value.Add(t);
                        }
                    }
                }
            }
        }

        public void Unsubscribe(string[] topics)
        {
            lock (SubTopicsRegistry)
            {
                var pair = SubTopicsRegistry.SingleOrDefault(x => x.Key.Equals(ID));
                if (!pair.Equals(default(KeyValuePair<string, List<string>>)))
                {
                    foreach (var t in topics)
                    {
                        if (pair.Value.Any(x => x.Equals(t)))
                        {
                            pair.Value.Remove(t);
                        }
                    }
                }
            }
        }

        public void UnsubscribeAll()
        {
            lock (SubTopicsRegistry)
            {
                var pair = SubTopicsRegistry.SingleOrDefault(x => x.Key.Equals(ID));
                if (!pair.Equals(default(KeyValuePair<string, List<string>>)))
                {
                    SubTopicsRegistry.Remove(ID);
                }
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
            IEnumerable<string> subSessionIds = null;
            lock (SubTopicsRegistry)
            {
                subSessionIds =
                    SubTopicsRegistry.Where(x => x.Value.Any(t => t.Equals(topic)))
                        .Select(x => x.Key)
                        .ToList();
            }
            if (subSessionIds != null && subSessionIds.Any())
            {
                var sessions = Sessions.Sessions.Where(x => subSessionIds.Any(i => i.Equals(x.ID))).ToList();
                foreach (var session in sessions)
                {
                    try
                    {
                        session.Context.WebSocket.Send(new PublishMessage(topic, data).ToBytes());
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }
        }
    }
}