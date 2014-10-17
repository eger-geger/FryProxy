using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;

using TrotiNet.Http;

namespace TrotiNet {

    /// <summary>
    ///     Implement the full HTTP proxy logic for one browser connection
    /// </summary>
    public class BaseProxyLogic : AbstractProxyLogic {

        public delegate Stream RequestHandler(
            HttpRequestLine requestLine, HttpHeaders requestHeaders, Stream requestBody);

        public delegate Stream ResponseHandler(
            HttpStatusLine statusLine, HttpHeaders responseHeaders, Stream responseBody);

        public RequestHandler OnReceiveRequest;

        /// <summary>
        ///     The request headers of the HTTP request currently being handled
        /// </summary>
        protected HttpHeaders RequestHeaders;

        /// <summary>
        ///     The request line of the HTTP request currently being handled
        /// </summary>
        protected HttpRequestLine RequestLine;

        /// <summary>
        ///     The response header line of the HTTP response received
        /// </summary>
        protected HttpHeaders ResponseHeaders;

        /// <summary>
        ///     The response status line of the HTTP response received
        /// </summary>
        protected HttpStatusLine ResponseStatusLine;

        /// <summary>
        ///     Request processing pipeline state
        /// </summary>
        /// <seealso cref="RequestProcessingState" />
        protected RequestProcessingState State;

        /// <summary>
        ///     Base proxy constructor (an arbitrary intermediate step between
        ///     AbstractProxyLogic, and ProxyLogic)
        /// </summary>
        public BaseProxyLogic(HttpSocket clientHttpSocket) : base(clientHttpSocket) {}

        /// <summary>
        ///     Pipeline step: close the connections and stop
        /// </summary>
        protected void AbortRequest() {
            if (ServerSocket != null) {
                ServerSocket.CloseSocket();
                ServerSocket = null;
            }
            State.bPersistConnectionBP = false;
            State.NextStep = null;
        }

        /// <summary>
        ///     Implement a base proxy logic. The procedure is called for each request as long as it returns true.
        /// </summary>
        public override bool LogicLoop() {
            try {
                State = new RequestProcessingState(ReadRequest);

                while (State.NextStep != null) {
                    State.NextStep();
                }

                return State.bPersistConnectionBP;
            } catch {
                AbortRequest();
                throw;
            }
        }

//        /// <summary>
//        ///     Called when RequestLine and RequestHeaders are set
//        /// </summary>
//        protected virtual void RequestHandler() {}

        /// <summary>
        ///     Called when ResponseStatusLine and ResponseHeaders are set
        /// </summary>
        protected virtual void OnReceiveResponse() {}

