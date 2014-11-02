using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;

namespace FryProxy.Headers {

    public class HttpHeadersCollection {

        public const String HeaderValueSeparator = ",";

        private static readonly char[] HeaderNameValueSeparator = {
            ':'
        };

        private readonly List<KeyValuePair<String, String>> _headers;

        public HttpHeadersCollection() {
            _headers = new List<KeyValuePair<String, String>>();
        }

        /// <summary>
        ///     Create new headers from given strings
        /// </summary>
        /// <param name="headers">
        ///     HTTP message headers as {name} : {value} strings
        /// </param>
        public HttpHeadersCollection(IEnumerable<String> headers) : this() {
            Contract.Requires<ArgumentNullException>(headers != null, "headers");

            headers.Where(str => !String.IsNullOrEmpty(str))
                .Select(ParseHeaderLine)
                .ToList()
                .ForEach(Add);
        }

        public String this[String name] {
            get { return Contains(name) ? String.Join(HeaderValueSeparator, _headers.Where(h => h.Key == name).Select(h => h.Value)) : null; }

            set {
                var newHeader = new KeyValuePair<String, String>(name, value);

                var currentHeaders = _headers
                    .Where(h => h.Key == name)
                    .Select((h, i) => Tuple.Create(i, h))
                    .ToList();

                if (currentHeaders.Count > 0) {
                    _headers[currentHeaders.First().Item1] = newHeader;
                    currentHeaders.ForEach(t => _headers.Remove(t.Item2));
                } else {
                    _headers.Add(newHeader);
                }
            }
        }

        public IEnumerable<KeyValuePair<String, String>> Pairs {
            get { return _headers.AsReadOnly(); }
        }

        public IEnumerable<String> Lines {
            get { return _headers.Select(FormatHeader); }
        }

        private static KeyValuePair<String, String> ParseHeaderLine(String headerLine) {
            Contract.Requires<ArgumentNullException>(headerLine != null, "headerLine");
            Contract.Requires<ArgumentException>(!String.IsNullOrWhiteSpace(headerLine), "headerLine");

            var header = headerLine.Split(HeaderNameValueSeparator, 2, StringSplitOptions.RemoveEmptyEntries);

            if (header.Length < 2 || String.IsNullOrWhiteSpace(header[0]) || String.IsNullOrWhiteSpace(header[1])) {
                throw new ArgumentException(String.Format("Invalid header: [{0}]", headerLine), "headerLine");
            }

            return new KeyValuePair<String, String>(header[0].Trim(), header[1].Trim());
        }

        private static String FormatHeader(KeyValuePair<String, String> header) {
            return String.Format("{0}: {1}", header.Key, header.Value);
        }

        public void Add(KeyValuePair<String, String> header) {
            _headers.Add(header);
        }

        public void Add(String name, String value) {
            Add(new KeyValuePair<String, String>(name, value));
        }

        public Boolean Remove(String key) {
            return _headers.Remove(_headers.Find(h => h.Key == key));
        }

        public Int32 RemoveAll(String name) {
            return _headers.RemoveAll(h => h.Key == name);
        }

        public Boolean Remove(KeyValuePair<String, String> header) {
            return _headers.Remove(header);
        }

        public Boolean Contains(String key) {
            return _headers.Any(h => h.Key == key);
        }

        public override String ToString() {
            return String.Join("\n", Lines);
        }

    }

}