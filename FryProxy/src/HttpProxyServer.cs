using System;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace FryProxy {

    /// <summary>
    ///     Server which listens to incoming connections and delegates handling to provided <see cref="HttpProxy" />
    /// </summary>
    public class HttpProxyServer {

        private readonly HttpProxyWorker _worker;

        /// <summary>
        ///     Create server bound to given hostname and random port
        /// </summary>
        /// <param name="hostname">hostname to bind</param>
        /// <param name="httpProxy">proxy which will handle incoming requests</param>
        public HttpProxyServer(String hostname, HttpProxy httpProxy) : this(hostname, 0, httpProxy) {}

        /// <summary>
        ///     Create server bound to given hostname and port
        /// </summary>
        /// <param name="hostname">hostname to bind</param>
        /// <param name="port">port to bind</param>
        /// <param name="httpProxy">proxy which will handle incoming requests</param>
        public HttpProxyServer(String hostname, Int32 port, HttpProxy httpProxy)
            : this(new DnsEndPoint(hostname, port, AddressFamily.InterNetwork), httpProxy) {}

        /// <summary>
        ///     Create server bound to given local endpoint
        /// </summary>
        /// <param name="proxyEndPoint">local endpoint to bind</param>
        /// <param name="httpProxy">proxy which will handle incoming requests</param>
        public HttpProxyServer(DnsEndPoint proxyEndPoint, HttpProxy httpProxy) : this(ToIPEndPoint(proxyEndPoint), httpProxy) {}

        /// <summary>
        ///     Create server bound to given local endpoint
        /// </summary>
        /// <param name="proxyEndPoint">local endpoint to bind</param>
        /// <param name="httpProxy">proxy which will handle incoming requests</param>
        public HttpProxyServer(IPEndPoint proxyEndPoint, HttpProxy httpProxy) {
            Contract.Requires<ArgumentNullException>(proxyEndPoint != null, "proxyEndPoint");
            Contract.Requires<ArgumentNullException>(httpProxy != null, "httpProxy");

            _worker = new HttpProxyWorker(proxyEndPoint, httpProxy);
        }

        /// <summary>
        ///     Local endpoint server is bound to
        /// </summary>
        public IPEndPoint ProxyEndPoint {
            get { return _worker.LocalEndPoint; }
        }

        /// <summary>
        ///     Proxy which handles incoming request
        /// </summary>
        public HttpProxy Proxy {
            get { return _worker.Proxy; }
        }

        /// <summary>
        ///     Indicates if server is running and expecting requests
        /// </summary>
        public Boolean IsListening {
            get { return _worker.Busy; }
        }

        private static IPEndPoint ToIPEndPoint(DnsEndPoint proxyEndPoint) {
            Contract.Requires<ArgumentNullException>(proxyEndPoint != null, "proxyEndPoint");

            var ipAddress = Dns.GetHostAddresses(proxyEndPoint.Host)
                .First(address => address.AddressFamily == AddressFamily.InterNetwork);

            return new IPEndPoint(ipAddress, proxyEndPoint.Port);
        }

        /// <summary>
        ///     Initialize server and bind it to local endpoint
        /// </summary>
        /// <returns>handle triggered once server is started</returns>
        public WaitHandle Start() {
            var startUpEvent = new ManualResetEvent(false);

            ThreadPool.QueueUserWorkItem(_ => _worker.Start(startUpEvent));

            return startUpEvent;
        }

        /// <summary>
        ///     Stop listening and unbind from local enpoint
        /// </summary>
        public void Stop() {
            _worker.Stop();
        }

    }

}