        /// <summary>
        ///     Pipeline step: read the HTTP request from the client, schedule the next step to be <c>SendRequest</c>, and call
        ///     <c>RequestHandler</c>
        /// </summary>
        protected virtual void ReadRequest() {
            using (var clientStream = new NetworkStream(ClientSocket)) {
                var clientReader = new StreamReader(clientStream, Encoding.UTF8);
                var requestLine = new HttpRequestLine(ReadFirstLine(clientReader));
                var requestHeaders = new HttpHeaders(ReadHeaders(clientReader));

                var requestBody = OnReceiveRequest != null
                    ? OnReceiveRequest(requestLine, requestHeaders, clientStream)
                    : clientStream;

                var serverSocket = ConnectToSocket(DestinationHostName, DestinationPort);

                using (var serverStream = new NetworkStream(serverSocket)) {
                    var serverWriter = new StreamWriter(serverStream);

                    serverWriter.WriteLine(requestLine.RequestLine);

                    foreach (var header in requestHeaders.Headers) {
                        serverWriter.WriteLine(header);
                    }

                    requestBody.CopyTo(serverWriter.BaseStream);

                    serverStream.CopyTo(clientStream);
                }
            }

            Logger.Info("Got request " + RequestLine.RequestLine);

            // We call RequestHandler now because Connect() will
            // modify the request Path.
            State.NextStep = SendRequest;

            // Now we parse the request to:
            // 1) find out where we should connect
            // 2) find out whether there is a message body in the request
            // 3) find out whether the BP connection should be kept-alive
            if (State.NextStep != null) {
                // Step 1)
                if (RelayHttpProxyHost == null) {
                    int NewDestinationPort;
                    string NewDestinationHost = ParseDestinationHostAndPort(
                        RequestLine, RequestHeaders, out NewDestinationPort);
                    Connect(NewDestinationHost, NewDestinationPort);
                } else {
                    Connect(RelayHttpProxyHost, RelayHttpProxyPort);
                }

                // Step 2)
                // Find out whether the request has a message body
                // (RFC 2616, section 4.3); if it has, get the message length
                State.bRequestHasMessage = false;
                State.RequestMessageLength = 0;
                State.bRequestMessageChunked = false;
                if (RequestHeaders.TransferEncoding != null) {
                    State.bRequestHasMessage = true;
                    State.bRequestMessageChunked = Array.IndexOf(
                        RequestHeaders.TransferEncoding, "chunked") >= 0;
                    Debug.Assert(
                        State.bRequestMessageChunked);
                } else if (RequestHeaders.ContentLength != null) {
                    State.RequestMessageLength =
                        (uint) RequestHeaders.ContentLength;

                    // Note: HTTP 1.0 wants "Content-Length: 0" when there
                    // is no entity body. (RFC 1945, section 7.2)
                    if (State.RequestMessageLength > 0) {
                        State.bRequestHasMessage = true;
                    }
                }
            }
            // Step 3)
            State.bUseDefaultPersistBP = true;
            if (RequestHeaders.ProxyConnection != null) {
                // Note: This is not part of the HTTP 1.1 standard. See
                // http://homepage.ntlworld.com./jonathan.deboynepollard/FGA/web-proxy-connection-header.html
                foreach (string i in RequestHeaders.ProxyConnection) {
                    if (i.Equals("close")) {
                        State.bPersistConnectionBP = false;
                        State.bUseDefaultPersistBP = false;
                        break;
                    }
                    if (i.Equals("keep-alive")) {
                        State.bPersistConnectionBP = true;
                        State.bUseDefaultPersistBP = false;
                        break;
                    }
                }
                if (RelayHttpProxyHost == null) {
                    RequestHeaders.ProxyConnection = null;
                }
            }

            // Note: we do not remove fields mentioned in the
            //  'Connection' header (the specs say we should).
        }

        private Socket ConnectToSocket(String host, Int32 port) {
            return null;
        }

        /// <summary>
        ///     Pipeline step: tunnel the request from the client to the remove
        ///     server, and schedule the next step to be <c>ReadResponse</c>
        /// </summary>
        protected virtual void SendRequest() {
            // Transmit the request to the server

            RequestLine.SendTo(ServerSocket);
            RequestHeaders.SendTo(ServerSocket);
            if (State.bRequestHasMessage) {
                // Tunnel the request message
                if (State.bRequestMessageChunked) {
                    ClientHttpSocket.TunnelChunkedDataTo(ServerSocket);
                } else {
                    Debug.Assert(
                        State.RequestMessageLength > 0);
                    ClientHttpSocket.TunnelDataTo(TunnelPS, State.RequestMessageLength);
                }
            }

            State.NextStep = ReadResponse;
        }

        /// <summary>
        ///     Read first non empty line from the stream
        /// </summary>
        /// <param name="reader">used for reading</param>
        /// <returns>first non empty line</returns>
        protected String ReadFirstLine(StreamReader reader) {
            var firstLine = String.Empty;

            while (String.IsNullOrEmpty(firstLine)) {
                firstLine = reader.ReadLine();
            }

            return firstLine;
        }

