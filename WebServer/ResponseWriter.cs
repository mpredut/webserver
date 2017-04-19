/*
 * ResponseWriter send the response to the client conected based on the request structure.
 * It format and build the data and then send as a byte frame to the socket. 
 * All the input for this class is the client context only.
 * Very carefully: 1. The data is sent asynchronous!
 *                 2.  Do NOT close the resources here!
 * A callback is call after the data was sent.
 **/

using System;
using System.IO;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Collections;
using System.Threading;


namespace WebServer
{

    public class ResponseWriter
    {

        private delegate void SentCallback(IAsyncResult ar);
        SentCallback onSentCallback = null;

        public void Send(HttpContext context)
        {
            onSentCallback = context.SentCallback;
            if (context.Stream == null)
            {
                throw new Exception("Internal error, STREAM NULL!");
            }
            if (context.Stream.IsConnected()
                && !context.Stream.isDisposed)
            {
                SendHeaders(context.Stream, context.Response);
                SendBody(context.Stream, context.Response.body);
            }
#if DEBUG
            else
            {
                Console.WriteLine("Context stream was destroyed unexpected!");
            }
#endif

            try
            {
                context.Stream.Flush();
            }
            catch (Exception err)
            {
                Logger.WriteLine("Failed to flush context stream!" + err.ToString());
            }
        }

        private void Send(Stream stream, string data, Encoding encoding)
        {
            try
            {
                byte[] buffer = encoding.GetBytes(data);
                Logger.WriteLine("Sending " + buffer.Length + " bytes.");
#if DEBUG
                if (data.Length < 1100)
                    Logger.WriteLine(data);
#endif
                stream.BeginWrite(buffer, 0, buffer.Length,
                    new AsyncCallback(onSentCallback), stream);
            }
            catch (Exception err)
            {
                Logger.WriteLine("Failed to send data through context stream." + err.ToString());
            }
        }


        private void SendBody(Stream stream, Stream body)
        {
            if (body == null) return;

            try
            {
                body.Flush();
                body.Seek(0, SeekOrigin.Begin);
                var buffer = new byte[HttpContext.BUFF_SIZE];
                int bytesRead = body.Read(buffer, 0, HttpContext.BUFF_SIZE);
                while (bytesRead > 0)
                {
                    stream.BeginWrite(buffer, 0, bytesRead,
                   new AsyncCallback(onSentCallback), stream);
                    bytesRead = body.Read(buffer, 0, HttpContext.BUFF_SIZE);
                }
                //do not close! : body.Close(); resource file routine is doing it!
            }
            catch (Exception err)
            {
                Logger.WriteLine("Failed to send body through context stream." + err.ToString());
            }
        }


        private void SendHeaders(Stream stream, HTTPResponse response)
        {
            var sb = new StringBuilder();
            sb.AppendFormat("{0} {1} {2}\r\n", response.version,
                (int)response.status, response.reason);
            sb.AppendFormat("{0}: {1}\r\n", "Content-Type", response.contentType);
            sb.AppendFormat("{0}: {1}\r\n", "Content-Length", response.contentLength);
            sb.AppendFormat("{0}: {1}\r\n", "Connection",
                (response.connection == ConnectionType.KeepAlive ? "Keep-Alive" : "Close"));

            if (response.cookies != null)
            {
                foreach (DictionaryEntry cookie in response.cookies)
                {
                    sb.Append("Set-Cookie: ");
                    sb.Append(cookie.Key);//name
                    sb.Append("=");
                    Cookie rc = (Cookie)cookie.Value;
                    sb.Append(rc.Value ?? string.Empty);

                    if (rc.Expires > DateTime.MinValue)
                        sb.Append(";expires=" + rc.Expires.ToString("R"));
                    if (!string.IsNullOrEmpty(rc.Path))
                        sb.AppendFormat(";path={0}", rc.Path);
                    sb.Append("\r\n");
                }
            }

            if (response.headers != null)
            {
                foreach (DictionaryEntry header in response.headers)
                {
                    sb.AppendFormat("{0}: {1}\r\n", header.Key, header.Value);
                }
                sb.Append("\r\n");
            }

            Send(stream, sb.ToString(), response.encoding);
        }


        public void SendErrorPage(HttpContext context, HTTPResponse response, Exception exception)
        {
            string htmlTemplate = @"<html>
            <head>
            <meta http-equiv=""Content-Type"" content=""text/html; charset=utf-8"" />
            <title>{1}</title>
            </head>
            <body>
                <h1>{0} - {1}</h1>
                <pre>{2}</pre>
            </body>
            </html>";

            var body = string.Format(htmlTemplate,
                                     (int)response.status,
                                     response.reason,
                                     exception);
            byte[] bodyBytes = response.encoding.GetBytes(body);
            context.Response = response;
            context.Response.body.Write(bodyBytes, 0, bodyBytes.Length);
            Send(context);
        }
    }
}