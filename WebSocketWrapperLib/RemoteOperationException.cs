using System;

namespace WebSocketWrapperLib
{
    public class RemoteOperationException : Exception
    {
        public string ExceptionType { get; private set; }

        public RemoteOperationException(string message, string exceptionType) : base(message)
        {
            ExceptionType = exceptionType;
        }
    }
}