using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using WebSocketSharp;

namespace WebSocketWrapperLib
{
    public static class Coordinator
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

        internal static bool OnMessage(WebSocket ws, MessageEventArgs e, Action<Message> messageReceived)
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
                        Task.Run(() =>
                        {
                            try
                            {
                                if (msg.Type.Equals(RpcRequestMessage.MsgType))
                                {
                                    HandleRpcRequest(ws, msg);
                                }
                                else
                                {
                                    messageReceived(msg);
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
                                            Message = ex.Message
                                        }
                                    }.ToBytes());
                                }
                                else
                                {
                                    throw;
                                }
                            }
                        });
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

        private static void HandleRpcRequest(WebSocket ws, Message msg)
        {
            var rpcRequestMsg = new RpcRequestMessage(msg);
            var req = rpcRequestMsg.Request;
            var contractImpl = WebSocketWrapperContext.ResolveRpcContractImpl(req.Contract);
            var contractImplType = contractImpl.GetType();
            var methodDef = contractImplType.GetMethod(req.Method);
            var parameters = new object[methodDef.GetParameters().Length];
            var i = 0;
            foreach (var p in methodDef.GetParameters())
            {
                parameters[i] = Convert.ChangeType(req.Parameters[i], p.ParameterType);
                ++i;
            }
            var result = contractImplType
                .InvokeMember(req.Method, BindingFlags.InvokeMethod, null, contractImpl, parameters);
            ws.Send(new RpcResponseMessage(msg.Id)
            {
                Response = new RpcResponseMessage.RpcResponse()
                {
                    Result = result
                }
            }.ToBytes());
        }

        public static T Request<T>(this WebSocket ws, Message req) where T : Message
        {
            return Request<T>(ws, req, WebSocketWrapperContext.RequestTimeout);
        }

        public static T Request<T>(this WebSocket ws, Message req, int timeout) where T : Message
        {
            return Coordinate<T>(() => ws.Send(req.ToBytes()), req, timeout);
        }

        internal static T Coordinate<T>(Action send, Message req) where T : Message
        {
            return Coordinate<T>(send, req, WebSocketWrapperContext.RequestTimeout);
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