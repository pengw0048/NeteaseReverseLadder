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
            start:
            var ps = new ProxySelector();
            while (true)
            {
                if (!UpdateProxySelector(ps)) Console.WriteLine("获取代理列表失败，正在重试");
                else break;
                ps.ProxyTestTimeout += 5000;
            }
            var proxy = new NeteaseProxy(ps);
            proxy.StartProxy();
            Console.WriteLine("请设置网易云音乐代理为127.0.0.1，端口15213");
            Console.WriteLine("如果播放失败，按回车切换到下一个代理服务器");
            while (true)
            {
                var aproxy = ps.GetTopProxies(1);
                if (aproxy.Count == 0)
                {
                    Console.WriteLine("没有可用代理，重新搜索");
                    proxy.Stop();
                    goto start;
                }
                Console.WriteLine("现在使用的是：" + aproxy[0]);
                Console.ReadLine();
                ps.RemoveTopProxy();
            }
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
