using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CSRedis;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Redis;
using Microsoft.Extensions.Logging;

namespace MirrorBeatmap.Controllers
{
    [ApiController]
    [Route("/")]
    public class MainController : ControllerBase
    {
        private readonly IDistributedCache redis;

        public MainController(IDistributedCache redis)
        {
            this.redis = redis;
        }


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
                var thumbFileName = $"{id}.jpg";
                var buffer = redis.Get(thumbFileName);
                if (buffer != null)
                {
                    var ms = new MemoryStream(buffer);
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
                var previewFilename = $"{id}.mp3";
                var buffer = redis.Get(previewFilename);
                if (buffer != null)
                {
                    var ms = new MemoryStream(buffer);
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

        private async Task DownloadPreviewSoundAsync(string id)
        {
            var previewFilename = $"{id}.mp3";
            if (await redis.GetAsync(previewFilename) != null)
                return;
            try
            {
                using var httpclient = new HttpClient();

                httpclient.DefaultRequestHeaders.Add("User-Agent", "osu!ude");
                httpclient.DefaultRequestHeaders.Add("Referrer", "https://osu.zhzi233.cn");
                var buffer = await httpclient.GetByteArrayAsync("https://cdnx.sayobot.cn:25225/preview/" + previewFilename);
                await redis.SetAsync(previewFilename, buffer);
            }
            catch
            {
                _ = redis.RemoveAsync(previewFilename);
            }

        }

        private async Task DownloadPreviewPictureAsync(string id)
        {
            try
            {
                var thumbFileName = id + ".jpg";

                if (redis.Get(thumbFileName) != null)
                    return;
                do
                {
                    using (var httpClient = new HttpClient())
                    {
                        var buffer =
                            await httpClient.GetByteArrayAsync($"https://b.ppy.sh/thumb/{id}.jpg");
                        redis.Set(thumbFileName, buffer);
                    }
                    if (id.EndsWith('l') && redis.Get(id + "l.jpg") != null)
                        return;
                    thumbFileName = id + "l.jpg";
                }
                while (true);
            }
            catch
            {
                _ = redis.RemoveAsync($"{id}.jpg");
                _ = redis.RemoveAsync($"{id}l.jpg");
            }

        }
    }
}
