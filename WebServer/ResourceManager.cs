
/*
 * The ResourceManager general class used to build the response
 * based on the resource type requested by the client. 
 * The Process function is the main entry for this class.
 **/

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace WebServer
{  

    public enum ProcessingResult
    {

        Continue,
        SendResponse,
        Abort
    }


    public class ResourceManager
    {

        public static readonly int CONTENT_LENGHT_LIMIT = 0;

        public class ResourceProvider
        {
            private readonly List<FileResources> providers = new List<FileResources>();
            private bool isStarted;
            public IList<string> Find(string path)
            {
                if (path == null)
                    return new string[0];

                var viewNames = new List<string>();
                foreach (FileResources provider in providers)
                    provider.Find(path, viewNames);

                return viewNames;
            }


            public int Count
            {
                get { return providers.Count; }
            }
            public void Add(FileResources loader)
            {
                if (isStarted)
                    throw new InvalidOperationException("Manager have been started.");
                providers.Add(loader);
            }
            public void Start()
            {
                isStarted = true;
            }
            public bool Exists(string uriPath)
            {
                foreach (FileResources provider in providers)
                {
                    if (provider.Exists(uriPath))
                        return true;
                }

                return false;
            }

            public Resource Get(string uri)
            {
                foreach (FileResources provider in providers)
                {
                    Resource resource = provider.Get(uri);
                    if (resource != null)
                        return resource;
                }

                return null;
            }

        }


        private readonly Dictionary<string, string> contentTypes =
            new Dictionary<string, string>();

        private readonly ResourceProvider resourceManager;

        public ResourceManager()
        {
            resourceManager = new ResourceProvider();
        }


        public ResourceProvider Resources
        {
            get { return resourceManager; }
        }


        public ProcessingResult Process(HttpContext context)
        {
            HTTPRequest request = context.Request;
            HTTPResponse response = context.Response;

            try
            {
                Resource resource = resourceManager.Get(context.Request.URI.LocalPath);
                if (resource == null)
                {
                    response.status = HttpStatusCode.NotFound;
                    response.reason = "Requested resource is not found!";
                    return ProcessingResult.Continue;
                }

                string extension = Path.GetExtension(request.URI.AbsolutePath).TrimStart('.');

                string header;
                if (!MIME.extensions.TryGetValue(extension, out header))
                {
                    response.status = HttpStatusCode.UnsupportedMediaType;
                    response.reason = "Media not supported!";
                    return ProcessingResult.Continue;
                }

                response.contentType = header;

                var str = request.headers["If-Modified-Since"] as string;
                if (str != null)
                    try
                    {
                        DateTime browserCacheDate = DateTime.Parse(str);

                        DateTime since = browserCacheDate.ToUniversalTime();
                        DateTime modified = resource.ModifiedAt;

                        // Allow for file systems with subsecond time stamps
                        modified = new DateTime(modified.Year, modified.Month, modified.Day, modified.Hour, modified.Minute, modified.Second, modified.Kind);
                        if (since >= modified)
                        {
                            response.status = HttpStatusCode.NotModified;
                            return ProcessingResult.SendResponse;
                        }
                    }
                    catch { };

                response.Add("Last-Modified", resource.ModifiedAt.ToString("r"));
                response.body = resource.Stream;
                response.contentLength = response.body.Length;

                if (CONTENT_LENGHT_LIMIT != 0 && response.contentLength > CONTENT_LENGHT_LIMIT)
                {
                    Logger.WriteLine("Requested to send " + response.contentLength +
                        " bytes, but we only allow " + CONTENT_LENGHT_LIMIT);
                    response.status = HttpStatusCode.ExpectationFailed;
                    response.reason = "Too large content length";
                    return ProcessingResult.Continue;
                }

                return ProcessingResult.SendResponse;
            }

            catch (Exception err)
            {
                var writer = new ResponseWriter();
                writer.SendErrorPage(context, context.Response, err);
                Logger.WriteLine("Failed to process file '" + request.URI.AbsolutePath + "'." + err.ToString());
            }

            return ProcessingResult.Abort;
        }
    }
}

