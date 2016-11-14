using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Models;

namespace NeteaseReverseLadder
{
    class NeteaseProxy
    {
        private ProxyServer proxyServer;
        public ProxySelector ps;

        public NeteaseProxy(ProxySelector ps)
        {
            proxyServer = new ProxyServer();
            proxyServer.TrustRootCertificate = true;
            this.ps = ps;
        }

        public void StartProxy()
        {
            proxyServer.BeforeRequest += OnRequest;
            proxyServer.BeforeResponse += OnResponse;

            var explicitEndPoint = new ExplicitProxyEndPoint(IPAddress.Any, 15213, true)
            {
                // ExcludedHttpsHostNameRegex = new List<string>() { "google.com", "dropbox.com" }
            };

            proxyServer.AddEndPoint(explicitEndPoint);
            proxyServer.Start();

            foreach (var endPoint in proxyServer.ProxyEndPoints)
                Console.WriteLine("在 IP {0} 和端口 {1} 上开启代理服务器", endPoint.IpAddress, endPoint.Port);
        }

        public void Stop()
        {
            proxyServer.BeforeRequest -= OnRequest;
            proxyServer.BeforeResponse -= OnResponse;

            proxyServer.Stop();
        }

        //intecept & cancel, redirect or update requests
        public async Task OnRequest(object sender, SessionEventArgs e)
        {
            if (e.WebSession.Request.Url.StartsWith("http://music.163.com/eapi/song/enhance/player"))
            {
                body = await e.GetRequestBody();
                head = e.WebSession.Request.RequestHeaders;
            }

        }
        private byte[] body;
        private Dictionary<string, HttpHeader> head;
        //Modify response
        public async Task OnResponse(object sender, SessionEventArgs e)
        {
            //read response headers
            var responseHeaders = e.WebSession.Response.ResponseHeaders;
            if ((e.WebSession.Request.Method == "GET" || e.WebSession.Request.Method == "POST") && e.WebSession.Response.ResponseStatusCode == "200")
            {
                if (e.WebSession.Response.ContentType != null && (e.WebSession.Response.ContentType.Trim().ToLower().Contains("text") || e.WebSession.Response.ContentType.Trim().ToLower().Contains("json")) || e.WebSession.Request.Url.StartsWith("http://music.163.com/eapi/song/enhance/player/url"))
                {
                    if (e.WebSession.Request.Url.StartsWith("http://music.163.com/eapi/song/enhance/player/url"))
                    {
                        Console.WriteLine("从代理服务器获取歌曲地址");
                        var proxies = ps.GetTopProxies(1);
                        var tasks = new List<Task<byte[]>>();
                        var st = new Stopwatch();
                        st.Start();
                        foreach (var proxy in proxies)
                        {
                            tasks.Add(Task<byte[]>.Factory.StartNew(() =>
                            {
                                try
                                {
                                    using (var wc = new ImpatientWebClient())
                                    {
                                        wc.Proxy = new WebProxy(proxy.host, proxy.port);
                                        foreach (var aheader in head)
                                        {
                                            var str = aheader.Key.ToLower();
                                            if (str == "host" || str == "content-length" || str == "accept" || str == "user-agent" || str == "connection") continue;
                                            wc.Headers.Add(aheader.Key, aheader.Value.Value);
                                        }
                                        var ret = wc.UploadData(e.WebSession.Request.Url, body);
                                        return ret;
                                    }
                                }
                                catch (Exception) { }
                                return new byte[0];
                            }));
                        }
                        var idx = Task.WaitAny(tasks.ToArray());
                        st.Stop();
                        await e.SetResponseBody(tasks[idx].Result);
                        Console.WriteLine("修改完成，用时 " + st.ElapsedMilliseconds + " ms");
                    }
                    else if (e.WebSession.Request.Url.StartsWith("http://music.163.com/eapi/"))
                    {
                        var body = await e.GetResponseBodyAsString();
                        if (Regex.Match(body, "\"st\":-\\d+").Success)
                        {
                            Console.WriteLine("替换歌曲列表信息");
                            body = Regex.Replace(body, "\"st\":-\\d+", "\"st\":0");
                            body = body.Replace("\"pl\":0", "\"pl\":320000");
                            body = body.Replace("\"dl\":0", "\"dl\":320000");
                            await e.SetResponseBodyString(body);
                        }
                    }
                }
            }
        }
    }
}
