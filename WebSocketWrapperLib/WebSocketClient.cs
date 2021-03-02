using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using WebSocketSharp;

namespace WebSocketWrapperLib
{
    public class WebSocketClient : WebSocket
    {
        private readonly Dictionary<string, Func<object>> _registeredRpcContractImpls = new Dictionary<string, Func<object>>();

        private double _reconnectInterval;
        private double _pingPongInterval;
        private Thread _autoReconnectWorker;
        private Thread _autoPingPongWorker;
        private volatile bool _autoReconnectWorkerEnabled;
        private volatile bool _autoPingPongWorkerEnabled;
        private readonly object _lockForAutoPingPongWorkerInterval = new object();
        private readonly object _lockForStartStopAutoPingPongWorker = new object();
        private readonly object _lockForAutoReconnectWorkerInterval = new object();
        private readonly object _lockForStartStopAutoReconnectWorker = new object();
        private readonly ManualResetEventSlim _reconnectWaitHandle = new ManualResetEventSlim(false);
        private readonly ManualResetEventSlim _pingPongWaitHandle = new ManualResetEventSlim(false);
        private readonly RequestResponseBehaviorCoordinator _requestResponseBehaviorCoordinator;

        public event Action<Message> MessageReceived;
        public event Action OnReconnecting;

        public WebSocketClient(string url, params string[] protocols) : base(url, protocols)
        {
            OnClose += OnOnClose;
            OnOpen += OnOnOpen;
            OnMessage += OnOnMessage;

            AutoReconnect = true;
            ReconnectBackOffMultiplier = 2;
            ReconnectInterval = TimeSpan.FromSeconds(5);
            MaxReconnectInterval = TimeSpan.FromMinutes(3);

            AutoPingPong = true;
            AutoPingPongInterval = TimeSpan.FromMinutes(1);

            _requestResponseBehaviorCoordinator = new RequestResponseBehaviorCoordinator();

            if (IsSecure)
            {
                SslConfiguration.EnabledSslProtocols = SslProtocols.Default;
            }
        }

        public RequestResponseBehaviorCoordinator RequestResponseBehaviorCoordinator
        {
            get { return _requestResponseBehaviorCoordinator; }
        }

        public bool AutoPingPong { get; set; }
        public TimeSpan AutoPingPongInterval { get; set; }
        public bool AutoReconnect { get; set; }
        public int ReconnectBackOffMultiplier { get; set; }
        public TimeSpan ReconnectInterval { get; set; }
        public TimeSpan MaxReconnectInterval { get; set; }

        private void OnOnMessage(object sender, MessageEventArgs e)
        {
            var callback = MessageReceived;
            _requestResponseBehaviorCoordinator.OnMessage(this, e, callback, ResolveRpcContractImpl, OnHandleMessageError);
        }

        protected virtual void OnHandleMessageError(Exception obj)
        {

        }

        private void OnOnOpen(object sender, EventArgs eventArgs)
        {
            StopAutoReconnectWorker();

            if (AutoPingPong)
            {
                StartAutoPingPongWorker();
            }
        }

        private void StopAutoPingPongWorker()
        {
            lock (_lockForStartStopAutoPingPongWorker)
            {
                _autoPingPongWorkerEnabled = false;
                if (_autoPingPongWorker != null)
                {
                    _pingPongWaitHandle.Set();
                    lock (_lockForAutoPingPongWorkerInterval)
                    {
                        Monitor.Pulse(_lockForAutoPingPongWorkerInterval);
                    }
                    _autoPingPongWorker.Join();
                    _autoPingPongWorker = null;
                }
            }
        }

