using System;
using System.Diagnostics.Contracts;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

using FryProxy.Headers;

namespace FryProxy.Utility {

    public static class EndPointResolver {

        private static readonly Regex HostAndPortRegex = new Regex(@"(?<host>\w+):(?<port>\d+)");

        public static DnsEndPoint ResolveRequestEndPoint(this HttpRequestHeaders headers, Int32 defaultPort) {
            var hostFromHeaders = headers.RequestHeaders.Host;

            return !String.IsNullOrEmpty(hostFromHeaders)
                ? ResolveHostEndPoint(hostFromHeaders, defaultPort)
                : ResolveURIEndPoint(headers.RequestURI);
        }

        public static DnsEndPoint ResolveURIEndPoint(String uri) {
            Uri parsedUri;

            if (Uri.TryCreate(uri, UriKind.Absolute, out parsedUri)) {
                return new DnsEndPoint(parsedUri.Host, parsedUri.Port, AddressFamily.InterNetwork);
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

            return new DnsEndPoint(host, defaultPort, AddressFamily.InterNetwork);
        }

    }

}