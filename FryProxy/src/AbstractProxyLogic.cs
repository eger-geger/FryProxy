using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

using log4net;

using TrotiNet.Http;

namespace TrotiNet {

    /// <summary>
    ///     Abstract class for all HTTP proxy logic implementations
    /// </summary>
    /// <remarks>
    ///     One instance of a derived class will be created per client connection.
    /// </remarks>
    public abstract class AbstractProxyLogic {

        /// <summary>
        ///     Name of the host to which <c>ClientHttpSocket</c> is currently connected
        /// </summary>
        protected string DestinationHostName;

        /// <summary>
        ///     Port to which <c>ClientHttpSocket</c> is currently connected
        /// </summary>
        protected int DestinationPort;

        /// <summary>
        ///     Set to a proxy host name if our proxy is not connecting to the internet, but to another proxy instead
        /// </summary>
        protected string RelayHttpProxyHost;

        /// <summary>
        ///     Set to a proxy bypass specification if our proxy is not connecting to the internet, but to another proxy instead
        /// </summary>
        /// <remarks>
        ///     XXX Bypass not implemented
        /// </remarks>
        protected string RelayHttpProxyOverride;

        /// <summary>
        ///     Set to a proxy port if our proxy is not connecting to the internet, but to another proxy instead
        /// </summary>
        protected int RelayHttpProxyPort;

        /// <summary>
        ///     Socket dedicated to the (client) browser-proxy communication
        /// </summary>
        protected HttpSocket ClientHttpSocket;

        /// <summary>
        ///     Socket dedicated to the proxy-server (remote) communication
        /// </summary>
        protected HttpSocket ServerSocket;

        protected readonly ILog Logger;

        protected readonly Socket ClientSocket; 

        /// <summary>
        ///     Common constructor for proxies; one proxy instance is created per client connection
        /// </summary>
        /// <param name="clientHttpSocket">Client socket</param>
        protected AbstractProxyLogic(HttpSocket clientHttpSocket) {
            if (clientHttpSocket == null) {
                throw new ArgumentNullException("clientHttpSocket");
            }

            ClientSocket = clientHttpSocket.LowLevelSocket;
            ClientHttpSocket = clientHttpSocket;
            ServerSocket = null;
            Logger = LogManager.GetLogger(GetType());
        }

        /// <summary>
        ///     If necessary, connect the remote <c>ServerSocket</c> socket to the given host and port.
        ///     If ServerSocket is already connected to the right host and port, the socket is reused as is.
        /// </summary>
        /// <param name="hostname">Remote host name</param>
        /// <param name="port">Remote port</param>
        protected void Connect(string hostname, int port) {
            if (String.IsNullOrEmpty(hostname)) {
                throw new ArgumentException("Should no be empty", "hostname");
            }

            if (port <= 0) {
                throw new ArgumentOutOfRangeException("port");
            }

            if (DestinationHostName != null &&
                DestinationHostName.Equals(hostname) &&
                DestinationPort == port &&
                (ServerSocket != null && !ServerSocket.IsSocketDead()))
                return;

            if (ServerSocket != null) {
                Logger.Debug("Changing hostname/port from " +
                          DestinationHostName + ":" + DestinationPort +
                          " to " + hostname + ":" + port);

                // We have a socket connected to the wrong host (or port)
                ServerSocket.CloseSocket();
                ServerSocket = null;
            }

            IPAddress[] ips = Dns.GetHostAddresses(hostname);

            Socket socket = null;
            
            Exception e = null;
            
            foreach (var ip in ips) {
                try {
                    socket = new Socket(ip.AddressFamily, SocketType.Stream,
                        ProtocolType.Tcp);
                    socket.Connect(ip, port);
                    break;
                } catch (Exception ee) {
                    if (ip.Equals(IPAddress.IPv6Loopback))
                        // Do not log that
                        continue;

                    if (e == null)
                        e = ee;
                    if (socket != null) {
                        socket.Close();
                        socket = null;
                    }

                    Logger.Error(ee);
                }
            }
            if (socket == null)
                throw e;

            // Checked up, and good to go
            ServerSocket = new HttpSocket(socket);
            DestinationHostName = hostname;
            DestinationPort = port;

            Logger.Debug("ServerSocket connected to " + hostname + ":" + port);
        }

