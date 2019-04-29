using System;
using System.IO;
using Nancy;

namespace Nancy.Responses
{
    public static class FormatterExtensions
    {
        public static PartialFileResponse AsPartialFile(this IResponseFormatter formatter, string applicationRelativeFilePath, string contentType)
        {
            return new PartialFileResponse(applicationRelativeFilePath, contentType, formatter.Context);
        }

        public static PartialFileResponse AsPartialFile(this IResponseFormatter formatter, string applicationRelativeFilePath)
        {
            return new PartialFileResponse(applicationRelativeFilePath, formatter.Context);
        }
    }
}