        private void StartAutoPingPongWorker()
        {
            lock (_lockForStartStopAutoPingPongWorker)
            {
                StopAutoPingPongWorker();
                _autoPingPongWorkerEnabled = true;
                _pingPongInterval = AutoPingPongInterval.TotalMilliseconds < 5000 ? 5000 : AutoPingPongInterval.TotalMilliseconds;
                _autoPingPongWorker = new Thread(() =>
                {
                    while (_autoPingPongWorkerEnabled && ReadyState == WebSocketState.Open)
                    {
                        try
                        {
                            _pingPongWaitHandle.Reset();
                            Task.Run(() =>
                            {
                                try
                                {
                                    if (ReadyState == WebSocketState.Open && !Ping())
                                    {
                                        //                                        Close(CloseStatusCode.Abnormal, "No pong received after ping.");
                                        Log.Warn("No pong received after ping.");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Log.Error("Error occured while auto ping-pong: " + ex);
                                }
                                finally
                                {
                                    _pingPongWaitHandle.Set();
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            Log.Error("Error occured while auto ping-pong: " + ex);
                        }
                        _pingPongWaitHandle.Wait();
                        if (ReadyState == WebSocketState.Open)
                        {
                            lock (_lockForAutoPingPongWorkerInterval)
                            {
                                if (_autoPingPongWorkerEnabled)
                                {
                                    Monitor.Wait(_lockForAutoPingPongWorkerInterval, (int)_pingPongInterval);
                                }
                            }
                        }
                    }
                    _autoPingPongWorkerEnabled = false;
                });
                _autoPingPongWorker.Start();
            }
        }

        private void StopAutoReconnectWorker()
        {
            lock (_lockForStartStopAutoReconnectWorker)
            {
                _autoReconnectWorkerEnabled = false;
                if (_autoReconnectWorker != null)
                {
                    _reconnectWaitHandle.Set();
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
            _requestResponseBehaviorCoordinator.CancelAll();

            StopAutoPingPongWorker();

            if (AutoReconnect)
            {
                StartAutoReconnectWorker();
            }
        }

        private void StartAutoReconnectWorker()
        {
            lock (_lockForStartStopAutoReconnectWorker)
            {
                StopAutoReconnectWorker();
                _autoReconnectWorkerEnabled = true;
                _reconnectInterval = ReconnectInterval.TotalMilliseconds < 1000 ? 1000 : ReconnectInterval.TotalMilliseconds;
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
                            _reconnectWaitHandle.Reset();
                            Task.Run(() =>
                            {
                                try
                                {
                                    Connect();
                                }
                                catch (Exception ex)
                                {
                                    Log.Error("Error occured while reconnecting: " + ex);
                                }
                                finally
                                {
                                    _reconnectWaitHandle.Set();
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            Log.Error("Error occured while reconnecting: " + ex);
                        }
                        _reconnectWaitHandle.Wait();
                        if (ReadyState != WebSocketState.Open)
                        {
                            lock (_lockForAutoReconnectWorkerInterval)
                            {
                                if (_autoReconnectWorkerEnabled)
                                {
                                    Monitor.Wait(_lockForAutoReconnectWorkerInterval, (int)_reconnectInterval);
                                    if (_reconnectInterval * ReconnectBackOffMultiplier < MaxReconnectInterval.TotalMilliseconds)
                                    {
                                        _reconnectInterval *= ReconnectBackOffMultiplier;
                                    }
                                    else
                                    {
                                        _reconnectInterval = MaxReconnectInterval.TotalMilliseconds;
                                    }
                                }
                            }
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

        public void RegisterRpcContractImpl<TInterface, TImpl>(TImpl impl) where TImpl : TInterface
        {
            RegisterRpcContractImpl<TInterface, TImpl>(() => impl);
        }

        public void RegisterRpcContractImpl<TInterface, TImpl>(Func<TImpl> funcImpl) where TImpl : TInterface
        {
            var implType = typeof(TImpl);
            var interfaceType = typeof(TInterface);
            if (interfaceType.IsInterface && implType.IsClass && !implType.IsAbstract &&
                interfaceType.IsAssignableFrom(implType))
            {
                lock (_registeredRpcContractImpls)
                {
                    _registeredRpcContractImpls[interfaceType.FullName] = () => { return funcImpl(); };
                }
            }
            else
            {
                throw new Exception("Can not register implementation to contract interface.");
            }
        }

        protected T ResolveRpcContractImpl<T>()
        {
            var type = typeof(T);
            return (T)ResolveRpcContractImpl(type.FullName);
        }

        protected object ResolveRpcContractImpl(string contractType)
        {
            lock (_registeredRpcContractImpls)
            {
                if (_registeredRpcContractImpls.ContainsKey(contractType))
                {
                    var funcImpl = _registeredRpcContractImpls[contractType];
                    return funcImpl();
                }
            }
            throw new Exception("Contract implementation not found.");
        }
    }
}