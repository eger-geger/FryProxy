using System;
using System.Collections.Generic;
using System.IO;
using FryProxy.Headers;

namespace FryProxy.Readers
{
    /// <summary>
    ///     Read HTTP message entities from underlying reader
    /// </summary>
    public class HttpHeaderReader {

        private readonly TextReader _reader;

        /// <summary>
        ///     Create new instance of <see cref="HttpHeaderReader"/> 
        /// </summary>
        /// <param name="reader">used for actual reading</param>
        public HttpHeaderReader(TextReader reader)
        {
            _reader = reader;
        }

        /// <summary>
        ///     Read next not empty line.
        ///     Can be used for reading request line and status line from HTTP messages.
        ///     Also can be used for reading chunk length of chunked message body.
        /// </summary>
        /// <returns>firts not empty line</returns>
        public String ReadFirstLine()
        {
            var firstLine = String.Empty;

            while (String.IsNullOrWhiteSpace(firstLine))
            {
                firstLine = _reader.ReadLine();
            }

            return firstLine;
        }

        /// <summary>
        ///     Read HTTP headers from underlying <see cref="TextReader"/>
        /// </summary>
        /// <returns>raw header lines</returns>
        public IList<String> ReadHeaders()
        {
            var headers = new List<String>();

            for (var nextLine = _reader.ReadLine();
                !String.IsNullOrEmpty(nextLine);
                nextLine = _reader.ReadLine())
            {
                headers.Add(nextLine);
            }

            return headers;
        }

        /// <summary>
        ///     Read <see cref="HttpMessageHeader"/> from underlying reader.
        /// </summary>
        /// <returns>HTTP message header</returns>
        public HttpMessageHeader ReadHttpMessageHeader()
        {
            return new HttpMessageHeader(ReadFirstLine())
            {
                Headers = new HttpHeaders(ReadHeaders())
            };
        }

    }
}
