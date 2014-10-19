using System;
using System.Diagnostics.Contracts;
using System.Net;
using System.Net.Sockets;
using System.Threading;

using log4net;

namespace FryProxy {

    public class HttpProxyWorker {

        private readonly HttpProxy _httpProxy;

        private readonly TcpListener _listener;
        private readonly ILog _logger;

        public HttpProxyWorker(IPEndPoint proxyEndPoint, HttpProxy httpProxy) : this(new TcpListener(proxyEndPoint), httpProxy) {}

        private HttpProxyWorker(TcpListener listener, HttpProxy httpProxy) {
            _logger = LogManager.GetLogger(GetType());
            _httpProxy = httpProxy;
            _listener = listener;

            Busy = false;
        }

        public IPEndPoint LocalEndPoint {
            get { return _listener.LocalEndpoint as IPEndPoint; }
        }

        public HttpProxy Proxy {
            get { return _httpProxy; }
        }

        public Boolean Busy { get; private set; }

        public void Start(EventWaitHandle startEventHandle) {
            Contract.Requires<InvalidOperationException>(!Busy, "Worker is busy");

            _listener.Start();

            _logger.InfoFormat("started on {0}", LocalEndPoint);

            Busy = true;

            startEventHandle.Set();

            while (Busy) {
                ThreadPool.QueueUserWorkItem(HandleSocketConnection, _listener.AcceptSocket());
            }
        }

        public void Stop() {
            if (!Busy) {
                return;
            }

            Busy = false;

            try {
                _listener.Stop();
            } catch (Exception ex) {
                _logger.Warn("Error occured while stopping proxy worker", ex);
            } finally {
                _logger.InfoFormat("stopped on {0}", LocalEndPoint);
            }
        }

        private void HandleSocketConnection(Object socket) {
            _httpProxy.Handle(socket as Socket);
        }

    }

}