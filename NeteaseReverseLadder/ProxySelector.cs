using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace NeteaseReverseLadder
{
    class ProxySelector
    {
        public int ProxyTestTimeout = 5000;
        public class Proxy
        {
            public string host;
            public int port;
            public int latency;
            public bool valid;
            public override string ToString()
            {
                if (valid) return String.Format("{0}:{1} [{2}ms]", host, port, latency);
                else return String.Format("{0}:{1} [x]", host, port, latency);
            }
        }
        public List<Proxy> Proxies;
        public void UpdateProxyList()
        {
            var newProxies = new List<Proxy>();
            var ret = "";
            using (var wc = new ImpatientWebClient())
                ret = wc.DownloadString("http://cn-proxy.com/");
            var tables = Regex.Matches(ret, "<table class=\"sortable\">.+?<tbody>(.*?)<\\/tbody>", RegexOptions.Singleline);
            var first = true;
            foreach (Match mat in tables)
            {
                if (first)
                {
                    first = false;
                    continue;
                }
                var trs = Regex.Matches(mat.Groups[1].Value, "<tr>(.*?)<\\/tr>", RegexOptions.Singleline);
                foreach (Match tr in trs)
                {
                    var tds = Regex.Matches(tr.Groups[1].Value, "<td>(.*?)<\\/td>");
                    try
                    {
                        if (newProxies.Count < 10)
                        newProxies.Add(new Proxy() { host = tds[0].Groups[1].Value, port = int.Parse(tds[1].Groups[1].Value), latency = int.MaxValue });
                    }
                    catch (Exception) { }
                }
            }
            Proxies = newProxies;
        }
        public void UpdateLatency()
        {
            foreach (var proxy in Proxies)
            {
                var latency = int.MaxValue;
                try
                {
                    using (var wc = new ImpatientWebClient(ProxyTestTimeout))
                    {
                        wc.Proxy = new WebProxy(proxy.host, proxy.port);
                        var sw = new Stopwatch();
                        sw.Start();
                        var ret = wc.DownloadString("http://music.163.com/about");
                        sw.Stop();
                        if (ret.Contains("music.126"))
                        {
                            latency = (int)sw.ElapsedMilliseconds;
                        }
                    }
                    proxy.latency = latency;
                    proxy.valid = latency != int.MaxValue;
                }
                catch (Exception) { }
                Console.WriteLine(proxy);
            }
            Proxies = Proxies.OrderBy(o => o.latency).ToList();
        }
        public Proxy GetTopProxy()
        {
            return Proxies.FirstOrDefault();
        }
        public void RemoveTopProxy()
        {
            if (Proxies.Count > 0) Proxies.RemoveAt(0);
        }
    }
    class ImpatientWebClient : WebClient
    {
        private int Timeout;
        public ImpatientWebClient(int Timeout = 10000)
        {
            this.Timeout = Timeout;
        }
        protected override WebRequest GetWebRequest(Uri uri)
        {
            WebRequest w = base.GetWebRequest(uri);
            w.Timeout = Timeout;
            return w;
        }
    }
}
