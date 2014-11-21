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

        private readonly ILog _logger;

        public HttpProxyWorker(IPEndPoint proxyEndPoint, HttpProxy httpProxy) : this(new TcpListener(proxyEndPoint), httpProxy) {}

        private HttpProxyWorker(TcpListener listener, HttpProxy httpProxy) {
            _logger = LogManager.GetLogger(GetType());
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

            lock (_listener) {
                _listener.Start();
                _logger.InfoFormat("started on {0}", LocalEndPoint);
                _listener.BeginAcceptSocket(AcceptClientSocket, null);
            }

            startEventHandle.Set();
        }

        private void AcceptClientSocket(IAsyncResult ar) {
            Socket socket;

            lock (_listener) {
                try {
                    socket = _listener.EndAcceptSocket(ar);
                } catch (ObjectDisposedException) {
                    return;
                }

                _listener.BeginAcceptSocket(AcceptClientSocket, null);    
            }

            try {
                _httpProxy.HandleClient(socket);
            } catch (Exception ex) {
                _logger.Error("Failed to handle client request", ex);
            } finally {
                socket.Close();
                socket.Dispose();
            }
        }
        
        public void Stop() {
            try {
                _listener.Stop();
            } catch (Exception ex) {
                _logger.Warn("Error occured while stopping proxy worker", ex);
            } finally {
                _logger.InfoFormat("stopped on {0}", LocalEndPoint);
            }
        }

    }

}