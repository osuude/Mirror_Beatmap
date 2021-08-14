
using Microsoft.Extensions.Caching.Distributed;
using MirrorBeatmap.Services.Interfaces;
using System.Collections.Concurrent;

namespace MirrorBeatmap.Services;
public class ResoucesDownloader : IResourcesDownloader
{
    private readonly IDistributedCache redis;
    private readonly HttpClient thumbHttpClient = new();
    private readonly HttpClient previewHttpClient = new();


    public ConcurrentQueue<string> ThumbDownloadQueue { get; init; }
    public ConcurrentQueue<string> PreviewDownloadQueue { get; init; }

    public ResoucesDownloader(IDistributedCache redis)
    {
        ThumbDownloadQueue = new ConcurrentQueue<string>();
        PreviewDownloadQueue = new ConcurrentQueue<string>();
        this.redis = redis;
        previewHttpClient.DefaultRequestHeaders.Add("User-Agent", "osu!ude");
        previewHttpClient.DefaultRequestHeaders.Add("Referrer", "https://osu.zhzi233.cn");

        Task.Run(processThumbDownload);
        Task.Run(processPreviewDownload);
    }

    private async void processThumbDownload()
    {
        while (true)
        {
            if (ThumbDownloadQueue.TryDequeue(out var _filename))
            {
                try
                {

                    var buffer = await thumbHttpClient.GetByteArrayAsync("https://b.ppy.sh/thumb/{id}.jpg" + _filename);
                    await redis.SetAsync(_filename, buffer);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
            else
                await Task.Delay(100);

        }
    }

    private async void processPreviewDownload()
    {
        while (true)
        {
            if (PreviewDownloadQueue.TryDequeue(out var _filename))
            {
                try
                {
                    //                                                     "cdnx" not a typo
                    var buffer = await previewHttpClient.GetByteArrayAsync("https://cdnx.sayobot.cn:25225/preview/" + _filename);
                    await redis.SetAsync(_filename, buffer);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
            else
                await Task.Delay(100);

        }
    }



    public void DownloadPreview(string filename)
    {
        PreviewDownloadQueue.Enqueue(filename);
    }

    public void DownloadThumb(string filename)
    {
        ThumbDownloadQueue.Enqueue(filename);
    }
}
