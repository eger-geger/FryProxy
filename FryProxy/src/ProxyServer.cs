using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

using log4net;

namespace FryProxy {

    public class ProxyServer : IDisposable {

        private readonly Thread _listeningThread;

        private readonly ILog _logger = LogManager.GetLogger(typeof(ProxyServer));

        public ProxyServer(IPAddress address, Int32 port) {
            var startUpEvent = new ManualResetEventSlim(false);

            _listeningThread = new Thread(() => StartListener(new TcpListener(address, port), startUpEvent)) {
                IsBackground = true,
                Name = "ProxyServer Thread"
            };

            _listeningThread.Start();

            startUpEvent.Wait();

            _logger.InfoFormat("STARTED on {0}:{1}", address, port);
        }

        public Boolean IsListening { get; private set; }

        public void Dispose() {
            IsListening = false;
            _listeningThread.Abort();
        }

        private void StartListener(TcpListener listener, ManualResetEventSlim startUpEvent) {
            listener.Start();

            IsListening = true;

            startUpEvent.Set();

            _logger.Info("LISTENING");

            var proxy = new HttpProxy();

            while (IsListening) {
                ThreadPool.QueueUserWorkItem(s => proxy.Handle((Socket) s), listener.AcceptSocket());
            }

            listener.Stop();
        }

    }

}