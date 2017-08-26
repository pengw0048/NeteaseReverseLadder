using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Models;

namespace NeteaseReverseLadder
{
    class NeteaseProxy
    {
        private static string[] toFetch = { "music.163.com/eapi/v1/playlist/manipulate/tracks", "music.163.com/eapi/song/enhance", "music.163.com/eapi/song/like" };

        private ProxyServer proxyServer;
        public ProxySelector proxySelector;

        public NeteaseProxy(ProxySelector proxySelector)
        {
            proxyServer = new ProxyServer
            {
                TrustRootCertificate = true
            };
            this.proxySelector = proxySelector;
        }

        public void Start()
        {
            proxyServer.BeforeRequest += OnRequest;
            proxyServer.BeforeResponse += OnResponse;
            proxyServer.AddEndPoint(new ExplicitProxyEndPoint(IPAddress.Any, 15213, true));
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
            if (toFetch.Any(str => e.WebSession.Request.Url.Contains(str)))
            {
                Console.WriteLine("从代理服务器获取：" + e.WebSession.Request.Url);
                var proxy = proxySelector.GetTop();
                var st = new Stopwatch();
                st.Start();
                try
                {
                    using (var wc = new ImpatientWebClient())
                    {
                        wc.Proxy = new WebProxy(proxy.host, proxy.port);
                        foreach (var aheader in e.WebSession.Request.RequestHeaders)
                        {
                            var str = aheader.Name.ToLower();
                            if (str == "host" || str == "content-length" || str == "accept" || str == "user-agent" || str == "connection") continue;
                            wc.Headers.Add(aheader.Name, aheader.Value);
                        }
                        var body = wc.UploadData(e.WebSession.Request.Url, await e.GetRequestBody());
                        try
                        {
                            // HACK: WebClient似乎在服务器返回Chunked + GZip的时候，只能读出Unchunked的结果，不会自动UnGZip。
                            // 正确的解决方法是让WebClient做两种解码。暂时没有找到方法。
                            // 尝试手动解GZip。这对网易云音乐应该暂时可以得到正确的结果，但一般来说不是最好的解决方法。
                            // （例如，服务器恰巧返回了一个gzip文件，那么我们不应该解压它）
                            body = Decompress(body);
                        }
                        catch (Exception) { }
                        var headers = new Dictionary<string, HttpHeader>();
                        foreach (var key in wc.ResponseHeaders.AllKeys)
                        {
                            headers.Add(key, new HttpHeader(key, wc.ResponseHeaders[key]));
                        }
                        await e.Ok(body, headers);
                    }
                    st.Stop();
                    Console.WriteLine("修改完成，用时 " + st.ElapsedMilliseconds + " ms");
                }
                catch (Exception ex) { Console.WriteLine(ex); }
            }
        }

        public async Task OnResponse(object sender, SessionEventArgs e)
        {
            var contentType = e.WebSession.Response.ContentType;
            if ((contentType.Contains("text") || contentType.Contains("json")) && e.WebSession.Request.Url.Contains("music.163.com/eapi/"))
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

        static byte[] Decompress(byte[] data)
        {
            using (var compressedStream = new MemoryStream(data))
            using (var zipStream = new GZipStream(compressedStream, CompressionMode.Decompress))
            using (var resultStream = new MemoryStream())
            {
                zipStream.CopyTo(resultStream);
                return resultStream.ToArray();
            }
        }
    }
}
