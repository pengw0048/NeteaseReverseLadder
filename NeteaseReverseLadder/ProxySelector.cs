using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NeteaseReverseLadder
{
    class ProxySelector
    {
        public int ProxyTestTimeout = 5000;
        public int parallelism = 5;
        public class Proxy
        {
            public string host;
            public int port;
            public int latency;
            public bool valid;
            public override string ToString()
            {
                if (valid) return string.Format("{0}:{1} [{2}ms]", host, port, latency);
                else return string.Format("{0}:{1} [x]", host, port, latency);
            }
        }
        public List<Proxy> Proxies;
        public void UpdateList()
        {
            var newProxies = new List<Proxy>();
            var ret = "";
            using (var wc = new ImpatientWebClient())
                ret = wc.DownloadString("https://cn-proxy.com/");
                var trs = Regex.Matches(ret, "<tr>(.*?)<\\/tr>", RegexOptions.Singleline);
                foreach (Match tr in trs)
                {
                    var tds = Regex.Matches(tr.Groups[1].Value, "<td>(.*?)<\\/td>");
                if (tds.Count > 2)
                {
                    var host = tds[0].Groups[1].Value;
                    if (Regex.IsMatch(host, @"\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}"))
                    {
                        if (int.TryParse(tds[1].Groups[1].Value, out var port))
                        {
                            newProxies.Add(new Proxy() { host=host, port=port, latency = int.MaxValue });
                        }
                    }
                }
                        
                }
            lock (this)
                Proxies = newProxies;
        }
        public void UpdateLatency()
        {
            var newProxies = Proxies.Select(p => new Proxy { host = p.host, port = p.port }).ToList();
            var actions = new List<Action>();
            foreach (var proxy in newProxies)
            {
                actions.Add(() =>
                {
                    var latency = int.MaxValue;
                    try
                    {
                        using (var wc = new ImpatientWebClient(ProxyTestTimeout))
                        {
                            wc.Proxy = new WebProxy(proxy.host, proxy.port);
                            var sw = new Stopwatch();
                            sw.Start();
                            var ret = wc.DownloadString("https://music.163.com/about");
                            sw.Stop();
                            if (ret.Contains("music.126"))
                            {
                                latency = (int)sw.ElapsedMilliseconds;
                            }
                        }
                        lock (this)
                        {
                            proxy.latency = latency;
                            proxy.valid = latency != int.MaxValue;
                        }
                        Console.WriteLine("{0}: OK", proxy);
                    }
                    catch (Exception e) {
                        Console.WriteLine("{0}: {1}", proxy, e.Message);
                    }
                });
            }
            Parallel.Invoke(new ParallelOptions { MaxDegreeOfParallelism = parallelism }, actions.ToArray());
            newProxies = newProxies.Where(p => p.valid).OrderBy(p => p.latency).ToList();
            Console.WriteLine("Available proxies: ");
            newProxies.ForEach(p => Console.WriteLine(p));
            lock(this)
                Proxies = newProxies.Where(p => p.valid).OrderBy(p => p.latency).ToList();
        }
        public Proxy GetTop()
        {
            lock(this)
                return Proxies.Where(p => p.valid).OrderBy(p => p.latency).FirstOrDefault();
        }
        public void Remove(Proxy proxy)
        {
            lock(this)
                Proxies.Remove(proxy);
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
            var w = (HttpWebRequest)base.GetWebRequest(uri);
            w.Timeout = Timeout;
            w.ReadWriteTimeout = Timeout;
            return w;
        }
    }
}
