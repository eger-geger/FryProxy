using System;
using System.Diagnostics.Contracts;
using System.Net;
using System.Net.Sockets;
using System.Threading;

using log4net;

namespace FryProxy {

    internal class HttpProxyWorker {

        private readonly HttpProxy _httpProxy;
        private readonly TcpListener _listener;
        private Thread _workingThread;

        public HttpProxyWorker(IPEndPoint proxyEndPoint, HttpProxy httpProxy) : this(new TcpListener(proxyEndPoint), httpProxy) {}

        private HttpProxyWorker(TcpListener listener, HttpProxy httpProxy) {
            _httpProxy = httpProxy;
            _listener = listener;
        }

        public IPEndPoint LocalEndPoint {
            get { return _listener.LocalEndpoint as IPEndPoint; }
        }

        public HttpProxy Proxy {
            get { return _httpProxy; }
        }

        public Boolean Active {
            get { return _listener.Server.IsBound; }
        }

        public void Start(EventWaitHandle startEventHandle) {
            Contract.Requires<ArgumentNullException>(startEventHandle != null, "startEventHandle");

            if (Active) {
                startEventHandle.Set();
                return;
            }

            _workingThread = new Thread(AcceptSocketLoop);

            _listener.Start();
            _workingThread.Start();
            startEventHandle.Set();

            if (IsDebugEnabled) {
                Logger.DebugFormat("started on {0}", LocalEndPoint);
            }
        }

        private void AcceptSocketLoop() {
            var resetHandle = new AutoResetEvent(false);

            do {
                _listener.BeginAcceptSocket(AcceptClientSocket, resetHandle);
            } while (resetHandle.WaitOne());
        }

        private void AcceptClientSocket(IAsyncResult ar) {
            Socket socket = null;

            try {
                socket = _listener.EndAcceptSocket(ar);
            } catch {
                if (socket != null) {
                    socket.Close();
                }

                return;
            } finally {
                var resetEvent = ar.AsyncState as AutoResetEvent;

                if (resetEvent != null) {
                    resetEvent.Set();
                }
            }

            try {
                _httpProxy.HandleClient(socket);
            } catch (Exception ex) {
                if (IsDebugEnabled) {
                    Logger.Debug("Failed to handle client request", ex);
                }
            } finally {
                socket.Close();
            }
        }

        public void Stop() {
            try {
                _listener.Stop();

                if (_workingThread != null) {
                    _workingThread.Abort();
                }
            } catch (Exception ex) {
                if (IsDebugEnabled) {
                    Logger.Debug("Error occured while stopping proxy worker", ex);
                }
            } finally {
                if (IsDebugEnabled) {
                    Logger.DebugFormat("stopped on {0}", LocalEndPoint.Address);
                }
            }
        }

        private static readonly ILog Logger = LogManager.GetLogger(typeof(HttpProxyWorker));
        private static readonly Boolean IsDebugEnabled = Logger.IsDebugEnabled;

    }

}