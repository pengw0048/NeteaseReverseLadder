using System;

namespace NeteaseReverseLadder
{
    class Program
    {
        static void Main(string[] args)
        {
            start:
            var ps = new ProxySelector();
            if (!UpdateProxySelector(ps))
            {
                Console.WriteLine("Failed retriving proxy list");
                Console.ReadLine();
                return;
            }
            var proxy = new NeteaseProxy(ps);
            proxy.Start();
            Console.WriteLine("Please change proxy of Netease Music to address 127.0.0.1，port 15213");
            Console.WriteLine("Press enter to use the next proxy");
            while (true)
            {
                var aproxy = ps.GetTop();
                if (aproxy == null)
                {
                    Console.WriteLine("No available proxy, retrying");
                    proxy.Stop();
                    goto start;
                }
                Console.WriteLine("Using: " + aproxy);
                Console.ReadLine();
                ps.Remove(aproxy);
            }
        }
        static bool UpdateProxySelector(ProxySelector ps)
        {
            Console.WriteLine("Retriving proxy list");
            ps.UpdateList();
            Console.WriteLine(ps.Proxies.Count + " results, measuring speed");
            ps.UpdateLatency();
            return ps.Proxies.Count >= 1 && ps.Proxies[0].latency != int.MaxValue;
        }
    }
}
