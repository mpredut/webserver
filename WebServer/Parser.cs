/*
 * Parser class that implement the HttpMachine interface to parse the HttpRequest from clients.
 * When the parser has done , it notify the caller client context and fill the client data structures.
 **/

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Net;
using Microsoft.Win32;

using HttpMachine;

namespace WebServer
{
    class Parser
    {

        private static Encoding chrEncoder = Encoding.UTF8;     // To encode string    
        public delegate void OnParserEnd(object sender, HTTPRequest request);
        public static OnParserEnd onParserEnd;


        class Handler : IHttpParserDelegate
        {
            HttpContext ctx_;
            public static readonly int MAX_LENGHT_URI = 2083;
            //public event EventHandler<HTTPRequest> OnParserEnd = delegate { };

            public List<HTTPRequest> Requests = new List<HTTPRequest>();

            protected string headerName, headerValue;
            protected string method, URI, path, queryString, fragment, statusReason;
            protected int versionMajor = -1, versionMinor = -1;
            protected int statusCode;
            protected Hashtable headers;
            protected List<ArraySegment<byte>> body;
            protected bool onHeadersEndCalled, shouldKeepAlive;

            public Handler(HttpContext ctx)
            {
                ctx_ = ctx;
            }

            public void OnMessageBegin(HttpParser parser)
            {
                //Logger.WriteLine("OnMessageBegin");

                // TODO: this used to work, but i removed the StringBuffers. so work around maybe
                // defer creation of buffers until message is created so 
                // NullRef will be thrown if OnMessageBegin is not called.

                headers = new Hashtable();
                body = new List<ArraySegment<byte>>();
            }

            public void OnMessageEnd(HttpParser parser)
            {
                //Logger.WriteLine("OnMessageEnd");

                //  Assert.AreEqual(shouldKeepAlive, parser.shouldKeepAlive,
                //     "Differing values for parser.shouldKeepAlive between OnheadersEnd and OnMessageEnd");

                HTTPRequest request = new HTTPRequest();

                request.versionMajor = versionMajor;
                request.versionMinor = versionMinor;

                request.shouldKeepAlive = shouldKeepAlive;
                request.method = method;

                //URI
                //TODO: Hack to fix it to work for all host!
                if (URI.Length > MAX_LENGHT_URI)
                {
                    throw new Exception("URI lenght is too high: " + URI.Length + " insteed of " + MAX_LENGHT_URI);
                }
                if (headers.ContainsKey("Host"))
                {
                    request.URI = new Uri("http://" + headers["Host"].ToString() + URI);
                }
                else
                {
                    request.URI = new Uri("http://" + "127.0.0.1" + URI);
                }

                if (request.URI.AbsolutePath == "//" || request.URI.AbsolutePath == "/")
                {
                    request.URI = new Uri(request.URI, "/index.html");
                }

                //encoder
                if (headers.ContainsKey("Encoding"))
                {
                    request.encoding = Encoding.GetEncoding(headers.ContainsKey("Encoding").ToString());
                }
                else
                {
                    request.encoding = Encoding.UTF8;
                }

                request.path = path;
                request.queryString = queryString;
                request.fragment = fragment;

                //Cookie             
                if (headers.ContainsKey("Cookie"))
                {
                    CookieParser cp = new CookieParser(headers["Cookie"].ToString());
                    request.cookies = new Hashtable();
                    IEnumerable<Cookie> cookies = cp.Parse();
                    foreach (Cookie cookie in cookies)
                    {
                        request.cookies[cookie.Name] = cookie;
                    }
                }

                request.headers = headers;
                request.onHeadersEndCalled = onHeadersEndCalled;

                request.statusCode = (HttpStatusCode)statusCode;
                request.statusReason = statusReason;

                //POST Body
                // aggregate body chunks into one big chunk
                var length = body.Aggregate(0, (s, b) => s + b.Count);
                request.contentLength = length;
                if (length > 0)
                {
                    request.body = new byte[length];
                    int where = 0;
                    foreach (var buf in body)
                    {
                        Buffer.BlockCopy(buf.Array, buf.Offset, request.body, where, buf.Count);
                        where += buf.Count;
                    }
                }
                //TODO:move body from memory to Stream in case the file is big enought
                //_bodyFileName = Path.GetTempFileName();
                //body = new FileStream(bodyFileName, FileMode.CreateNew);          

                if (headers.ContainsKey("Content-Type"))
                    request.contentType = headers["Content-Type"].ToString();
                else request.contentType = null;

                // add it to the list of requests recieved.
                Requests.Add(request);

                // reset our internal state
                versionMajor = versionMinor = -1;
                method = URI = queryString = fragment = headerName = headerValue = null;
                headers = null;
                body = null;
                shouldKeepAlive = false;
                onHeadersEndCalled = false;

                onParserEnd(ctx_, request);
            }


