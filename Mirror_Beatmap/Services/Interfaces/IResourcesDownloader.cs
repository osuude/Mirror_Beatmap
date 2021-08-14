
using System.Collections.Concurrent;

namespace MirrorBeatmap.Services.Interfaces;
public interface IResourcesDownloader
{
    ConcurrentQueue<string> ThumbDownloadQueue { get; init; }
    ConcurrentQueue<string> PreviewDownloadQueue { get; init; }


    void DownloadThumb(string filename);
    void DownloadPreview(string filename);
}
