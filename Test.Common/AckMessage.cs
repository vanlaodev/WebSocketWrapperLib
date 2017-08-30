using WebSocketWrapperLib;

namespace Test.Common
{
    public class AckMessage : Message
    {
        public const string MsgType = "Ack";

        public AckMessage()
        {
        }

        public AckMessage(string replyId) : base(replyId)
        {
        }

        public AckMessage(Message message) : base(message)
        {
        }

        protected override string DefaultType
        {
            get { return MsgType; }
        }
    }
}