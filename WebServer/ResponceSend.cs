
/*
* Unused class, it can be removed.
* TODO: remove it!.
**/

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Microsoft.Win32;

namespace WebServer
{
    class ResponceSend
    {
        private static Encoding chrEncoder = Encoding.UTF8;     // To encode string 
        static public Hashtable respStatus;
        ResponceSend()
        {
            respStatusInit();
        }

        private void respStatusInit()
        {
            respStatus = new Hashtable();

            respStatus.Add(200, "200 Ok");
            respStatus.Add(201, "201 Created");
            respStatus.Add(202, "202 Accepted");
            respStatus.Add(204, "204 No Content");

            respStatus.Add(301, "301 Moved Permanently");
            respStatus.Add(302, "302 Redirection");
            respStatus.Add(304, "304 Not Modified");

            respStatus.Add(400, "400 Bad Request");
            respStatus.Add(401, "401 Unauthorized");
            respStatus.Add(403, "403 Forbidden");
            respStatus.Add(404, "404 Not Found");

            respStatus.Add(500, "500 Internal Server Error");
            respStatus.Add(501, "501 Not Implemented");
            respStatus.Add(502, "502 Bad Gateway");
            respStatus.Add(503, "503 Service Unavailable");
        }

        public static byte[] Send(HttpContext ctx)
        {
            byte[] data = null;
            /*
            if (buffer == null)
            {
                data = notResponse();
                return data;
            }*/
            try
            {
                /*
                if (buffer == null)
                {
                    data = notResponse();
                    return data;
                }*/


                string headersString = ctx.Response.version + " " + ctx.Response.status + " " + ctx.Response.reason + "\n";

                foreach (DictionaryEntry Header in ctx.Response.headers)
                {
                    headersString += Header.Key + ": " + Header.Value + "\n";
                }

                headersString += "\n";
                byte[] bheadersString = Encoding.ASCII.GetBytes(headersString);
                data = bheadersString.ToArray();
                // Send headers	
                ctx.Stream.Write(bheadersString, 0, bheadersString.Length);

                // Send body
                if (ctx.Response.bodyData != null)
                {
                    ctx.Stream.Write(ctx.Response.bodyData, 0, ctx.Response.bodyData.Length);
                    data = data.Concat(ctx.Response.bodyData).ToArray();
                }
                /*
                if (ctx.Response.fs != null)
                    using (ctx.Response.fs)
                    {
                        byte[] buff = new byte[1024*1024];
                        int bytesRead = 0;
                        while ((bytesRead = ctx.Response.fs.Read(buff, 0, buff.Length)) > 0)
                        {
                            ctx.Stream.Write(buff, 0, bytesRead);                        
                            bheadersString.Concat(buff).ToArray();
                        }

                        ctx.Response.fs.Close();
                    }
                if (ctx.Response.resource != null)
                {
                    byte[] buff = File.ReadAllBytes(ctx.Response.resource);
                    data = data.Concat(buff).ToArray();
                }
                */
            }
            catch
            {

            }
            finally
            {
                if (data == null)
                {
                    data = notResponse();
                }

            }
            return data;
        }

        static public byte[] notImplemented()
        {
            return sendResponse("<html><head><meta http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\">" +
            "</head><body><h2>My Web Server</h2><div>501 - method Not Implemented</div></body></html>",
                "501 Not Implemented",
                "text/html");
        }

        static public byte[] notFound()
        {
            return sendResponse("<html><head><meta http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\">" +
                "</head><body><h2>My Web Server</h2><div>404 - Not Found</div></body></html>",
                "404 Not Found",
                "text/html");
        }

        static public byte[] notResponse()
        {
            return sendResponse("<html><head><meta http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\">" +
                "</head><body><h2>My Web Server</h2><div>503 - Service Unavailable: " +
                "Server cannot response due to overloading or maintenance. The client can try again later</div></body></html>",
              "503 Service Unavailable",
              "text/html");
        }
        static private byte[] sendOk(byte[] bContent, string contentType)
        {
            return sendResponse(bContent, "200 OK", contentType);
        }

        // For strings
        static private byte[] sendResponse(string strContent, string responseCode, string contentType)
        {
            byte[] bContent = chrEncoder.GetBytes(strContent);
            return sendResponse(bContent, responseCode, contentType);
        }

        // For byte arrays
        static private byte[] sendResponse(byte[] bContent, string responseCode, string contentType)
        {
            try
            {
                byte[] bHeader = chrEncoder.GetBytes(
                                    "HTTP/1.1 " + responseCode + "\r\n"
                                  + "Server: My Web Server\r\n"
                                  + "Content-Length: " + bContent.Length.ToString() + "\r\n"
                                  + "Connection: close\r\n"
                                  + "Content-Type: " + contentType + "\r\n\r\n");
                byte[] data = bHeader.Concat(bContent).ToArray();
                return data;
            }
            catch
            {
                Logger.WriteLine("ERROR :sendResponse!");
            }
            return null;
        }

    }


}






