using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Nancy.Helpers;

namespace Nancy.Responses
{
    /// <summary>
    /// A response representing a file.
    /// </summary>
    /// <remarks>If the response contains an invalid file (not found, empty name, missing extension and so on) the status code of the response will be set to <see cref="HttpStatusCode.NotFound"/>.</remarks>
    public class PartialFileResponse : Response
    {
        private const string ContentLength = "Content-Length";
        private const string AcceptRanges = "Accept-Ranges";
        private const string ContentRange = "Content-Range";
        private const string ContentDisposition = "Content-Disposition";
        private const string ETag = "ETag";
        private const string LastModified = "Last-Modified";
        private const string IfRange = "If-Range";

        /// <summary>
        /// Represents a list of "base paths" where it is safe to
        /// serve files from.
        /// Attempting to server a file outside of these safe paths
        /// will fail with a 404.
        /// </summary>
        public static IList<string> SafePaths { get; set; }

        /// <summary>
        ///  Size of buffer for transmitting file between flushes.
        /// </summary>
        public static int BufferSize = 0x1000;

        static PartialFileResponse()
        {
            SafePaths = new List<string>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PartialFileResponse"/> for the file specified
        /// by the <paramref name="filePath"/> parameter.
        /// </summary>
        /// <param name="filePath">The name of the file, including path relative to the root of the application, that should be returned.</param>
        /// <remarks>The <see cref="MimeTypes.GetMimeType"/> method will be used to determine the mimetype of the file and will be used as the content-type of the response. If no match if found the content-type will be set to application/octet-stream.</remarks>
        /// <param name="context">Current context</param>
        public PartialFileResponse(string filePath, NancyContext context)
            : this(filePath, MimeTypes.GetMimeType(filePath), context)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PartialFileResponse"/> for the file specified
        /// by the <paramref name="filePath"/> parameter and the content-type specified by the <paramref name="contentType"/> parameter.
        /// </summary>
        /// <param name="filePath">The name of the file, including path relative to the root of the application, that should be returned.</param>
        /// <param name="contentType">The content-type of the response.</param>
        /// <param name="context">Current context</param>
        public PartialFileResponse(string filePath, string contentType, NancyContext context)
        {
            this.InitializeGenericFileResponse(filePath, contentType, context);
        }

        /// <summary>
        /// Gets the filename of the file response
        /// </summary>
        /// <value>A string containing the name of the file.</value>
        public string Filename { get; protected set; }

        internal static Dictionary<string, Stream> cacheInMemory = new Dictionary<string, Stream>();
        private static Action<Stream> GetFileContent(string filePath, long rangeStart, long rangeEnd)
        {
            return stream =>
            {
                Stream source;
                if (cacheInMemory.Keys.Contains(filePath))
                    source = cacheInMemory[filePath];
                else
                {
                    source = File.OpenRead(filePath);
                    var fileBuffer= new byte[source.Length];
                    source.Read(fileBuffer);
                    cacheInMemory.Add(filePath, new MemoryStream(fileBuffer));
                }

                source.Position = 0;
                if (!source.CanSeek)
                    throw new InvalidOperationException(
                        "Sending Range Responses requires a seekable stream eg. FileStream or MemoryStream");

                var totalBytesToSend = rangeEnd - rangeStart + 1;
                var buffer = new byte[BufferSize];
                var bytesRemaining = totalBytesToSend;

                source.Seek(rangeStart, SeekOrigin.Begin);
                while (bytesRemaining > 0)
                {
                    var count = bytesRemaining <= buffer.Length
                        ? source.Read(buffer, 0, (int)Math.Min(bytesRemaining, int.MaxValue))
                        : source.Read(buffer, 0, buffer.Length);

                    try
                    {
                        stream.Write(buffer, 0, count);
                        stream.Flush();
                        bytesRemaining -= count;
                    }
                    catch (Exception httpException)
                    {
                        /* in Asp.Net we can call HttpResponseBase.IsClientConnected
                        * to see if the client broke off the connection
                        * and avoid trying to flush the response stream.
                        * instead I'll swallow the exception that IIS throws in this situation
                        * and rethrow anything else.*/
                        if (httpException.Message
                            == "An error occurred while communicating with the remote host. The error code is 0x80070057.")
                        {
                            return;
                        }

                        throw;
                    }
                }
                if (source is FileStream)
                    source.Dispose();
            };
        }

        static bool IsSafeFilePath(string rootPath, string filePath)
        {
            if (!File.Exists(filePath))
            {
                return false;
            }

            var fullPath = Path.GetFullPath(filePath);

            return fullPath.StartsWith(Path.GetFullPath(rootPath), StringComparison.OrdinalIgnoreCase);
        }

        private void InitializeGenericFileResponse(string filePath, string contentType, NancyContext context)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                this.StatusCode = HttpStatusCode.NotFound;
                return;
            }
            if (SafePaths == null || SafePaths.Count == 0)
            {
                throw new InvalidOperationException("No SafePaths defined.");
            }
            foreach (var rootPath in SafePaths)
            {
                string fullPath;
                if (Path.IsPathRooted(filePath))
                {
                    fullPath = filePath;
                }
                else
                {
                    fullPath = Path.Combine(rootPath, filePath);
                }

                if (IsSafeFilePath(rootPath, fullPath))
                {
                    this.Filename = Path.GetFileName(fullPath);

                    this.SetResponseValues(contentType, fullPath, context);

                    return;
                }
            }

