using System.Text;
using WebSocketWrapperLib;

namespace Test.Common
{
    public class TextMessage : Message
    {
        public const string MsgType = "Text";

        public TextMessage()
        {
        }

        public TextMessage(string replyId) : base(replyId)
        {
        }

        public TextMessage(Message message) : base(message)
        {
        }

        public string Text
        {
            get { return Encoding.UTF8.GetString(RawData); }
            set { RawData = Encoding.UTF8.GetBytes(value); }
        }

        protected override string DefaultType
        {
            get { return MsgType; }
        }
    }
}