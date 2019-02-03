using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Models;

namespace NeteaseReverseLadder
{
    class NeteaseProxy
    {
        private static string[] proxiedAddresses;
        private static string[] skipRequestHeaders = { "host", "content-length", "accept", "user-agent", "connection", "accept-encoding" };

        private ProxyServer proxyServer;
        public ProxySelector proxySelector;

        public NeteaseProxy(ProxySelector proxySelector)
        {
            proxyServer = new ProxyServer();
            this.proxySelector = proxySelector;
            proxiedAddresses = File.ReadLines("ProxyPaths.txt").Where(l => l.Length > 0).ToArray();
        }

        public void Start()
        {
            proxyServer.BeforeRequest += OnRequest;
            proxyServer.AddEndPoint(new ExplicitProxyEndPoint(IPAddress.Any, 15213, true));
            proxyServer.Start();

            foreach (var endPoint in proxyServer.ProxyEndPoints)
                Console.WriteLine("Started proxy on IP {0} and port {1}", endPoint.IpAddress, endPoint.Port);
        }

        public void Stop()
        {
            proxyServer.BeforeRequest -= OnRequest;
            proxyServer.Stop();
        }

        public async Task OnRequest(object sender, SessionEventArgs e)
        {
            if (proxiedAddresses.Any(str => e.HttpClient.Request.Url.Contains(str)))
            {
                Console.WriteLine("Retrieving via proxy: " + e.HttpClient.Request.Url);
                var proxy = proxySelector.GetTop();
                var st = new Stopwatch();
                st.Start();
                try
                {
                    using (var wc = new ImpatientWebClient())
                    {
                        wc.Proxy = new WebProxy(proxy.host, proxy.port);
                        foreach (var aheader in e.HttpClient.Request.Headers)
                        {
                            var str = aheader.Name.ToLower();
                            if (skipRequestHeaders.Contains(str)) continue;
                            wc.Headers.Add(aheader.Name, aheader.Value);
                        }
                        var body = wc.UploadData(e.HttpClient.Request.Url, await e.GetRequestBody());
                        var headers = new Dictionary<string, HttpHeader>();
                        foreach (var key in wc.ResponseHeaders.AllKeys)
                        {
                            headers.Add(key, new HttpHeader(key, wc.ResponseHeaders[key]));
                        }
                        e.Ok(body, headers);
                    }
                    st.Stop();
                    Console.WriteLine("Finished in " + st.ElapsedMilliseconds + " ms");
                }
                catch (Exception ex) { Console.WriteLine(ex); }
            }
        }
    }
}
