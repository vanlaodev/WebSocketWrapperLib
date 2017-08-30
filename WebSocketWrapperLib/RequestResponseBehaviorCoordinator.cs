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

        public static int RequestTimeout = 30 * 1000;

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

        public static T Request<T>(this WebSocket ws, Message req) where T : Message
        {
            return Request<T>(ws, req, RequestTimeout);
        }

        public static T Request<T>(this WebSocket ws, Message req, int timeout) where T : Message
        {
            return Coordinate<T>(() => ws.Send(req.ToBytes()), req, timeout);
        }

        internal static T Coordinate<T>(Action send, Message req) where T : Message
        {
            return Coordinate<T>(send, req, RequestTimeout);
        }

        internal static T Coordinate<T>(Action send, Message req, int timeout) where T : Message
        {
            req.RequireReply = true;
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
                            return HandleResponse<T>(msgId);
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
                            return HandleResponse<T>(msgId);
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

        private static T HandleResponse<T>(string msgId) where T : Message
        {
            var resp = Responses[msgId];
            if (resp.Type.Equals(ErrorMessage.MsgType))
            {
                var errMsg = new ErrorMessage(resp);
                throw new RemoteOperationException(errMsg.Error.Message);
            }
            return (T)Activator.CreateInstance(typeof(T), resp);
        }
    }
}