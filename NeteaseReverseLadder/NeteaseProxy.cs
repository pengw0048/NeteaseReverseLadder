using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private ProxyServer proxyServer;
        public ProxySelector proxySelector;
        private bool DEBUG = true;

        List<string> urlToBlock = new List<string>(new string[] { "x.jd.com" });

        List<string> urlToProxy = new List<string>(new string[] { "rcmusic2/fcgi-bin/fcg_guess_youlike_pc.fcg" });

        List<string> urlToModify = new List<string>(new string[] { "qqmusic/fcgi-bin/qm_rplstingmus.fcg", "qqmusic/fcgi-bin/update_songinfo.fcg",
            "soso/fcgi-bin/client_search_cp", "node/pc/wk_v15/singer_detail.html", "v8/fcg-bin/fcg_v8_album_detail_cp.fcg", "vipdown/fcgi-bin/fcg_3g_song_list_rover.fcg",
            "qzone/fcg-bin/fcg_ucc_getcdinfo_byids_cp.fcg", "musichall/fcgi-bin/fcg_action_ctrl"});

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
            if (e.WebSession.Request.Url.Contains("music.163.com/eapi/song/enhance") || e.WebSession.Request.Url.Contains("music.163.com/eapi/song/like"))
            {
                Console.WriteLine("从代理服务器获取：" + e.WebSession.Request.Url);
                var proxy = proxySelector.GetTop();
                var st = new Stopwatch();
                st.Start();
                try
                {
                    byte[] ret = null;
                    using (var wc = new ImpatientWebClient())
                    {
                        wc.Proxy = new WebProxy(proxy.host, proxy.port);
                        foreach (var aheader in e.WebSession.Request.RequestHeaders)
                        {
                            var str = aheader.Name.ToLower();
                            if (str == "host" || str == "content-length" || str == "accept" || str == "user-agent" || str == "connection") continue;
                            wc.Headers.Add(aheader.Name, aheader.Value);
                        }
                        ret = wc.UploadData(e.WebSession.Request.Url.Replace("https://", "http://"), await e.GetRequestBody());
                    }
                    st.Stop();
                    await e.Ok(ret);
                    Console.WriteLine("修改完成，用时 " + st.ElapsedMilliseconds + " ms");
                }
                catch (Exception ex) { Console.WriteLine(ex); }
            }
        }

        public async Task OnResponse(object sender, SessionEventArgs e)
        {
            var contentType = e.WebSession.Response.ContentType.ToLower();
            if ((contentType.Contains("text") || contentType.Contains("json") || contentType.Contains("javascript")))
            {
                string url = e.WebSession.Request.Url;
                // Netease Music
                if (url.Contains("music.163.com/eapi/"))
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
                // QQ Music
                else if (TestURL(url, urlToModify))
                {
                    string body = await e.GetResponseBodyAsString();
                    body = body.Replace("msgid=\"23\"", "msgid=\"0\""); // message dialog id that will prompt if you click "play"
                    //body = body.Replace("alert=\"0\"", "alert=\"2\"");
                    body = body.Replace("switch=\"1\"", "switch=\"3749695\""); // this is some random number
                    body = body.Replace("\"msgid\":23", "\"msgid\":0");
                    //body = body.Replace("\"alert\":0", "\"alert\":2");
                    body = body.Replace("\"switch\":1", "\"switch\":3749695");
                    body = body.Replace("\"msg\":23", "\"msg\":0");
                    body = body.Replace("\"payStatus\":0", "\"payStatus\":1");
                    body = body.Replace("\"pay_status\":0", "\"pay_status\":1");
                    body = body.Replace("\"status\":0", "\"status\":1");
                    await e.SetResponseBodyString(body);
                    Console.WriteLine("修改成功：" + e.WebSession.Request.Url);
                    if(DEBUG)
                        if (url.Contains("vipdown/fcgi-bin/fcg_3g_song_list_rover.fcg"))
                        {
                            Console.WriteLine(body);
                        }
                }

            }
        }

        private bool TestURL(string url, List<string> urls)
        {
            foreach (string next_url in urls)
            {
                if (url.Contains(next_url))
                    return true;
            }
            return false;
        }
    }
}