            this.StatusCode = HttpStatusCode.NotFound;
        }

        private void SetResponseValues(string contentType, string fullPath, NancyContext context)
        {
            // TODO - set a standard caching time and/or public?
            var fi = new FileInfo(fullPath);

            var lastWriteTimeUtc = fi.LastWriteTimeUtc;
            var etag = string.Concat("\"", HashFileInfo(fi).ToString("x16"), "\"");
            var lastModified = lastWriteTimeUtc.ToString("R");
            var sourceLength = fi.Length;

            this.Headers[AcceptRanges] = "bytes";

            if (CacheHelpers.ReturnNotModified(etag, lastWriteTimeUtc, context))
            {
                this.StatusCode = HttpStatusCode.NotModified;
                this.ContentType = null;
                this.Contents = Response.NoBody;

                return;
            }

            this.Headers[ETag] = etag;
            this.Headers[LastModified] = lastModified;

            Range range = new Range(false, 0, sourceLength - 1);

            var ifRange = context.Request.Headers.IfRange;
            if (string.IsNullOrEmpty(ifRange)
                || ifRange == etag
                || (DateTime.TryParseExact(ifRange, "R", CultureInfo.InvariantCulture, DateTimeStyles.None, out var ifModifiedTime)
                    && (lastWriteTimeUtc - ifModifiedTime).Duration().TotalSeconds < 1))
            {
                // rangeHeader should be of the format "bytes=0-" or "bytes=0-12345" or "bytes=123-456"
                range = GetRangeFromHeaders(context.Request, sourceLength);
            }

            if (range.ReturnRange == false)
            {
                this.Headers[ContentLength] = sourceLength.ToString(CultureInfo.InvariantCulture);
                this.StatusCode = HttpStatusCode.OK;
            }
            else
            {
                if (range.Start < 0 || range.End > sourceLength - 1 || range.Length < 0)
                {
                    this.StatusCode = HttpStatusCode.RequestedRangeNotSatisfiable;
                    this.Contents = Response.NoBody;
                    return;
                }
                else
                {
                    this.Headers[ContentLength] = range.Length.ToString(CultureInfo.InvariantCulture);
                    this.Headers[ContentRange] = string.Format("bytes {0}-{1}/{2}", range.Start, range.End, sourceLength);
                    this.StatusCode = HttpStatusCode.PartialContent;
                }
            }

            if (range.Length > 0)
            {
                this.Contents = GetFileContent(fullPath, range.Start, range.End);
            }

            this.ContentType = contentType;
        }

        private static Range GetRangeFromHeaders(Request request, long sourceLength)
        {
            // rangeHeader should be of the format "bytes=0-" or "bytes=0-12345" or "bytes=123-456"
            var rangeHeader = request.Headers["Range"].FirstOrDefault();

            if (!string.IsNullOrEmpty(rangeHeader) && rangeHeader.Contains("="))
            {
                var rangeParts = rangeHeader.Split('=')[1].Split('-');
                var rangeStart = long.Parse(rangeParts[0]);
                var rangeEnd = rangeParts.Length == 2 && !string.IsNullOrEmpty(rangeParts[1])
                    ? long.Parse(rangeParts[1]) // the client requested a chunk
                    : sourceLength - 1;

                return new Range(true, rangeStart, rangeEnd);
            }

            return new Range(false, 0, sourceLength - 1);
        }

        private static long HashFileInfo(FileInfo fileInfo)
        {
            unchecked // Overflow is fine, just wrap
            {
                long hash = 17;
                hash = (hash * 23) + fileInfo.Length;
                hash = (hash * 23) + fileInfo.LastWriteTimeUtc.Ticks;
                return hash;
            }
        }

        private struct Range
        {
            public Range(bool returnRange, long start, long end)
            {
                this.ReturnRange = returnRange;
                this.Start = start;
                this.End = end;
            }

            public bool ReturnRange { get; }
            public long Start { get; }
            public long End { get; }
            public long Length => (this.End - this.Start + 1);
        }
    }
}