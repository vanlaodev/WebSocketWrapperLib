using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using WebSocketSharp;

namespace WebSocketWrapperLib
{
    public static class RequestResponseBehaviorCoordinator
    {
        private static readonly Dictionary<string, object> Locks = new Dictionary<string, object>();
        private static readonly Dictionary<string, Message> Responses = new Dictionary<string, Message>();

        internal static void OnResponse(Message message)
        {
            var msgReplyId = message.ReplyId;
            if (!string.IsNullOrEmpty(msgReplyId))
            {
                object l;
                lock (Locks)
                {
                    l = Locks.ContainsKey(msgReplyId) ? Locks[msgReplyId] : null;
                }
                if (l != null)
                {
                    lock (l)
                    {
                        lock (Responses)
                        {
                            Responses[msgReplyId] = message;
                        }
                        Monitor.Pulse(l);
                    }
                }
            }
        }

        public static T Request<T>(this WebSocket ws, Message req, int timeout) where T : Message
        {
            return Coordinate<T>(() => ws.Send(req.ToBytes()), req, timeout);
        }

        internal static T Coordinate<T>(Action send, Message req, int timeout) where T : Message
        {
            var msgId = req.Id;
            var l = new object();
            lock (Locks)
            {
                Locks[msgId] = l;
            }
            try
            {
                send();
                bool signaled;
                lock (l)
                {
                    lock (Responses)
                    {
                        if (Responses.ContainsKey(msgId))
                        {
                            return (T)Activator.CreateInstance(typeof(T), Responses[msgId]);
                        }
                    }
                    signaled = Monitor.Wait(l, timeout);
                }
                if (signaled)
                {
                    lock (Responses)
                    {
                        if (Responses.ContainsKey(msgId))
                        {
                            return (T)Activator.CreateInstance(typeof(T), Responses[msgId]);
                        }
                    }
                    throw new OperationCanceledException();
                }
                throw new TimeoutException();
            }
            finally
            {
                lock (Locks)
                {
                    Locks.Remove(msgId);
                }
                lock (Responses)
                {
                    Responses.Remove(msgId);
                }
            }
        }
    }
}