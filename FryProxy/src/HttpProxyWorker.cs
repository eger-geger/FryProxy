using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using log4net;

namespace FryProxy
{
    internal class HttpProxyWorker
    {
        private readonly HttpProxy _httpProxy;
        private readonly TcpListener _listener;

        private readonly ILog _logger = LogManager.GetLogger(typeof (HttpProxyWorker));
        private readonly ISet<Socket> _openSockets;
        private Thread _acceptSocketThread;
        private Boolean _shuttingDown;

        public HttpProxyWorker(IPEndPoint proxyEndPoint, HttpProxy httpProxy)
            : this(new TcpListener(proxyEndPoint), httpProxy)
        {
        }

        private HttpProxyWorker(TcpListener listener, HttpProxy httpProxy)
        {
            _openSockets = new HashSet<Socket>();
            _httpProxy = httpProxy;
            _listener = listener;
        }

        public IPEndPoint LocalEndPoint
        {
            get { return _listener.LocalEndpoint as IPEndPoint; }
        }

        public HttpProxy Proxy
        {
            get { return _httpProxy; }
        }

        public Boolean Active
        {
            get { return _listener.Server.IsBound; }
        }

        public void Start(EventWaitHandle startEventHandle)
        {
            Contract.Requires<ArgumentNullException>(startEventHandle != null, "startEventHandle");

            if (!Active)
            {
                var waitHandle = new ManualResetEventSlim(false);

                lock (_listener)
                {
                    _shuttingDown = false;
                    
                    _listener.Start();

                    _acceptSocketThread = new Thread(AcceptSocketLoop);
                    
                    _acceptSocketThread.Start(waitHandle);
                }

                waitHandle.Wait();

                _logger.DebugFormat("started on {0}", LocalEndPoint);
            }

            startEventHandle.Set();
        }

        private void AcceptSocketLoop(Object startEvent)
        {
            var resetHandle = new AutoResetEvent(false);

            if (startEvent is ManualResetEventSlim)
            {
                (startEvent as ManualResetEventSlim).Set();
            }

            do
            {
                _logger.Debug("Begin Accept Socket");

                try
                {
                    _listener.BeginAcceptSocket(AcceptClientSocket, resetHandle);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
            } while (!_shuttingDown && resetHandle.WaitOne());

            if (_logger.IsDebugEnabled)
            {
                _logger.Debug("Socket Accept loop finished");
            }
        }

        private void AcceptClientSocket(IAsyncResult ar)
        {
            Socket socket = null;

            try
            {
                socket = _listener.EndAcceptSocket(ar);
            }
            catch
            {
                if (socket != null)
                {
                    socket.Close();
                }

                return;
            }
            finally
            {
                var resetEvent = ar.AsyncState as AutoResetEvent;

                if (resetEvent != null)
                {
                    resetEvent.Set();
                }   
            }

            ThreadPool.QueueUserWorkItem(ignore => HandleSocket(socket));

            _logger.Debug("End Accept Socket");
        }

        private void HandleSocket(Socket socket)
        {
            lock (_openSockets)
            {
                _openSockets.Add(socket);
            }

            try
            {
                _httpProxy.HandleClient(socket);
            }
            catch (Exception ex)
            {
                _logger.Debug("Failed to handle client request", ex);
            }

            socket.Close();

            lock (_openSockets)
            {
                _openSockets.Remove(socket);
            }
        }

        public void Stop()
        {
            lock (_listener)
            {
                if (!Active)
                {
                    return;
                }

                _shuttingDown = true;

                try
                {
                    if (!_acceptSocketThread.Join(TimeSpan.FromSeconds(5)))
                    {
                        _acceptSocketThread.Abort();
                    }
                }
                catch (Exception ex)
                {
                    _logger.Debug("Error occured while stopping", ex);
                }

                try
                {
                    _listener.Stop();
                }
                catch (Exception ex)
                {
                    _logger.Debug("Error while stopping", ex);
                }
            }

            lock (_openSockets)
            {
                foreach (Socket socket in _openSockets)
                {
                    socket.Close();
                }

                _openSockets.Clear();
            }

            _logger.DebugFormat("stopped on {0}", LocalEndPoint.Address);
        }
    }
}