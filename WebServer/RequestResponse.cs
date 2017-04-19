/*
* The main data request and receive  structure from and to the client
**/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Collections;
using System.Globalization;
using System.IO;


namespace WebServer
{
    public enum ConnectionType
    {
        Close,
        KeepAlive
    };


    public class HTTPRequest : EventArgs
    {
        public string method;
        public Uri URI;
        public string path;
        public string queryString;
        public string fragment;
        public int versionMajor;
        public int versionMinor;

        public Hashtable headers;
        public byte[] body;
        public long contentLength;
        public string contentType;

        public Hashtable cookies;

        public bool onHeadersEndCalled;

        public HttpStatusCode statusCode;
        public string statusReason;

        public Encoding encoding;
        public bool shouldKeepAlive;

    };


    public class HTTPResponse
    {
        public string version;
        public HttpStatusCode status;
        public string reason;

        public Hashtable headers;
        public byte[] bodyData;
        public Stream body;
        public Hashtable cookies;


        public ConnectionType connection;
        public string contentType;
        public long contentLength;
        public Encoding encoding;


        public HTTPResponse(HTTPRequest request)
        {
            version = "HTTP/" + request.versionMajor + "." + request.versionMinor;
            reason = " My Server Mariusp   ";
            status = HttpStatusCode.OK;
            //headers["Content-Type"] = request.headers["Content-Type"];
            bodyData = request.body;
            headers = new Hashtable();
            cookies = request.cookies;

            contentType = "text/html";
            encoding = request.encoding;
            connection = (request.shouldKeepAlive ? ConnectionType.KeepAlive : ConnectionType.Close);
        }


        public HTTPResponse(string version_, HttpStatusCode status_, string reason_)
        {
            version = version_;
            status = status_;
            reason = reason_;
            contentType = "text/html";
            headers = new Hashtable();
            headers["Content-Type"] = "text/html";
            encoding = Encoding.UTF8;
            connection = ConnectionType.Close;
            body = new MemoryStream();
        }


        public void Add(string name, string value)
        {
            string lowerName = name.ToLower();
            headers[name] = value;
        }
    };
}
