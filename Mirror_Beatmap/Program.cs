using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Server.Kestrel.Transport;
using System.Threading;

namespace Mirror_Beatmap
{
    public class Program
    {
        public static void Main(string[] args)
        {

            foreach (var file in Directory.GetFiles("cache"))
            {
                var fileContent = File.ReadAllBytes(file);
                if (fileContent.Length < 10)
                    File.Delete($"cache/{file}");
                else
                    Nancy.Responses.PartialFileResponse.cacheInMemory.Add(file, new MemoryStream(fileContent));
            }
            new Thread(() => { for (; ; ) { Thread.Sleep(10000); GC.Collect(int.MaxValue, GCCollectionMode.Forced, true, false); } }) { IsBackground = true }.Start();
            Nancy.Responses.PartialFileResponse.SafePaths.Add("cache");
            CreateWebHostBuilder(args).Build().Run();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>()
                //.UseLibuv()
                .UseKestrel(options =>
                {
                    //options.ListenUnixSocket("/dev/shm/asp.netcore.sock");
                    options.ListenLocalhost(9999, configure =>
                    {
                        //configure.UseHttps("dev.zhzi233.cn.pfx", File.ReadAllText("keystorePass.txt"));
                        configure.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
                    });
                })
            ;
    }
}