        /// <summary>
        ///     Extract the host and port to use from either the HTTP request line, or the HTTP headers; update the request line to remove the hostname and port
        /// </summary>
        /// <param name="hrl">
        ///     The HTTP request line; the Path will be updated to remove the host name and port number
        /// </param>
        /// <param name="hh_rq">
        ///     The HTTP request headers
        /// </param>
        /// <param name="port">
        ///     When this method returns, contains the request port
        /// </param>
        /// <remarks>
        ///     May modify the Path of <c>hrl</c>
        /// </remarks>
        protected string ParseDestinationHostAndPort(HttpRequestLine hrl, HttpHeaders hh_rq, out int port) {
            string host = null;
            
            port = hrl.Method.Equals("CONNECT") ? 443 : 80;

            bool bIsHTTP1_0 = hrl.HTTPVersion.Equals("1.0");

            if (hrl.Path.Equals("*")) {
                Debug.Assert(!bIsHTTP1_0);
                goto hostname_from_header;
            }

            // At this point, hrl.Path follows one of these forms:
            // - scheme:(//authority)/abs_path
            // - authority
            // - /abs_path

            int prefix = 0; // current parse position
            if (hrl.Path.Contains("://")) {
                if (hrl.Path.StartsWith("http://"))
                    prefix = 7; // length of "http://"
                else if (hrl.Path.StartsWith("https://")) {
                    prefix = 8; // length of "https://"
                    port = 443;
                } else {
                    throw new HttpProtocolBroken(
                        "Expected scheme missing or unsupported");
                }
            }

            // Starting from offset prefix, we now have either:
            // 1) authority (only for CONNECT)
            // 2) authority/abs_path
            // 3) /abs_path

            int slash = hrl.Path.IndexOf('/', prefix);
            string authority = null;
            if (slash == -1) {
                // case 1
                authority = hrl.Path;
            } else if (slash > 0) // Strict inequality
                // case 2
                authority = hrl.Path.Substring(prefix, slash - prefix);

            if (authority != null) {
                // authority is either:
                // a) hostname
                // b) hostname:
                // c) hostname:port

                int c = authority.IndexOf(':');
                if (c < 0)
                    // case a)
                    host = authority;
                else if (c == authority.Length - 1)
                    // case b)
                    host = authority.TrimEnd('/');
                else {
                    // case c)
                    host = authority.Substring(0, c);
                    port = int.Parse(authority.Substring(c + 1));
                }

                prefix += authority.Length;
            }

            if (host != null) {
#if false
    // XXX Not sure whether this can happen (without doing ad
    // replacement) or if we want to prevent it
                if (hh_rq.Host != null)
                {
                    // Does hh_rq.Host and host match? (disregarding
                    // the potential ":port" prefix of hh_rq.Host)
                    int c2 = hh_rq.Host.IndexOf(':');
                    string rq_host = c2 < 0 ? hh_rq.Host :
                        hh_rq.Host.Substring(0, c2);
                    if (!rq_host.Equals(host))
                        // Host discrepancy: fix the 'Host' header
                        hh_rq.Host = host;
                }
#endif

                // Remove the host from the request Path, unless the "server"
                // is actually a proxy, in which case the Path should remain
                // unchanged. (RFC 2616, section 5.1.2)
                if (RelayHttpProxyHost == null) {
                    hrl.Path = hrl.Path.Substring(prefix);
                    Logger.Debug("Rewriting request line as: " + hrl.RequestLine);
                }

                return host;
            }

            hostname_from_header:
            host = hh_rq.Host;
            if (host == null)
                throw new HttpProtocolBroken("No host specified");
            int cp = host.IndexOf(':');
            if (cp < 0) {
                /* nothing */
            } else if (cp == host.Length - 1)
                host = host.TrimEnd('/');
            else {
                host = host.Substring(0, cp);
                port = int.Parse(host.Substring(cp + 1));
            }
            return host;
        }

        /// <summary>
        ///     Entry point to HTTP request handling
        /// </summary>
        public abstract bool LogicLoop();

        /// <summary>
        ///     In case of a proxy chain, set the next proxy to contact
        /// </summary>
        /// <remarks>
        ///     <c>ProxyOverride</c> is ignored.
        /// </remarks>
        public void SetRelayProxy(SystemProxySettings sps) {
            if (sps == null || !sps.ProxyEnable) {
                RelayHttpProxyHost = null;
                RelayHttpProxyPort = 0;
                return;
            }

            sps.GetHttpSpecificProxy(out RelayHttpProxyHost,
                out RelayHttpProxyPort);
            RelayHttpProxyOverride = null;
        }

        /// <summary>
        ///     Message packet handler for tunneling data from PS to BP
        /// </summary>
        protected void TunnelBP(byte[] msg, uint position, uint to_send) {
            if (to_send == 0)
                return;
            if (ClientHttpSocket.WriteBinary(msg, position, to_send) < to_send)
                throw new IoBroken();
        }

        /// <summary>
        ///     Message packet handler for tunneling data from BP to PS
        /// </summary>
        protected void TunnelPS(byte[] msg, uint position, uint to_send) {
            if (to_send == 0)
                return;
            if (ServerSocket.WriteBinary(msg, position, to_send) < to_send)
                throw new IoBroken();
        }

    }

}