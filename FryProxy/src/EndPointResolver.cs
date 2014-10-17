using System;
using System.Diagnostics.Contracts;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

using FryProxy.HttpMessage;

namespace FryProxy {

    public static class EndPointResolver {

        private static readonly Regex HostAndPortRegex = new Regex(@"(?<host>\w+):(?<port>\d+)");

        public static DnsEndPoint ResolveRequestEndPoint(this RequestMessage message, Int32 defaultPort) {
            var hostFromHeaders = message.RequestHeaders.Host;

            return !String.IsNullOrEmpty(hostFromHeaders)
                ? ResolveHostEndPoint(hostFromHeaders, defaultPort)
                : ResolveURIEndPoint(message.RequestURI);
        }

        public static DnsEndPoint ResolveURIEndPoint(String uri) {
            Uri parsedUri;

            if (Uri.TryCreate(uri, UriKind.Absolute, out parsedUri)) {
                return new DnsEndPoint(parsedUri.Host, parsedUri.Port);
            }

            throw new ArgumentException(String.Format("Cannot resolve endpoint from: {0}", uri), "uri");
        }

        public static DnsEndPoint ResolveHostEndPoint(String host, Int32 defaultPort) {
            Contract.Requires<ArgumentNullException>(!String.IsNullOrWhiteSpace(host), "host");
            Contract.Requires<ArgumentOutOfRangeException>(defaultPort > IPEndPoint.MinPort && defaultPort < IPEndPoint.MaxPort, "defaultPort");

            var hostAndPortMatch = HostAndPortRegex.Match(host);

            if (hostAndPortMatch.Success) {
                return new DnsEndPoint(
                    hostAndPortMatch.Groups["host"].Value,
                    Int32.Parse(hostAndPortMatch.Groups["port"].Value),
                    AddressFamily.InterNetwork
                    );
            }

            return new DnsEndPoint(host, defaultPort);
        }

    }

}