        /// <summary>
        ///     Read stream line-by-line until empty line is met
        /// </summary>
        /// <param name="reader">used for reading</param>
        /// <returns>collection of read lines</returns>
        protected ICollection<String> ReadHeaders(StreamReader reader) {
            var headers = new List<String>();

            while (true) {
                var headerLine = reader.ReadLine();

                if (String.IsNullOrEmpty(headerLine)) {
                    break;
                }

                headers.Add(headerLine);
            }

            return headers;
        }

        /// <summary>
        ///     Pipeline step: read the HTTP response from the local client,
        ///     schedule the next step to be <c>SendResponse</c>, and call
        ///     <c>OnReceiveResponse</c>
        /// </summary>
        protected virtual void ReadResponse() {
            // Wait until we receive the response, then parse its header
            ResponseStatusLine = new HttpStatusLine(ServerSocket);
            ResponseHeaders = new HttpHeaders(ServerSocket);

            // Get bPersistConnectionPS (RFC 2616, section 14.10)
            bool bUseDefaultPersistPS = true;
            if (ResponseHeaders.Connection != null) {
                foreach (var item in ResponseHeaders.Connection) {
                    if (item.Equals("close")) {
                        State.bPersistConnectionPS = false;
                        bUseDefaultPersistPS = false;
                        break;
                    }
                    if (item.Equals("keep-alive")) {
                        State.bPersistConnectionPS = true;
                        bUseDefaultPersistPS = false;
                        break;
                    }
                }
            }
            if (bUseDefaultPersistPS) {
                State.bPersistConnectionPS =
                    (!ResponseStatusLine.HTTPVersion.Equals("1.0"));
            }

            if (State.bPersistConnectionPS) {
                ServerSocket.KeepAlive = true;
            } else {
                State.bPersistConnectionBP = false;
            }

            State.NextStep = SendResponse;
            OnReceiveResponse();
        }

        /// <summary>
        ///     Pipeline: tunnel the HTTP response from the remote server to the
        ///     local client, and end the request processing
        /// </summary>
        protected virtual void SendResponse() {
            if (!(ResponseHeaders.TransferEncoding == null &&
                  ResponseHeaders.ContentLength == null)) {
                // Transmit the response header to the client
                SendResponseStatusAndHeaders();
            }

            // Find out if there is a message body
            // (RFC 2616, section 4.4)
            int sc = ResponseStatusLine.StatusCode;
            if (RequestLine.Method.Equals("HEAD") ||
                sc == 204 || sc == 304 || (sc >= 100 && sc <= 199)) {
                SendResponseStatusAndHeaders();
                goto no_message_body;
            }

            bool bResponseMessageChunked = false;
            uint ResponseMessageLength = 0;
            if (ResponseHeaders.TransferEncoding != null) {
                bResponseMessageChunked = Array.IndexOf(
                    ResponseHeaders.TransferEncoding,
                    "chunked") >= 0;
                Debug.Assert(
                    bResponseMessageChunked);
            } else if (ResponseHeaders.ContentLength != null) {
                ResponseMessageLength =
                    (uint) ResponseHeaders.ContentLength;
                if (ResponseMessageLength == 0) {
                    goto no_message_body;
                }
            } else {
                // We really should have been given a response
                // length. It appears that some popular websites
                // send small files without a transfer-encoding
                // or length.

                // It seems that all of the browsers handle this
                // case so we need to as well.

                var buffer = new byte[512];
                ServerSocket.TunnelDataTo(ref buffer);

                // Transmit the response header to the client
                ResponseHeaders.ContentLength = (uint) buffer.Length;
                ResponseStatusLine.SendTo(ClientHttpSocket);
                ResponseHeaders.SendTo(ClientHttpSocket);

                ClientHttpSocket.TunnelDataTo(TunnelBP, buffer);
                State.NextStep = null;
                return;
            }

            if (State.OnResponseMessagePacket != null) {
                if (!State.bPersistConnectionPS) {
                    // Pipeline until the connection is closed
                    ServerSocket.TunnelDataTo(State.OnResponseMessagePacket);
                } else if (bResponseMessageChunked) {
                    ServerSocket.TunnelChunkedDataTo(
                        State.OnResponseMessagePacket);
                } else {
                    ServerSocket.TunnelDataTo(State.OnResponseMessagePacket,
                        ResponseMessageLength);
                }
                State.OnResponseMessagePacket(null, 0, 0);
            } else {
                if (!State.bPersistConnectionPS) {
                    // Pipeline until the connection is closed
                    ServerSocket.TunnelDataTo(TunnelBP);
                } else if (bResponseMessageChunked) {
                    ServerSocket.TunnelChunkedDataTo(ClientHttpSocket);
                } else {
                    ServerSocket.TunnelDataTo(TunnelBP, ResponseMessageLength);
                }
            }

            no_message_body:

            if (!State.bPersistConnectionPS && ServerSocket != null) {
                ServerSocket.CloseSocket();
                ServerSocket = null;
            }

            State.NextStep = null;
        }

