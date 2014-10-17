using System.Text;

namespace TrotiNet {

    /// <summary>
    ///     Dummy proxy that simply echoes back what it gets from the browser
    /// </summary>
    /// Used for TCP testing.
    public class ProxyDummyEcho : AbstractProxyLogic {

        private readonly bool bPrintEchoPrefix;

        /// <summary>
        ///     Instantiate a dummy proxy that echoes what it reads on the
        ///     socket back to it
        /// </summary>
        /// <param name="clientHttpSocket">Client socket</param>
        /// <param name="PrintEchoPrefix">
        ///     If true, the proxy will add an
        ///     "Echo" prefix for each message
        /// </param>
        public ProxyDummyEcho(HttpSocket clientHttpSocket, bool PrintEchoPrefix) : base(clientHttpSocket) {
            bPrintEchoPrefix = PrintEchoPrefix;
        }

        /// <summary>
        ///     Static constructor with <c>PrintEchoPrefix = true</c>
        /// </summary>
        public static AbstractProxyLogic CreateEchoProxy(HttpSocket socketBP) {
            return new ProxyDummyEcho(socketBP, true);
        }

        /// <summary>
        ///     Static constructor with <c>PrintEchoPrefix = false</c>
        /// </summary>
        public static AbstractProxyLogic CreateMirrorProxy(HttpSocket socketBP) {
            return new ProxyDummyEcho(socketBP, false);
        }

        /// <summary>
        ///     Dummy logic loop, for test purposes
        /// </summary>
        public override bool LogicLoop() {
            uint r = ClientHttpSocket.ReadBinary();
            if (r == 0)
                // Connection closed
                return false;

            string s = Encoding.ASCII.GetString(
                ClientHttpSocket.Buffer, 0, (int) r);
            if (bPrintEchoPrefix) {
                ClientHttpSocket.WriteBinary(Encoding.
                    ASCII.GetBytes("Echo: "));
            }
            ClientHttpSocket.WriteBinary(ClientHttpSocket.Buffer, r);

            if (s.StartsWith("x"))
                return false;
            return true;
        }

    }

}