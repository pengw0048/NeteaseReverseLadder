using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;
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
            proxyServer = new ProxyServer
            {
                TrustRootCertificate = true
            };
            this.ps = ps;
            var timer = new Timer();
            timer.Elapsed += new ElapsedEventHandler(OnTimedEvent);
            timer.Interval = 5 * 60 * 1000;
            timer.Enabled = true;
        }

        private void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            lock(this)
                foreach (var pair in requests)
                {
                    if ((DateTime.Now - pair.Value.time).TotalSeconds > 30)
                        requests.Remove(pair.Key);
                }
        }

        public void StartProxy()
        {
            proxyServer.BeforeRequest += OnRequest;
            proxyServer.BeforeResponse += OnResponse;

            var explicitEndPoint = new ExplicitProxyEndPoint(IPAddress.Any, 15213, true);

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
        
        public async Task OnRequest(object sender, SessionEventArgs e)
        {
            if (e.WebSession.Request.Url.Contains("music.163.com/eapi/song/enhance") || e.WebSession.Request.Url.Contains("music.163.com/eapi/song/like"))
            {
                var request = new RequestInfo() { body = await e.GetRequestBody(), head = e.WebSession.Request.RequestHeaders, time = DateTime.Now };
                lock (this)
                    requests.Add(e.WebSession.RequestId, request);
            }

        }
        class RequestInfo
        {
            public byte[] body;
            public Dictionary<string, HttpHeader> head;
            public DateTime time;
        }
        private Dictionary<Guid, RequestInfo> requests = new Dictionary<Guid, RequestInfo>();
        //Modify response
        public async Task OnResponse(object sender, SessionEventArgs e)
        {
            //read response headers
            var responseHeaders = e.WebSession.Response.ResponseHeaders;
            if ((e.WebSession.Request.Method == "GET" || e.WebSession.Request.Method == "POST") && e.WebSession.Response.ResponseStatusCode == "200")
            {
                if (e.WebSession.Response.ContentType != null && (e.WebSession.Response.ContentType.Trim().ToLower().Contains("text") || e.WebSession.Response.ContentType.Trim().ToLower().Contains("json")) || e.WebSession.Request.Url.Contains("music.163.com/eapi/song"))
                {
                    if (e.WebSession.Request.Url.Contains("music.163.com/eapi/song/enhance") || e.WebSession.Request.Url.Contains("music.163.com/eapi/song/like"))
                    {
                        Console.WriteLine("从代理服务器获取：" + e.WebSession.Request.Url);
                        var proxy = ps.GetTopProxy();
                        var st = new Stopwatch();
                        st.Start();
                        byte[] ret = null;
                        try
                        {
                            using (var wc = new ImpatientWebClient())
                            {
                                RequestInfo request;
                                lock(this)
                                    request = requests[e.WebSession.RequestId];
                                requests.Remove(e.WebSession.RequestId);
                                wc.Proxy = new WebProxy(proxy.host, proxy.port);
                                foreach (var aheader in request.head)
                                {
                                    var str = aheader.Key.ToLower();
                                    if (str == "host" || str == "content-length" || str == "accept" || str == "user-agent" || str == "connection") continue;
                                    wc.Headers.Add(aheader.Key, aheader.Value.Value);
                                }
                                ret = wc.UploadData(e.WebSession.Request.Url.Replace("https://", "http://"), request.body);
                            }
                            st.Stop();
                            await e.SetResponseBody(ret);
                            Console.WriteLine("修改完成，用时 " + st.ElapsedMilliseconds + " ms");
                            lock (this)
                                requests.Remove(e.WebSession.RequestId);
                        }
                        catch (Exception ex) { Console.WriteLine(ex); }
                    }
                    else if (e.WebSession.Request.Url.Contains("music.163.com/eapi/"))
                    {
                        var body = await e.GetResponseBodyAsString();
                        if (Regex.Match(body, "\"st\":-\\d+").Success)
                        {
                            Console.WriteLine("替换歌曲列表信息");
                            body = Regex.Replace(body, "\"st\":-\\d+", "\"st\":0");
                            body = body.Replace("\"pl\":0", "\"pl\":320000");
                            body = body.Replace("\"dl\":0", "\"dl\":320000");
                            body = body.Replace("\"fl\":0", "\"fl\":320000");
                            body = body.Replace("\"sp\":0", "\"sp\":7");
                            body = body.Replace("\"cp\":0", "\"cp\":1");
                            body = body.Replace("\"subp\":0", "\"subp\":1");
                            await e.SetResponseBodyString(body);
                        }
                    }
                }
            }
        }
    }
}
