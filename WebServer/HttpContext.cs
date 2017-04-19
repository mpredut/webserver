/*
 * HttpContext class store a context per client connection.
 * It include the stream(socket) where receive and send the data and the management of the connection.
 * Data received is managed with a buffer pool
 **/

using System;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections;
using System.Globalization;
using System.Collections.Generic;

using WebServer.Decoder;

namespace WebServer
{

    public enum CloseType
    {
        Pending,
        immediately,
        Reuse
    }

    public class HttpContext : IDisposable
    {

        public readonly static int BUFF_SIZE = 4196;

        private static readonly ObjectPool<byte[]> Buffers =
            new ObjectPool<byte[]>(() => new byte[BUFF_SIZE]);

        private byte[] buffer;
        private int timerKeepAliveTimeout = 1000; // miliseconds
        private Timer timerKeepAlive = null;

        //for debug purpose
        private static long instanceCounter = 0;
        public long instanceId = 0;
        //
       
        public HTTPRequest Request;
        public HTTPResponse Response;
        public SocketNetworkStream Stream;

        internal Socket Socket { get; private set; }
        ResourceManager filemanager;
        private BodyDecoderCollection bodyDecoders;

        //for synchronize ReciveCallback with close context
        private EventWaitHandle _waitHandle = new AutoResetEvent(false);
        private bool isDisposed = false;
        IAsyncResult currentAynchResult;
        private Object objlock = new Object();


        public HttpContext(Socket socket, ResourceManager fm, BodyDecoderCollection bd)
        {
#if DEBUG
            this.instanceId = Interlocked.Increment(ref instanceCounter);
#endif
            setContext(socket, fm, bd);
        }


        public void setContext(Socket socket, ResourceManager fm, BodyDecoderCollection bd)
        {
            Socket = socket;
            //todo: move those to static
            filemanager = fm;
            bodyDecoders = bd;
            Parser.onParserEnd = OnParserEnd;
         
            //todo: make it static ?
            if (timerKeepAlive != null) timerKeepAlive.Dispose();
            timerKeepAlive = new Timer(OnConnectionTimeout);

            buffer = Buffers.Dequeue();

            isDisposed = false;
            Request = null;
            Response = null;
        }


        internal void Start()
        {
            Stream = new SocketNetworkStream(Socket, true);
            object[] obj = new object[3];
            obj[0] = buffer;
            obj[1] = Stream;
            obj[2] = instanceId;
            currentAynchResult = Stream.BeginRead(buffer, 0, buffer.Length, ReceiveCallback, obj);
            //_waitHandle.Set();
        }


        private void ReceiveCallback(IAsyncResult ar)
        {
            lock (objlock)
            {
                //mutexRead.WaitOne();
                if (isDisposed) return;

                Request = null;
                Response = null;
                //_waitHandle.Reset();
                //Console.WriteLine("ctx  " + this.instanceId);
                // been closed by our side.
                if (Stream == null)
                    return;

                // Fetch a user-defined object that contains information 
                object[] obj = new object[3];
                obj = (object[])ar.AsyncState;
                byte[] buffer = (byte[])obj[0]; // Received byte array            
                Stream stream = (Stream)obj[1];// A Socket to handle remote host communication. 
                long instanceId = (long)obj[2];
                if (this.instanceId != instanceId)
                {
                    Console.WriteLine("\nAIUREA\n");
                    return;
                };

                try
                {
                    int bytesLeft = Stream.EndRead(ar);
                    if (bytesLeft == 0)
                    {
                        Logger.WriteLine("Client disconnected: " + this.instanceId);
                        //Close(CloseType.immediately);
                        return;
                    }
#if DEBUG
                    Logger.WriteLine(this.instanceId + " received " + bytesLeft + " bytes.");
                    if (bytesLeft < 1100)
                    {
                        string temp = Encoding.Default.GetString(buffer, 0, bytesLeft);
                        Logger.WriteLine(temp);
                    }
#endif

                    int offset = Parser.ParseBuffer(buffer, bytesLeft, this);
                    bytesLeft -= offset;

                    if (bytesLeft > 0)
                    {
                        // Moving  bytesLeft  from  offset to beginning of array
                        Buffer.BlockCopy(buffer, offset, buffer, 0, bytesLeft);
                    }
                    if (Stream != null && (Stream.IsConnected()
                        && !Stream.isDisposed))
                    {
                        currentAynchResult = Stream.BeginRead(buffer, bytesLeft, buffer.Length - bytesLeft, ReceiveCallback, obj);
                    }
#if DEBUG
                    else
                    {
                        Logger.WriteLine("Stream is null!!!");
                    }
#endif
                    //_waitHandle.Set();
                }

                catch (ParserException err)
                {
                    Logger.WriteLine(err.ToString());
                    var response = new HTTPResponse("HTTP/1.0", HttpStatusCode.BadRequest, err.Message);
                    var writer = new ResponseWriter();
                    writer.SendErrorPage(this, response, err);
                    //Close(CloseType.immediately);
                }

                catch (Exception err)
                {
                    if (!(err is IOException))
                    {
                        Logger.WriteLine("Failed to read from stream: " + err);
                        var response = new HTTPResponse("HTTP/1.0", HttpStatusCode.InternalServerError, err.Message);
                        var writer = new ResponseWriter();
                        writer.SendErrorPage(this, response, err);
                    }
                    // Close(CloseType.immediately);
                }
                finally
                {
                    // mutexRead.ReleaseMutex();
                }
            }
        }

        
        public void SentCallback(IAsyncResult ar)
        {
      
            try
            {
                // A Socket which has sent the data to remote host 
                /* todo: synchronize it!
                SocketNetworkStream handler = (SocketNetworkStream)ar.AsyncState;
                handler.EndWrite(ar);
                 * */
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                 "ERR: SendCallback!" + ex.ToString());
            }
            finally
            {
            }
        }


