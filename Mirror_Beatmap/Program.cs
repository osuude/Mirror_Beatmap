using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MirrorBeatmap
{
    public static class Program
    {
        internal static Dictionary<string, MemoryStream> MemoryCache = new Dictionary<string, MemoryStream>();
        public static void Main(string[] args)
        {
            if (!Directory.Exists("cache"))
                Directory.CreateDirectory("cache");
            Array.ForEach(Directory.GetFiles("cache"), file =>
            {
                var fs = File.OpenRead(file);
                if (fs.Length < 1)
                {
                    File.Delete(file);
                    return;
                }

                var ms = new MemoryStream();
                fs.CopyTo(ms);
               
                MemoryCache.Add(Path.GetFileName(file), ms);
            });

            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
