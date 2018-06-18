using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using WebSocketSharp;

namespace WebSocketWrapperLib
{
    public class RequestResponseBehaviorCoordinator
    {
        private readonly Dictionary<string, object> _locks = new Dictionary<string, object>();
        private readonly Dictionary<string, Message> _responses = new Dictionary<string, Message>();

        internal void CancelAll()
        {
            IEnumerable<object> locks;
            lock (_locks)
            {
                locks = _locks.Values;
            }
            foreach (var l in locks)
            {
                lock (l)
                {
                    Monitor.Pulse(l);
                }
            }
        }

        internal void OnResponse(Message message)
        {
            var msgReplyId = message.ReplyId;
            if (!string.IsNullOrEmpty(msgReplyId))
            {
                object l;
                lock (_locks)
                {
                    l = _locks.ContainsKey(msgReplyId) ? _locks[msgReplyId] : null;
                }
                if (l != null)
                {
                    lock (l)
                    {
                        lock (_responses)
                        {
                            _responses[msgReplyId] = message;
                        }
                        Monitor.Pulse(l);
                    }
                }
            }
        }

        internal bool OnMessage(WebSocket ws, MessageEventArgs e, Action<Message> messageReceived,
            Func<string, object> contractFinder, Action<Exception> onError)
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
                            if (onError != null)
                            {
                                try
                                {
                                    onError(ex);
                                }
                                catch
                                {
                                    // ignored
                                }
                            }

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

        private void HandleRpcRequest(WebSocket ws, Message msg, Func<string, object> rpcTarget)
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

/*        public T Request<T>(this WebSocket ws, Message req, int timeout) where T : Message
        {
            return Coordinate<T>(() => ws.Send(req.ToBytes()), req, timeout);
        }*/

        public T Coordinate<T>(Action send, Message req, int timeout) where T : Message
        {
            req.RequireReply = true;
            var msgId = req.Id;
            var l = new object();
            lock (_locks)
            {
                _locks[msgId] = l;
            }
            try
            {
                send();
                bool signaled;
                lock (l)
                {
                    lock (_responses)
                    {
                        if (_responses.ContainsKey(msgId))
                        {
                            return HandleResponse<T>(msgId);
                        }
                    }
                    signaled = Monitor.Wait(l, timeout);
                }
                if (signaled)
                {
                    lock (_responses)
                    {
                        if (_responses.ContainsKey(msgId))
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
                lock (_locks)
                {
                    _locks.Remove(msgId);
                }
                lock (_responses)
                {
                    _responses.Remove(msgId);
                }
            }
        }

        private T HandleResponse<T>(string msgId) where T : Message
        {
            var resp = _responses[msgId];
            if (resp.Type.Equals(ErrorMessage.MsgType))
            {
                var errMsg = new ErrorMessage(resp);
                throw new RemoteOperationException(errMsg.Error.Message);
            }
            return (T)Activator.CreateInstance(typeof(T), resp);
        }
    }
}