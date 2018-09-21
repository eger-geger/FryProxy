using System;
using System.Collections.Generic;
using System.Linq;

namespace FryProxy.Headers
{
    /// <summary>
    ///     Collection of HTTP headers
    /// </summary>
    public class HttpHeaders
    {
        private const String HeaderValueSeparator = ",";

        private const Char HeaderNameSeparator = ':';

        private static readonly Char[] HeaderNameSeparatorArray =
        {
            HeaderNameSeparator
        };

        private readonly List<KeyValuePair<String, String>> _headers;

        /// <summary>
        ///     Create new instance of HTTP header collection
        /// </summary>
        public HttpHeaders()
        {
            _headers = new List<KeyValuePair<String, String>>();
        }

        /// <summary>
        ///     Create new headers from given strings
        /// </summary>
        /// <param name="headers">
        ///     HTTP message headers as {name} : {value} strings
        /// </param>
        public HttpHeaders(IEnumerable<String> headers) : this()
        {
            if (headers == null)
            {
                throw new ArgumentNullException(nameof(headers));
            }

            headers.Where(str => !string.IsNullOrEmpty(str))
                .Select(ParseHeaderLine)
                .ToList()
                .ForEach(Add);
        }

        /// <summary>
        ///     Add or update header to collection
        /// </summary>
        /// <param name="name">header name</param>
        /// <returns>header value</returns>
        public String this[String name]
        {
            get
            {
                return Contains(name)
                    ? String.Join(HeaderValueSeparator, _headers.Where(h => h.Key == name).Select(h => h.Value))
                    : null;
            }

            set
            {
                var newHeader = new KeyValuePair<String, String>(name, value);

                int existingHeaderIndex = _headers.FindIndex(h => h.Key == name);

                if (existingHeaderIndex != -1)
                {
                    _headers[existingHeaderIndex] = newHeader;
                }
                else
                {
                    _headers.Add(newHeader);
                }
            }
        }

        public IEnumerable<KeyValuePair<String, String>> Pairs
        {
            get { return _headers.AsReadOnly(); }
        }

        public IEnumerable<String> Lines
        {
            get { return _headers.Select(FormatHeader); }
        }

        private static KeyValuePair<String, String> ParseHeaderLine(String headerLine)
        {
            if (string.IsNullOrEmpty(headerLine))
            {
                throw new ArgumentException(nameof(headerLine));
            }

            string[] header = headerLine.Split(HeaderNameSeparatorArray, 2, StringSplitOptions.None);

            if (header.Length < 2 || String.IsNullOrWhiteSpace(header[0]))
            {
                throw new ArgumentException(String.Format("Invalid header: [{0}]", headerLine), "headerLine");
            }

            return new KeyValuePair<String, String>(header[0].Trim(), header[1].Trim());
        }

        private static String FormatHeader(KeyValuePair<String, String> header)
        {
            return header.Key + HeaderNameSeparator + header.Value;
        }

        public void Add(KeyValuePair<String, String> header)
        {
            _headers.Add(header);
        }

        public void Add(String name, String value)
        {
            Add(new KeyValuePair<String, String>(name, value));
        }

        public Boolean Remove(String key)
        {
            return _headers.Remove(_headers.Find(h => h.Key == key));
        }

        public Int32 RemoveAll(String name)
        {
            return _headers.RemoveAll(h => h.Key == name);
        }

        public Boolean Remove(KeyValuePair<String, String> header)
        {
            return _headers.Remove(header);
        }

        public Boolean Contains(String key)
        {
            return _headers.Any(h => h.Key == key);
        }

        public override String ToString()
        {
            return String.Join("\n", Lines);
        }
    }
}