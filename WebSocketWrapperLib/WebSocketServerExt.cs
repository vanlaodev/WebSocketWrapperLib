using System.Linq;
using System.Threading;
using WebSocketSharp.Server;

namespace WebSocketWrapperLib
{
    public static class WebSocketServerExt
    {
        public static T GetCurrentSession<T>(this WebSocketServer wssv) where T : WebSocketBehaviorEx
        {
            foreach (
                var host in
                    wssv.WebSocketServices.Hosts.Where(x => x.Type == typeof(T)))
            {
                return
                    (T)host.Sessions.Sessions.SingleOrDefault(
                        x => ((T)x).ThreadId == Thread.CurrentThread.ManagedThreadId);
            }
            return null;
        }
    }
}