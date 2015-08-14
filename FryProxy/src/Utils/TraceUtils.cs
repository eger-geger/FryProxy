using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FryProxy.Headers;

namespace FryProxy.Utils
{
    public static class TraceUtils
    {
        public static void WriteHttpTrace(this StringBuilder stringBuilder, HttpMessageHeader messageHeader)
        {
            if (messageHeader == null)
            {
                return;
            }

            stringBuilder
                .AppendFormat("StartLine: {0}", messageHeader.StartLine)
                .AppendLine();

            var headers = messageHeader.Headers.Lines.ToList();

            if (headers.Count == 0)
            {
                return;
            }

            stringBuilder.AppendLine("Headers:");

            foreach (var header in headers)
            {
                stringBuilder
                    .AppendFormat("    {0}", header)
                    .AppendLine();
            }
        }

        public static String GetHttpTrace(HttpMessageHeader header)
        {
            if (header == null)
            {
                return String.Empty;
            }

            var sb = new StringBuilder();

            WriteHttpTrace(sb, header);

            return sb.ToString();
        }

    }
}
