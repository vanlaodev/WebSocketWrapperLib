using System;

namespace WebSocketWrapperLib
{
    internal static class Ext
    {
        public static Exception GetInnermostException(this Exception exception)
        {
            var ex = exception;
            while (ex.InnerException != null)
            {
                ex = ex.InnerException;
            }
            return ex;
        }
    }
}