using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using CommandLine;
using log4net.Config;

namespace FryProxy.ConsoleApp
{
    public class Program
    {
        private const String CertificateFileName = "fry.pfx";

        private const String CertificatePassword = "fry";

        private const String StopCommad = "stop";

        private static X509Certificate2 Certificate
        {
            get
            {
                Stream certificateStream = Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream(typeof (Program), CertificateFileName);

                if (certificateStream == null)
                {
                    throw new InvalidOperationException("Failed to load SSL certificate");
                }

                var certificateBytes = new byte[certificateStream.Length];

                certificateStream.Read(certificateBytes, 0, certificateBytes.Length);

                return new X509Certificate2(certificateBytes, CertificatePassword);
            }
        }

        public static void Main(String[] args)
        {
            XmlConfigurator.Configure();

            var options = new CommandlineOptions();

            if (!Parser.Default.ParseArguments(args, options))
            {
                return;
            }

            try
            {
                var httpProxyServer = options.HttpPort == 0 
                    ? new HttpProxyServer(options.Host, new HttpProxy())
                    : new HttpProxyServer(options.Host, options.HttpPort, new HttpProxy());

                var sslProxyServer = options.SslPort == 0
                    ? new HttpProxyServer(options.Host, new SslProxy(Certificate))
                    : new HttpProxyServer(options.Host, options.SslPort, new SslProxy(Certificate));

                WaitHandle.WaitAll(new[]
                {
                    httpProxyServer.Start(),
                    sslProxyServer.Start()
                });

                Console.WriteLine("Started HTTP proxy on {0}", httpProxyServer.ProxyEndPoint);
                Console.WriteLine("Started SSL proxy on {0}", sslProxyServer.ProxyEndPoint);
                Console.WriteLine("Type 'stop' in order to shutdown");

                while (Console.ReadLine() != StopCommad)
                {
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                }

                Console.WriteLine("shutting down...");

                httpProxyServer.Stop();
                sslProxyServer.Stop();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
}