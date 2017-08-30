using System;
using System.Threading.Tasks;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace WebSocketWrapperLib
{
    public abstract class WebSocketBehaviorEx : WebSocketBehavior
    {
        protected Message Request<T>(Message req, int timeout) where T : Message
        {
            return RequestResponseBehaviorCoordinator.Coordinate<T>(() => Send(req.ToBytes()), req, timeout);
        }

        protected override void OnMessage(MessageEventArgs e)
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
                                OnMessage(msg);
                            }
                            catch (Exception ex)
                            {
                                if (msg.RequireReply)
                                {
                                    Send(new ErrorMessage(msg.Id)
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
                        RequestResponseBehaviorCoordinator.OnResponse(msg);
                    }
                }
            }
        }

        protected virtual void OnMessage(Message msg)
        {

        }
    }
}