/*
 *   static public void OnResponse(ref HttpContext ctx)
        {
           
            ctx.Response.version = "HTTP/1.1";

            if (ctx.Request.statusCode != (int)RespState.OK)
                ctx.Response.status = (int)RespState.BAD_REQUEST;
            else
                ctx.Response.status = (int)RespState.OK;
            ctx.Response.status = (int)RespState.OK;
            ctx.Response.headers = new Hashtable(); ;//new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            ctx.Response.headers.Add("Server", "myservr/1.0");
            ctx.Response.headers.Add("Date", DateTime.Now.ToString("r"));


            try {
                string path = contentPath + "\\" + ctx.Request.path.Replace("/", "\\");

            if (Directory.Exists(path))
            {
                if (File.Exists(path + "default.htm"))
                    path += "\\default.htm";
                else if (File.Exists(path + "index.html"))
                    path += "\\index.html";
                else
                {
                    string[] dirs = Directory.GetDirectories(path);
                    string[] files = Directory.GetFiles(path);

                    string bodyStr = "<!DOCTYPE HTML PUBLIC \"-//W3C//DTD HTML 4.0 Transitional//EN\">\n";
                    bodyStr += "<HTML><HEAD>\n";
                    bodyStr += "<META http-equiv=Content-Type content=\"text/html; charset=windows-1252\">\n";
                    bodyStr += "</HEAD>\n";
                    bodyStr += "<body><p>Folder listing, to do not see this add a 'default.htm' document\n<p>\n";
                    for (int i = 0; i < dirs.Length; i++)
                        bodyStr += "<br><a href = \"" + ctx.Request.URL + Path.GetFileName(dirs[i])
                                        + "/\">[" + Path.GetFileName(dirs[i]) + "]</a>\n";
                    for (int i = 0; i < files.Length; i++)
                        bodyStr += "<br><a href = \"" + ctx.Request.URL + Path.GetFileName(files[i]) + 
                                        "\">" + Path.GetFileName(files[i]) + "</a>\n";
                    bodyStr += "</body></HTML>\n";

                    ctx.Response.bodyData = Encoding.ASCII.GetBytes(bodyStr);
                    return;
                }
            }

            if (File.Exists(path))
            {
                RegistryKey rk = Registry.ClassesRoot.OpenSubKey(Path.GetExtension(path), true);

                // Get the data from a specified item in the key.
                String s = (String)rk.GetValue("Content Type");

                // Open the stream and read it back.
                //ctx.Response.fs = File.Open(path, FileMode.Open);
                ctx.Response.resource = path;
                if (s != "")
                    ctx.Response.headers["Content-type"] = s;
            }
            else
            {

                ctx.Response.status = (int)RespState.NOT_FOUND;

                string bodyStr = "<!DOCTYPE HTML PUBLIC \"-//W3C//DTD HTML 4.0 Transitional//EN\">\n";
                bodyStr += "<HTML><HEAD>\n";
                bodyStr += "<META http-equiv=Content-Type content=\"text/html; charset=windows-1252\">\n";
                bodyStr += "</HEAD>\n";
                bodyStr += "<body>File not found!!</body></HTML>\n";

                ctx.Response.bodyData = Encoding.ASCII.GetBytes(bodyStr);

            }


          
            }catch {

            };

        }


        public byte[] handleTheRequest(string strReceived)
        {            
          
            try
            {
                string httpmethod = strReceived.Substring(0, strReceived.IndexOf(" "));

                int start = strReceived.IndexOf(httpmethod) + httpmethod.Length + 1;
                int length = strReceived.LastIndexOf("HTTP") - start - 1;
                string requestedUrl = strReceived.Substring(start, length);

                string requestedFile;
                if (httpmethod.Equals("GET") || httpmethod.Equals("POST"))
                    requestedFile = requestedUrl.Split('?')[0];
                else // TODO : ADD HEAD metod
                {
                    data = notImplemented();
                    return data;
                }

               
               // int milliseconds = 10000;
               // Thread.Sleep(milliseconds);

                requestedFile = requestedFile.Replace("/", "\\").Replace("\\..", ""); 
                start = requestedFile.LastIndexOf('.') + 1;
                if (start > 0)
                {
                    length = requestedFile.Length - start;
                    string extension = requestedFile.Substring(start, length);
                    // IF we support this MIME extension
                    if (MIME.extensions.ContainsKey(extension))
                        if (File.Exists(contentPath + requestedFile)) // check if file exist
                            // send requested file with his content type.
                            data = sendOk(File.ReadAllBytes(contentPath + requestedFile),
                                MIME.extensions[extension]);
                        else
                            data = notFound(); 
                            //not found extension - not support.
                }
                else
                {
                    // If file is not specified try to send index.htm or index.html
                    // TODO ADD : "default.html"
                    if (requestedFile.Substring(length - 1, 1) != "\\")
                        requestedFile += "\\";
                    if (File.Exists(contentPath + requestedFile + "index.htm"))
                        data = sendOk(File.ReadAllBytes(contentPath + requestedFile + "\\index.htm"), "text/html");
                    else if (File.Exists(contentPath + requestedFile + "index.html"))
                        data = sendOk(File.ReadAllBytes(contentPath + requestedFile + "\\index.html"), "text/html");
                    else
                        data = notFound();
                }

            }
            catch {
                  data = notResponse();                
            }
                     

        }

   

*/