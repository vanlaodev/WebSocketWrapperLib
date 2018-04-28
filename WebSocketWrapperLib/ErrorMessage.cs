using System.Text;

namespace WebSocketWrapperLib
{
    public class ErrorMessage : Message
    {
        public const string MsgType = "Error";

        public ErrorMessage()
        {
        }

        public ErrorMessage(string replyId) : base(replyId)
        {
        }

        public ErrorMessage(Message message) : base(message)
        {
        }

        public ErrorInfo Error
        {
            get { return WebSocketWrapper.ObjectSerializer.Deserialize<ErrorInfo>(Encoding.UTF8.GetString(RawData)); }
            set { RawData = Encoding.UTF8.GetBytes(WebSocketWrapper.ObjectSerializer.Serialize(value)); }
        }

        public class ErrorInfo
        {
            public string Message { get; set; }
        }

        protected override string DefaultType
        {
            get { return MsgType; }
        }
    }
}