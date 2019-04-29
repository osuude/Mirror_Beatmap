using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nancy;
using Nancy.Bootstrapper;
using Nancy.Responses;

namespace Mirror_Beatmap
{
    public class MainModule : NancyModule
    {
        public MainModule()
        {

            Get("/", args =>
            {
                return Html("<h1>Welcome to beatmap mirror of osu!ude</h1>" + Environment.CurrentDirectory);
            });


            Get("/thumb/{id}.jpg", args =>
             {
                 try
                 {
                     if (File.Exists($"cache/{args.id}.jpg"))
                         return new PartialFileResponse($"{args.id}.jpg", "image/jpeg", Context);

                     DownloadPreviewPicture();

                     return Response.AsRedirect($"https://b.ppy.sh/thumb/{args.id}.jpg", RedirectResponse.RedirectType.Permanent);
                 }
                 catch
                 {
                     return Response.AsRedirect($"https://b.ppy.sh/thumb/{args.id}.jpg", RedirectResponse.RedirectType.Permanent);
                 }
             });


            Get("/preview/{id}.mp3", args =>
             {
                 try
                 {
                     if (File.Exists($"cache/{args.id}.mp3"))
                         return new PartialFileResponse($"{args.id}.mp3", "audio/mpeg", Context);
                     
                     DownloadPreviewSound();

                     return Response.AsRedirect($"https://cdnx.sayobot.cn:25225/preview/{args.id}.mp3", RedirectResponse.RedirectType.Permanent);
                 }
                 catch
                 {
                     return Response.AsRedirect($"https://cdnx.sayobot.cn:25225/preview/{args.id}.mp3", RedirectResponse.RedirectType.Permanent);
                 }
             });
        }


        private Response Html(string Html)
        {
            return Response.AsText(Html, "text/html");
        }


        private void DownloadPreviewSound()
        {
            new Thread(() =>
            {
                try
                {
                    using (var fs = File.OpenWrite($"cache/{Response.Context.Parameters.id}.mp3"))
                    using (var WebClient = new WebClient())
                    {
                        var buffer = WebClient.DownloadData($"https://cdnx.sayobot.cn:25225/preview/{Response.Context.Parameters.id}.mp3");
                        fs.Write(buffer);
                        fs.Close();
                    }
                }
                catch { }
            })
            { IsBackground = true }.Start();
        }

        private void DownloadPreviewPicture()
        {
            new Thread(() =>
            {
                try
                {
                    using (var fs = File.OpenWrite($"cache/{Response.Context.Parameters.id}.jpg"))
                    using (var WebClient = new WebClient())
                    {
                        var buffer = WebClient.DownloadData($"http://zhzi233.cn/thumb/{Response.Context.Parameters.id}.jpg");
                        fs.Write(buffer);
                        fs.Close();
                    }
                    if (!((string)Response.Context.Parameters.id).EndsWith('l'))
                        using (var fs = File.OpenWrite($"cache/{Response.Context.Parameters.id}l.jpg"))
                        using (var WebClient = new WebClient())
                        {
                            var buffer = WebClient.DownloadData($"http://zhzi233.cn/thumb/{Response.Context.Parameters.id}l.jpg");
                            fs.Write(buffer);
                            fs.Close();
                        }
                }
                catch { }
            })
            { IsBackground = true }.Start();
        }
    }
}