        /// <summary>
        ///     Send the response status line and headers from the proxy to
        ///     the client
        /// </summary>
        protected void SendResponseStatusAndHeaders() {
            ResponseStatusLine.SendTo(ClientHttpSocket);
            ResponseHeaders.SendTo(ClientHttpSocket);
        }

        /// <summary>
        ///     Continuation delegate used in the request processing pipeline
        /// </summary>
        protected delegate void ProcessingStep();

        /// <summary>
        ///     Maintains the internal state for the request currently being
        ///     processed
        /// </summary>
        protected class RequestProcessingState {

            /// <summary>
            ///     Points to the next processing step; must be updated after
            ///     each processing step, setting it to null will stop the
            ///     processing
            /// </summary>
            public ProcessingStep NextStep;

            /// <summary>
            ///     When set to not null, will be called every time a raw fragment
            ///     of a non-empty response message body is received; note that the
            ///     packet handler becomes responsible for sending the response
            ///     (whatever it is) to ClientHttpSocket
            /// </summary>
            /// <remarks>
            ///     The message body might be compressed (or otherwise modified),
            ///     as specified by the Content-Encoding header. Applications
            ///     should use <c>ProxyLogic.GetResponseMessageStream</c> to
            ///     decompress (whenever necessary) the message stream.
            /// </remarks>
            public HttpSocket.MessagePacketHandler OnResponseMessagePacket;

            /// <summary>
            ///     Length of the request message, if any
            /// </summary>
            public uint RequestMessageLength;

            /// <summary>
            ///     Whether the BP connection should be kept alive after handling the current request, or closed
            /// </summary>
            public bool bPersistConnectionBP;

            /// <summary>
            ///     Whether the PS connection should be kept alive after handling the current request, or closed
            /// </summary>
            /// <remarks>
            ///     If set to false, then <c>bPersistConnectionBP</c> will also be
            ///     set to false, because if no Content-Length has been specified,
            ///     the browser would keep on waiting (BP kept-alive, but PS
            ///     closed).
            /// </remarks>
            public bool bPersistConnectionPS;

            /// <summary>
            ///     Whether the request contains a message
            /// </summary>
            public bool bRequestHasMessage;

            /// <summary>
            ///     Whether the request message (if any) is being transmitted in chunks
            /// </summary>
            public bool bRequestMessageChunked;

            /// <summary>
            ///     Set to true if no instruction was given in the request headers about whether the BP connection should persist
            /// </summary>
            public bool bUseDefaultPersistBP;

            /// <summary>
            ///     Processing state constructor
            /// </summary>
            /// <param name="StartStep">
            ///     First step of the request processing pipeline
            /// </param>
            public RequestProcessingState(ProcessingStep StartStep) {
                NextStep = StartStep;
            }

        };

    }

}