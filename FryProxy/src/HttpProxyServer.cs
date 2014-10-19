using System;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace FryProxy {

    public class HttpProxyServer {

        private readonly HttpProxyWorker _worker;

        public HttpProxyServer(String host, HttpProxy httpProxy) : this(host, 0, httpProxy) {}

        public HttpProxyServer(String host, Int32 port, HttpProxy httpProxy)
            : this(new DnsEndPoint(host, port, AddressFamily.InterNetwork), httpProxy) {}

        public HttpProxyServer(DnsEndPoint proxyEndPoint, HttpProxy httpProxy) : this(ToIPEndPoint(proxyEndPoint), httpProxy) {}

        public HttpProxyServer(IPEndPoint proxyEndPoint, HttpProxy httpProxy) {
            Contract.Requires<ArgumentNullException>(proxyEndPoint != null, "proxyEndPoint");
            Contract.Requires<ArgumentNullException>(httpProxy != null, "httpProxy");

            _worker = new HttpProxyWorker(proxyEndPoint, httpProxy);
        }

        public IPEndPoint ProxyEndPoint {
            get { return _worker.LocalEndPoint; }
        }

        public HttpProxy Proxy {
            get { return _worker.Proxy; }
        }

        public Boolean IsListening {
            get { return _worker.Busy; }
        }

        private static IPEndPoint ToIPEndPoint(DnsEndPoint proxyEndPoint) {
            Contract.Requires<ArgumentNullException>(proxyEndPoint != null, "proxyEndPoint");

            var ipAddress = Dns.GetHostAddresses(proxyEndPoint.Host)
                .First(address => address.AddressFamily == AddressFamily.InterNetwork);

            return new IPEndPoint(ipAddress, proxyEndPoint.Port);
        }

        public WaitHandle Start() {
            var startUpEvent = new ManualResetEvent(false);

            ThreadPool.QueueUserWorkItem(_ => _worker.Start(startUpEvent));

            return startUpEvent;
        }

        public void Stop() {
            _worker.Stop();
        }

    }

}