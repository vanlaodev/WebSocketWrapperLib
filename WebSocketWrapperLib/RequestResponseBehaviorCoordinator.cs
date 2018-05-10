using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using WebSocketSharp;

namespace WebSocketWrapperLib
{
    internal static class RequestResponseBehaviorCoordinator
    {
        private static readonly Dictionary<string, object> Locks = new Dictionary<string, object>();
        private static readonly Dictionary<string, Message> Responses = new Dictionary<string, Message>();

        internal static void CancelAll()
        {
            IEnumerable<object> locks;
            lock (Locks)
            {
                locks = Locks.Values;
            }
            foreach (var l in locks)
            {
                lock (l)
                {
                    Monitor.Pulse(l);
                }
            }
        }

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

        internal static bool OnMessage(WebSocket ws, MessageEventArgs e, Action<Message> messageReceived,
            Func<string, object> contractFinder)
        {
            if (e.IsBinary)
            {
                Message msg = null;
                try
                {
                    msg = Message.Parse(e.RawData);
                }
                catch
                {
                    // ignored
                }
                if (msg != null)
                {
                    if (string.IsNullOrEmpty(msg.ReplyId))
                    {
                        /*                        Task.Run(() =>
                                                {*/
                        try
                        {
                            if (msg.Type.Equals(RpcRequestMessage.MsgType))
                            {
                                HandleRpcRequest(ws, msg, contractFinder);
                            }
                            else
                            {
                                if (messageReceived != null)
                                {
                                    messageReceived(msg);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            if (msg.RequireReply)
                            {
                                ws.Send(new ErrorMessage(msg.Id)
                                {
                                    Error = new ErrorMessage.ErrorInfo()
                                    {
                                        Message = ex.GetInnermostException().Message
                                    }
                                }.ToBytes());
                            }
                            else
                            {
                                throw;
                            }
                        }
                        //                        });
                    }
                    else
                    {
                        OnResponse(msg);
                    }
                    return true;
                }
            }
            return false;
        }

        private static void HandleRpcRequest(WebSocket ws, Message msg, Func<string, object> rpcTarget)
        {
            var cachedTypes = new Dictionary<string, Type>();
            var rpcRequestMsg = new RpcRequestMessage(msg);
            var req = rpcRequestMsg.Request;
            var contractImpl = rpcTarget(req.Contract);
            var contractImplType = contractImpl.GetType();
            var methodDef = contractImplType.GetMethod(req.Method, req.Parameters.Select(p =>
            {
                if (!cachedTypes.ContainsKey(p.Type))
                {
                    cachedTypes[p.Type] = Type.GetType(p.Type);
                }
                return cachedTypes[p.Type];
            }).ToArray());
            var methodReturnType = methodDef.ReturnType;
            var parameters = req.Parameters.Select(p =>
            {
                if (!cachedTypes.ContainsKey(p.Type))
                {
                    cachedTypes[p.Type] = Type.GetType(p.Type);
                }
                var type = cachedTypes[p.Type];
                if (type.IsValueType) return Convert.ChangeType(p.Value, type);
                return WebSocketWrapper.ObjectSerializer.Deserialize((string)p.Value, type);
            }).ToArray();
            var result = contractImplType
                .InvokeMember(req.Method, BindingFlags.InvokeMethod, null, contractImpl, parameters);
            ws.Send(new RpcResponseMessage(msg.Id)
            {
                Response = new RpcResponseMessage.RpcResponse()
                {
                    Value = methodReturnType.IsValueType ? result : WebSocketWrapper.ObjectSerializer.Serialize(result),
                    Type = methodReturnType.AssemblyQualifiedName
                }
            }.ToBytes());
        }

        public static T Request<T>(this WebSocket ws, Message req, int timeout) where T : Message
        {
            return Coordinate<T>(() => ws.Send(req.ToBytes()), req, timeout);
        }

        private static T Coordinate<T>(Action send, Message req, int timeout) where T : Message
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