using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NeteaseReverseLadder
{
    class Program
    {
        static void Main(string[] args)
        {
            var ps = new ProxySelector();
            while (true)
            {
                if (!UpdateProxySelector(ps)) Console.WriteLine("获取代理列表失败，10秒后重试");
                else break;
            }
            var proxy = new NeteaseProxy(ps);
            proxy.StartProxy();
            Console.WriteLine("请设置网易云音乐代理为127.0.0.1，端口15213");
            while (true) Console.ReadLine();
        }
        static bool UpdateProxySelector(ProxySelector ps)
        {
            Console.WriteLine("获取代理列表");
            ps.UpdateProxyList();
            Console.WriteLine("共" + ps.Proxies.Count + "条结果，测试速度");
            ps.UpdateLatency();
            ps.Proxies.ForEach(o => { if (o.latency != int.MaxValue) Console.WriteLine(o); });
            return ps.Proxies.Count >= 1 && ps.Proxies[0].latency != int.MaxValue;
        }
    }
}
