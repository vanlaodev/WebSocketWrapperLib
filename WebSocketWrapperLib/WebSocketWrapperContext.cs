using System;
using System.Collections.Generic;
using WebSocketSharp;

namespace WebSocketWrapperLib
{
    public static class WebSocketWrapperContext
    {
        private static readonly Dictionary<string, object> RegisteredRpcContractImpls = new Dictionary<string, object>();
        public static int RequestTimeout = 30 * 1000;

        public static T GenerateRpcContract<T>(WebSocket ws)
        {
            return GenericContractGenerator.Generate<T>(info =>
            {
                var resp = ws.Request<RpcResponseMessage>(new RpcRequestMessage()
                {
                    Request = new RpcRequestMessage.RpcRequest()
                    {
                        Contract = info.Contract,
                        Method = info.Method,
                        Parameters = info.Parameters
                    }
                });
                return resp.Response.Result;
            });
        }

        public static void RegisterRpcContractImpl<T>(object impl)
        {
            var interfaceType = typeof(T);
            var implType = impl.GetType();
            if (interfaceType.IsInterface && implType.IsClass && !implType.IsAbstract &&
                interfaceType.IsAssignableFrom(implType))
            {
                lock (RegisteredRpcContractImpls)
                {
                    RegisteredRpcContractImpls[interfaceType.FullName] = impl;
                }
            }
            else
            {
                throw new Exception("Can not register implementation to contract interface.");
            }
        }

        public static T ResolveRpcContractImpl<T>()
        {
            var type = typeof(T);
            return (T)ResolveRpcContractImpl(type.FullName);
        }

        public static object ResolveRpcContractImpl(string contractType)
        {
            lock (RegisteredRpcContractImpls)
            {
                if (RegisteredRpcContractImpls.ContainsKey(contractType))
                {
                    return RegisteredRpcContractImpls[contractType];
                }
            }
            throw new Exception("Contract implementation not found.");
        }
    }
}