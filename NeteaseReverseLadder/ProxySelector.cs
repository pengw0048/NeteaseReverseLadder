using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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
            public override string ToString()
            {
                if (latency == int.MaxValue) return host + ":" + port + " [x]";
                else return host + ":" + port + " [" + latency + " ms]";
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
            foreach (Match mat in tables)
            {
                var trs = Regex.Matches(mat.Groups[1].Value, "<tr>(.*?)<\\/tr>", RegexOptions.Singleline);
                foreach (Match tr in trs)
                {
                    var tds = Regex.Matches(tr.Groups[1].Value, "<td>(.*?)<\\/td>");
                    try
                    {
                        if(newProxies.Count <= 15)
                        newProxies.Add(new Proxy() { host = tds[0].Groups[1].Value, port = int.Parse(tds[1].Groups[1].Value), latency = int.MaxValue });
                    }
                    catch (Exception) { }
                }
            }
            lock (this)
            {
                Proxies = newProxies;
            }
        }
        public void UpdateLatency()
        {
            var tasks = new List<Task>();
            List<Proxy> newProxies;
            lock (this)
            {
                newProxies = Proxies.Select(item => new Proxy { host = item.host, port = item.port, latency = item.latency }).ToList();
            }
            foreach (var proxy in newProxies)
            {
                tasks.Add(Task.Factory.StartNew(() =>
                {
                    var latency = int.MaxValue;
                    try
                    {
                        using (var wc = new ImpatientWebClient())
                        {
                            wc.Timeout = ProxyTestTimeout;
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
                    }
                    catch (Exception) { }
                }));
            }
            Task.WaitAll(tasks.ToArray(), ProxyTestTimeout);
            newProxies = newProxies.OrderBy(o => o.latency).ToList();
            lock (this)
            {
                Proxies = newProxies;
            }
        }
        public List<Proxy> GetTopProxies(int MaxCount = 3)
        {
            var ret = new List<Proxy>();
            lock (this)
            {
                Proxies.ForEach(o => { if (ret.Count < MaxCount && o.latency != int.MaxValue) ret.Add(o); });
            }
            return ret;
        }
        public void RemoveTopProxy()
        {
            lock (this)
            {
                while (Proxies.Count > 0 && Proxies[0].latency==int.MaxValue) Proxies.RemoveAt(0);
                if (Proxies.Count > 0) Proxies.RemoveAt(0);
            }
        }
    }
    class ImpatientWebClient : WebClient
    {
        public int Timeout = 10000;
        protected override WebRequest GetWebRequest(Uri uri)
        {
            WebRequest w = base.GetWebRequest(uri);
            w.Timeout = Timeout;
            return w;
        }
    }
}
