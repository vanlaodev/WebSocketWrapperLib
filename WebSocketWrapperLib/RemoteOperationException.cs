using System;

namespace WebSocketWrapperLib
{
    public class RemoteOperationException : Exception
    {
        public RemoteOperationException(string message) : base(message)
        {
        }
    }
}