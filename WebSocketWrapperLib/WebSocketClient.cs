using System;
using System.Threading;
using System.Threading.Tasks;
using WebSocketSharp;

namespace WebSocketWrapperLib
{
    public class WebSocketClient : WebSocket
    {
        private int _reconnectInterval;
        private Thread _autoReconnectWorker;
        private volatile bool _autoReconnectWorkerEnabled;
        private readonly object _lockForAutoReconnectWorkerInterval = new object();
        private readonly object _lockForStartStopAutoReconnectWorker = new object();

        public event Action<Message> MessageReceived;
        public event Action OnReconnecting;

        public WebSocketClient(string url, params string[] protocols) : base(url, protocols)
        {
            OnClose += OnOnClose;
            OnOpen += OnOnOpen;
            OnMessage += OnOnMessage;

            AutoReconnect = true;
            ReconnectBackOffMultiplier = 2;
            InitialReconnectInterval = 5 * 1000;
            MaxReconnectInterval = 2 * 60 * 1000;
        }

        public bool AutoReconnect { get; set; }
        public int ReconnectBackOffMultiplier { get; set; }
        public int InitialReconnectInterval { get; set; }
        public int MaxReconnectInterval { get; set; }

        private void OnOnMessage(object sender, MessageEventArgs e)
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
                        var callback = MessageReceived;
                        if (callback != null)
                        {
                            Task.Run(() =>
                            {
                                try
                                {
                                    callback(msg);
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
                    }
                    else
                    {
                        RequestResponseBehaviorCoordinator.OnResponse(msg);
                    }
                }
            }
        }

        public T Request<T>(Message req) where T : Message
        {
            return RequestResponseBehaviorCoordinator.Coordinate<T>(() => Send(req.ToBytes()), req);
        }

        public T Request<T>(Message req, int timeout) where T : Message
        {
            return RequestResponseBehaviorCoordinator.Coordinate<T>(() => Send(req.ToBytes()), req, timeout);
        }

        private void OnOnOpen(object sender, EventArgs eventArgs)
        {
            StopAutoReconnectWorker();
        }

        private void StopAutoReconnectWorker()
        {
            lock (_lockForStartStopAutoReconnectWorker)
            {
                _autoReconnectWorkerEnabled = false;
                if (_autoReconnectWorker != null)
                {
                    lock (_lockForAutoReconnectWorkerInterval)
                    {
                        Monitor.Pulse(_lockForAutoReconnectWorkerInterval);
                    }
                    _autoReconnectWorker.Join();
                    _autoReconnectWorker = null;
                }
            }
        }

        private void OnOnClose(object sender, CloseEventArgs closeEventArgs)
        {
            if (AutoReconnect)
            {
                StartAutoReconnectWorker();
            }
        }

        private void StartAutoReconnectWorker()
        {
            lock (_lockForStartStopAutoReconnectWorker)
            {
                if (_autoReconnectWorkerEnabled) return;
                _autoReconnectWorkerEnabled = true;
                _reconnectInterval = InitialReconnectInterval < 0 ? 5 * 1000 : InitialReconnectInterval;
                _autoReconnectWorker = new Thread(() =>
                {
                    while (_autoReconnectWorkerEnabled && ReadyState != WebSocketState.Open)
                    {
                        try
                        {
                            var onReconnecting = OnReconnecting;
                            if (onReconnecting != null)
                            {
                                onReconnecting();
                            }
                            ConnectAsync();
                            if (ReadyState != WebSocketState.Open)
                            {
                                lock (_lockForAutoReconnectWorkerInterval)
                                {
                                    if (_autoReconnectWorkerEnabled)
                                    {
                                        Monitor.Wait(_lockForAutoReconnectWorkerInterval, _reconnectInterval);
                                        if (_reconnectInterval * ReconnectBackOffMultiplier <= MaxReconnectInterval)
                                        {
                                            _reconnectInterval = _reconnectInterval * ReconnectBackOffMultiplier;
                                        }
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                    _autoReconnectWorkerEnabled = false;
                });
                _autoReconnectWorker.Start();
            }
        }

        public void PrepareForDisposal()
        {
            AutoReconnect = false;
            StopAutoReconnectWorker();
        }
    }
}