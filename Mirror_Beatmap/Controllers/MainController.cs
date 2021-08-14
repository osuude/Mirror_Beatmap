using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CSRedis;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Redis;
using Microsoft.Extensions.Logging;
using MirrorBeatmap.Services.Interfaces;

namespace MirrorBeatmap.Controllers
{
    [ApiController]
    [Route("/")]
    public class MainController : ControllerBase
    {

        static ConcurrentQueue<string> _thumbDownloadQueue = new ConcurrentQueue<string>();
        static ConcurrentQueue<string> _previewDownloadQueue = new ConcurrentQueue<string>();


        private readonly IDistributedCache redis;
        private readonly IResourcesDownloader resourcesDownloader;

        public MainController(IDistributedCache redis, IResourcesDownloader resourcesDownloader)
        {
            this.redis = redis;
            this.resourcesDownloader = resourcesDownloader;
        }


        [HttpGet]//根目录首页
        public ContentResult Get()
        {
            return new ContentResult
            {
                ContentType = "text/html",
                StatusCode = 200,
                Content = "<h1>Welcome to beatmap mirror of osu!ude</h1>"
            };
        }

        [HttpGet("/thumb/{id}.jpg")]
        public async Task<dynamic> Thumb(string id)
        {
            try
            {
                var thumbFileName = $"{id}.jpg";
                var buffer = await redis.GetAsync(thumbFileName);
                if (buffer != null)
                {
                    return File(buffer, "image/jpeg", true);
                }
                DownloadThumbAsync(id);
                DownloadPreview(id);

                return Redirect($"http://b.ppy.sh/thumb/{id}.jpg");
            }
            catch
            {
                return Redirect($"http://b.ppy.sh/thumb/{id}.jpg");
            }
        }

        [HttpGet("/preview/{id}.mp3")]
        public async Task<dynamic> Preview(string id)
        {
            try
            {
                var previewFilename = $"{id}.mp3";
                var buffer = await redis.GetAsync(previewFilename);
                if (buffer != null)
                {
                    return File(buffer, "audio/mpeg", true);
                }

                DownloadPreview(id);

                return Redirect($"https://cdnx.sayobot.cn:25225/preview/{id}.mp3");
            }
            catch
            {
                return Redirect($"https://cdnx.sayobot.cn:25225/preview/{id}.mp3");
            }
        }

        private void DownloadPreview(string id)
        {
            var previewFilename = $"{id}.mp3";
            if (resourcesDownloader.PreviewDownloadQueue.Contains(previewFilename))
                return;
            resourcesDownloader.DownloadPreview(previewFilename);

        }

        private void DownloadThumbAsync(string id)
        {
            if (!id.EndsWith('l'))
                DownloadThumbAsync(id + 'l');

            var thumbFileName = id + ".jpg";
            if (resourcesDownloader.ThumbDownloadQueue.Contains(thumbFileName))
                return;
            resourcesDownloader.DownloadThumb(thumbFileName);

        }
    }
}
