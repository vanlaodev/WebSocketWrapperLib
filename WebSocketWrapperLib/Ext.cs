using System;
using System.Collections.Generic;
using System.Linq;
using WebSocketSharp.Server;

namespace WebSocketWrapperLib
{
    public static class Ext
    {
        internal static Exception GetInnermostException(this Exception exception)
        {
            var ex = exception;
            while (ex.InnerException != null)
            {
                ex = ex.InnerException;
            }
            return ex;
        }

        public static IEnumerable<T> GetSessions<T>(this WebSocketServer wssv, string path) where T : IWebSocketSession
        {
            WebSocketServiceHost serviceHost;
            if (wssv.WebSocketServices.TryGetServiceHost(path, out serviceHost))
            {
                return serviceHost.Sessions.Sessions.Cast<T>().ToList();
            }
            return null;
        }
    }
}