        private void SetTimer()
        {
            if (timerKeepAlive == null)
                timerKeepAlive = new Timer(OnConnectionTimeout);
            timerKeepAlive.Change(timerKeepAliveTimeout, timerKeepAliveTimeout);
        }


        private void OnParserEnd(object sender, HTTPRequest request)
        {
            HttpContext ctx = (HttpContext)sender;
            ctx.Request = request;

#if DEBUG
            //ctx = this;
            if (request == null || request.URI == null)
            {
            }
            if (ctx.Request.URI.AbsolutePath != request.URI.AbsolutePath)
            {
                // Console.WriteLine("DIFF0");
            }
            if (((HttpContext)sender).instanceId != this.instanceId || this.instanceId != ctx.instanceId)
            {
                ///Console.WriteLine("DIFF1");
            }
            //Logger.WriteLine("SendCtx" + ctx.instanceId);
#endif

            try
            {

                ctx.Response = new HTTPResponse(ctx.Request);

                // keep alive.
                if (ctx.Request.shouldKeepAlive)
                {
                    ctx.Response.Add("Keep-Alive", "timeout=60, max=1000");

                    // refresh timer
                    ctx.SetTimer();
                }

                ctx.OnSendHTTPResponse(ctx);

                //keep alive
                //todo: no re-frash and close if error before
                if (ctx.Request.shouldKeepAlive)
                {
                    ctx.SetTimer();
                }
                else
                {
                    ctx.Close(CloseType.immediately);
                    Logger.WriteLine("Closing connection.");
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine(ex.ToString());
            }
        }


        private void OnSendHTTPResponse(object sender)
        {
            var context = (HttpContext)sender;
            //Logger.WriteLine("ctx: " + context.instanceId + " OnSendHTTPResponse" + " thread id" + Thread.CurrentThread.ManagedThreadId);

            if (this.instanceId != context.instanceId)
            {
                Console.WriteLine("\nAIUREA2\n");
                return;
            };

            try
            {
                context.Response.Add("Date", DateTime.UtcNow.ToString("r"));
                context.Response.Add("Server", "MyWebServer");

                //POST
                if (context.Request.contentLength > 0)
                {
                    DecodeBody(Request);
                }

                //GET
                var res = filemanager.Process(context);

                //START 2 WRITE THREADS
                var writer = new ResponseWriter();
                if (res != ProcessingResult.Abort)
                {
                    writer.Send(context);
                }
            }

            catch (Exception err)
            {
                var writer = new ResponseWriter();
                writer.SendErrorPage(context, context.Response, err);
                Logger.WriteLine("Request failed. " + err.ToString());
            }
        }


        private void OnConnectionTimeout(object state)
        {       
            timerKeepAlive.Change(Timeout.Infinite, Timeout.Infinite);
            Logger.WriteLine("Keep-Alive timeout context " + this.instanceId);
            Close(CloseType.immediately);
        }
       


        public void Close(CloseType type)
        {
            lock (objlock)
            {
                Logger.WriteLine("Close context " + this.instanceId);

                if (Socket == null)
                    return;

                //if(Mutex.WaitAll(mutexRead.Handle, mutexRead.Handle);
                //mutexRead.WaitOne();
                // if (!mutexWrite.WaitOne()) return;

                isDisposed = true;

                try
                {
                    switch (type)
                    {
                        case CloseType.immediately:
                            Stream.Close();
                            break;
                        case CloseType.Pending:
                            Socket.Disconnect(true);
                            break;
                        case CloseType.Reuse:
                            //todo:
                            break;
                    }

                    //Free resource when thread is done
                    Socket = null;
                    Stream = null;
                }
                catch (Exception err)
                {
                    Logger.WriteLine("Failed to close context properly." + err.ToString());
                }
                finally
                {
                    //mutexRead.ReleaseMutex();
                    //mutexWrite.ReleaseMutex();
                    Buffers.Enqueue(buffer);
                    Disconnected(this, EventArgs.Empty); 
                    //SocketContextPool.httpContexts.Enqueue(this);
                }
            }
        }

        public void Dispose()
        {
            Buffers.Enqueue(buffer);         
        }


        private void DecodeBody(HTTPRequest request)
        {
            Encoding encoding = null;
            if (request.contentType != null)
            {
                string encodingStr = request.headers["Encoding"].ToString();
                if (!string.IsNullOrEmpty(encodingStr))
                    encoding = Encoding.GetEncoding(encodingStr);
            }

            if (encoding == null)
                encoding = Encoding.UTF8;

            // process body.
            MemoryStream a = new MemoryStream(request.body);
            DecodedData data = bodyDecoders.Decode
                (a, new ContentTypeHeader(request.contentType), encoding);
            if (data == null)
                return;

            //TODO: data is ready decoded data but do something with it 
            var Files = data.Files;
            var Form = data.Parameters;
        }


        ~HttpContext()
        {
            Logger.WriteLine("Distroy context: " + this.instanceId);
            //SocketContextPool.httpContexts.Enqueue(this);
        }


        public event EventHandler Disconnected = delegate { };
    }


}

