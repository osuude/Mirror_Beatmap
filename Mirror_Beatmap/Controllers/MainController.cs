using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace MirrorBeatmap.Controllers
{
    [ApiController]
    [Route("/")]
    public class MainController : ControllerBase
    {
        [HttpGet]//根目录首页
        public ContentResult Get()
        {
            return new ContentResult
            {
                ContentType = "text/html",
                StatusCode = 200,
                Content = $"<h1>Welcome to beatmap mirror of osu!ude</h1>{Environment.CurrentDirectory}"
            };
        }

        [HttpGet("/thumb/{id}.jpg")]
        public async Task<dynamic> Thumb(string id)
        {
            try
            {
                if (Program.MemoryCache.ContainsKey($"{id}.jpg"))
                {
                    var ms = new MemoryStream();
                    Program.MemoryCache[$"{id}.jpg"].Seek(0, SeekOrigin.Begin);
                    await Program.MemoryCache[$"{id}.jpg"].CopyToAsync(ms);
                    ms.Seek(0, SeekOrigin.Begin);
                    return File(ms, "image/jpeg", true);

                }
                DownloadPreviewPictureAsync(id);
                DownloadPreviewSoundAsync(id);

                return Redirect($"http://b.ppy.sh/thumb/{id}.jpg");
            }
            catch
            {
                return Redirect($"http://b.ppy.sh/thumb/{id}.jpg");
            }
        }

        [HttpGet("/preview/{id}.mp3")]
        public dynamic Preview(string id)
        {
            try
            {
                if (Program.MemoryCache.ContainsKey($"{id}.mp3"))
                {
                    var ms = new MemoryStream();
                    Program.MemoryCache[$"{id}.mp3"].Seek(0, SeekOrigin.Begin);
                    Program.MemoryCache[$"{id}.mp3"].CopyTo(ms);
                    ms.Seek(0, SeekOrigin.Begin);
                    return File(ms, "audio/mpeg", true);

                }

                DownloadPreviewSoundAsync(id);

                return Redirect($"https://cdnx.sayobot.cn:25225/preview/{id}.mp3");
            }
            catch
            {
                return Redirect($"https://cdnx.sayobot.cn:25225/preview/{id}.mp3");
            }
        }

        private async void DownloadPreviewSoundAsync(string id)
        {
            if (System.IO.File.Exists($"cache/{id}l.jpg"))
                return;
            try
            {
                using (var fs = System.IO.File.OpenWrite($"cache/{id}.mp3"))
                using (var WebClient = new System.Net.WebClient())
                {
                    var buffer = await WebClient.DownloadDataTaskAsync($"https://cdnx.sayobot.cn:25225/preview/{id}.mp3");
                    await fs.WriteAsync(buffer);
                    fs.Close();
                    if (!Program.MemoryCache.ContainsKey($"{id}.mp3"))
                        Program.MemoryCache.Add($"{id}.mp3", new MemoryStream(buffer));
                }
            }
            catch
            {
                System.IO.File.Delete($"cache/{id}.mp3");
            }

        }

        private async void DownloadPreviewPictureAsync(string id)
        {
            try
            {
                if (System.IO.File.Exists($"cache/{id}.jpg"))
                    return;
                using (var fs = System.IO.File.OpenWrite($"cache/{id}.jpg"))
                using (var WebClient = new System.Net.WebClient())
                {
                    var buffer =
                        await WebClient.DownloadDataTaskAsync($"https://b.ppy.sh/thumb/{id}.jpg");
                    await fs.WriteAsync(buffer);
                    fs.Close();
                    if (!Program.MemoryCache.ContainsKey($"{id}.jpg"))
                        Program.MemoryCache.Add($"{id}.jpg", new MemoryStream(buffer));
                }
                if (System.IO.File.Exists($"cache/{id}l.jpg"))
                    return;
                if (!id.EndsWith('l'))
                    using (var fs = System.IO.File.OpenWrite($"cache/{id}l.jpg"))
                    using (var WebClient = new System.Net.WebClient())
                    {
                        var buffer = WebClient.DownloadData($"https://b.ppy.sh/thumb/{id}l.jpg");
                        fs.Write(buffer);
                        fs.Close();
                        if (!Program.MemoryCache.ContainsKey($"{id}l.jpg"))
                            Program.MemoryCache.Add($"{id}l.jpg", new MemoryStream(buffer));
                    }
            }
            catch
            {
                System.IO.File.Delete($"cache/{id}.jpg");
                System.IO.File.Delete($"cache/{id}l.jpg");
            }

        }
    }
}
