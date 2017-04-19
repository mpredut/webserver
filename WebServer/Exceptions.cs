/**
 * File include all exception types
 * HttpException, ParserException, ForbiddenException. 
 **/

using System;
using System.Net;

namespace WebServer
{
    public class HttpException : Exception
    {

        public HttpException(HttpStatusCode code, string errMsg)
            : base(errMsg)
        {
            Code = code;
        }

        protected HttpException(HttpStatusCode code, string errMsg, Exception inner)
            : base(errMsg, inner)
        {
            Code = code;
        }

        public HttpStatusCode Code { get; private set; }
    }


    public class ParserException : Exception
    {

        public ParserException(string errMsg)
            : base(errMsg)
        {
        }

        public ParserException(string errMsg, Exception inner)
            : base(errMsg, inner)
        {
        }
    }


    public class ForbiddenException : HttpException
    {

        public ForbiddenException(string errMsg)
            : base(HttpStatusCode.Forbidden, errMsg)
        {
        }

        protected ForbiddenException(string errMsg, Exception inner)
            : base(HttpStatusCode.Forbidden, errMsg, inner)
        {
        }
    }
}