            void CommitHeader()
            {
                //Logger.WriteLine("Committing header '" + headerName + "' : '" + headerValue + "'");
                headers[headerName] = headerValue;
                headerName = headerValue = null;
            }


            public void OnHeaderName(HttpParser parser, string str)
            {
                //Logger.WriteLine("OnHeaderName:  '" + str + "'");

                if (!string.IsNullOrEmpty(headerValue))
                    CommitHeader();
                headerName = str;
            }


            public void OnHeaderValue(HttpParser parser, string str)
            {
                //Logger.WriteLine("OnHeaderValue:  '" + str + "'");
                if (string.IsNullOrEmpty(headerName))
                    throw new Exception("Got header value without name.");

                headerValue = str;
            }


            public void OnHeadersEnd(HttpParser parser)
            {
                //Logger.WriteLine("OnheadersEnd");
                onHeadersEndCalled = true;

                if (!string.IsNullOrEmpty(headerValue))
                    CommitHeader();

                versionMajor = parser.MajorVersion;
                versionMinor = parser.MinorVersion;
                shouldKeepAlive = parser.ShouldKeepAlive;
            }


            public void OnBody(HttpParser parser, ArraySegment<byte> data)
            {
                //var str = Encoding.ASCII.GetString(data.Array, data.Offset, data.Count);
                //Logger.WriteLine("Onbody:  '" + str + "'");
                body.Add(data);
            }

        }


        //** HttpMachine interfaces **/
        class RequestHandler : Handler, IHttpRequestParserDelegate
        {

            public RequestHandler(HttpContext ctx)
                : base(ctx)
            {

            }


            public void OnMethod(HttpParser parser, string str)
            {
                //Logger.WriteLine("Onmethod: '" + str + "'");
                method = str;
            }


            public void OnRequestUri(HttpParser parser, string str)
            {
                //Logger.WriteLine("OnURI:  '" + str + "'");
                URI = str;
            }


            public void OnPath(HttpParser parser, string str)
            {
                //Logger.WriteLine("OnPath:  '" + str + "'");
                path = str;
            }


            public void OnQueryString(HttpParser parser, string str)
            {
                //Logger.WriteLine("OnqueryString:  '" + str + "'");
                queryString = str;
            }


            public void OnFragment(HttpParser parser, string str)
            {
                //Logger.WriteLine("Onfragment:  '" + str + "'");
                fragment = str;
            }
        }

        class ResponseHandler : Handler, IHttpResponseParserDelegate
        {

            public ResponseHandler(HttpContext ctx)
                : base(ctx)
            {

            }


            public void OnResponseCode(HttpParser parser, int code, string reason)
            {
                statusCode = code;
                statusReason = reason;
            }
        };


        /*
         * Main parser entry
         * */
        public static int ParseBuffer(byte[] buffer, int len, HttpContext ctx)
        {
            //Logger.WriteLine("Ctx" + ctx.instanceId);

            /* HttpMachine docs says:
            //If the returned value is not the same as the length of the buffer you provided, an error occurred while parsing. 
            //Make sure you provide a zero-length buffer at the end of the stream, as some callbacks may still be pending.
             * */
            var parsed = 0;
            try
            {
                RequestHandler h = new RequestHandler(ctx);
                //h.OnParserEnd += OnParserEnd;
                var parserRq = new HttpParser(h);
                Array.Resize(ref buffer, len);
                parsed = parserRq.Execute(new ArraySegment<byte>(buffer)); //index buffer end.
                parserRq.Execute(default(ArraySegment<byte>)); // this is what the specs says      
                /*
                if(h.Requests.Count() != 0)           
                    OnParserEnd(ctx, h.Requests[0]);
                else
                {
                   // throw new Exception("Parser error - no data!");
                }*/

                if (parsed > len) parsed = len;
                return (parsed >= 0 ? parsed : 0);//number of bytes successfully parsed
            }

            catch (Exception ex)
            {
                Logger.WriteLine(ex.ToString());
                throw new ParserException(ex.ToString());
            }
            finally
            {                
            }
        }
    }
}
