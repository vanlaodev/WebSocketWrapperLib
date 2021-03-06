﻿using System;
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
        private readonly RequestResponseBehaviorCoordinator _requestResponseBehaviorCoordinator;

        protected WebSocketBehaviorEx()
        {
            _requestResponseBehaviorCoordinator = new RequestResponseBehaviorCoordinator();
        }

        public RequestResponseBehaviorCoordinator RequestResponseBehaviorCoordinator
        {
            get { return _requestResponseBehaviorCoordinator; }
        }

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
            _requestResponseBehaviorCoordinator.OnMessage(Context.WebSocket, e, OnMessage, s => this, OnHandleMessageError);
        }

        protected virtual void OnHandleMessageError(Exception obj)
        {

        }

        protected virtual void OnMessage(Message msg)
        {

        }

        protected override void OnClose(CloseEventArgs e)
        {
            UnsubscribeAll();

            base.OnClose(e);

            _requestResponseBehaviorCoordinator.CancelAll();
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

        protected virtual void InternalPublish(string topic, byte[] data)
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
                    Log.Error(string.Format("Internal publish failed for session '{0}': {1}", session.ID, ex.Message));
                }
            }
        }
    }
}