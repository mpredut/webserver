/*
 * SocketContextPool manage a poll of client context connections.
 * It start the main TCP listen on configured port.
 * When a client context is closed the SocketContextPool class is notified by the 
 * context itself and the context is reused.
 **/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;

using WebServer.Decoder;

namespace WebServer
{
    class SocketContextPool
    {
        private static int timeout = 500;
        private Socket socketSrv = null;

        //for shutdown management
        private bool shuttingDown = false;
        private long pendingAccepts = 0;
        private readonly ManualResetEvent shutdownEvent = new ManualResetEvent(false);

        //resources
        private readonly ResourceManager filemanager;
        private readonly BodyDecoderCollection bodyDecoders;

        public int CONTENT_LENGHT_LIMIT { get; set; }

        public static readonly ObjectPool<HttpContext> httpContexts = new ObjectPool<HttpContext>();


        public SocketContextPool(ResourceManager fm, BodyDecoderCollection bd)
        {
            filemanager = fm;
            bodyDecoders = bd;
        }


        public bool start(IPAddress ipAddress, int port, int maxConn)
        {
            try
            {
                if (shuttingDown)
                {
                    shutdownEvent.Set();
                    return true;
                }

                // Creates one SocketPermission object for access restrictions
                SocketPermission permission;
                permission = new SocketPermission(
                NetworkAccess.Accept,     // Allowed to accept connections 
                TransportType.Tcp,        // Defines transport types 
                "",                       // The IP addresses of local host 
                SocketPermission.AllPorts // Specifies all ports 
                );

                // Ensures the code to have permission to access a Socket 
                permission.Demand();

                // Creates a network endpoint 
                IPEndPoint ipEndPoint = new IPEndPoint(ipAddress, port);

                // Create one Socket object to listen the incoming connection 
                socketSrv = new Socket(
                    AddressFamily.InterNetwork,//ipAddr.AddressFamily,
                    SocketType.Stream,
                    ProtocolType.Tcp
                    );

                 socketSrv.Bind(ipEndPoint);

                // Places a Socket in a listening state and specifies the maximum 
                // Length of the pending connections queue 
                socketSrv.Listen(maxConn);
                socketSrv.ReceiveTimeout = timeout;
                socketSrv.SendTimeout = timeout;
                socketSrv.NoDelay = false;// Using the Nagle algorithm 

                /*
                ThreadPool.SetMaxThreads(50, 50);
                ThreadPool.SetMinThreads(10, 20);        
               */

                Interlocked.Increment(ref pendingAccepts);
                // Begins an asynchronous operation to accept an attempt 
                AsyncCallback aCallback = new AsyncCallback(AcceptCallback);
                socketSrv.BeginAccept(aCallback, socketSrv);
            }
            catch (Exception ex)
            {
                Logger.WriteLine("ERR: SocketContextPool.start!" + ex.ToString());
                return false;
            }
            return true;
        }

        
        public void AcceptCallback(IAsyncResult ar)
        {
            Socket srvSocket = null;

            // A new Socket to handle remote host communication 
            Socket socket = null;
            try
            {
                // Get Listening Socket object 
                srvSocket = (Socket)ar.AsyncState;//is identical with socketSrv

                if (socketSrv == null || srvSocket == null)//are indentical
                {
                    return;
                }

                // Create a new socket 
                socket = srvSocket.EndAccept(ar);
                if (socket == null || !socket.Connected)
                    return;

                //if shuttingDown requested
                Interlocked.Decrement(ref pendingAccepts);
                long _pendingAccepts = Interlocked.Read(ref pendingAccepts);
                if (shuttingDown && _pendingAccepts == 0)
                {
                    shutdownEvent.Set();
                    return;
                }

                socket.ReceiveTimeout = timeout;
                socket.SendTimeout = timeout;
                socket.NoDelay = false;  // Using the Nagle algorithm                                    

                // Simple way to deal with thread pool starvation for sockets
                int workerThreads = 0, completionPortThreads = 0;
                ThreadPool.GetAvailableThreads(out workerThreads, out completionPortThreads);
                if (completionPortThreads < 1)
                {
                    Logger.WriteLine("Resources arrives to limits! threads:" + workerThreads
                        + " sokets thrads: " + completionPortThreads);
                    ThreadPool.QueueUserWorkItem(threadPoolCallback, socket);
                    return;
                }

                // Got a new context.
                try
                {

                    if (httpContexts.count() <= 0)
                    {
                        HttpContext context = new HttpContext(socket, filemanager, bodyDecoders);
                        context.Disconnected += OnDisconnect;
                        //Console.WriteLine("Create a new context: " + context.instanceId);
                        //Console.WriteLine("In use" + context.instanceId);
                        context.Start();
                    }
                    else
                    {
                       // Console.WriteLine("ctx" + httpContexts.count());
                        HttpContext context = httpContexts.Dequeue();
                        context.Disconnected += OnDisconnect;
                       // Console.WriteLine("In use" + context.instanceId);
                        context.setContext(socket, filemanager, bodyDecoders);
                        context.Start();
                    }
                }
                catch (Exception err)
                {
                    Logger.WriteLine("Context creation raised an exception: " + err.Message);
                    socket.Disconnect(true);
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine(
                "ERR: AcceptCallback! " + ex.ToString());
            }
            finally
            {

                if (!shuttingDown)
                {
                    Interlocked.Increment(ref pendingAccepts);
                    // Begins an asynchronous operation to accept an attempt  
                    AsyncCallback aCallback = new AsyncCallback(AcceptCallback);
                    srvSocket.BeginAccept(aCallback, srvSocket);
                }
            }
        }


        private void OnDisconnect(object sender, EventArgs e)
        {
            var context = (HttpContext)sender;
            context.Disconnected -= OnDisconnect;
            SocketContextPool.httpContexts.Enqueue(context);            
            Logger.WriteLine("Context ctx " + context.instanceId + " disconected");
        }


        public void threadPoolCallback(Object threadContext)
        {
            try
            {
                byte[] buffer = new byte[10];
                Socket clientSocket = (Socket)threadContext;
                int nBytes = clientSocket.Receive(buffer); // Receive the request
                byte[] data = ResponceSend.notResponse();
                clientSocket.Send(data);
                clientSocket.Close();
            }
            catch
            {
                Logger.WriteLine("ERR: threadPoolCallback!");
            }
        }


        public void stop()
        {
            shuttingDown = true;
            try
            {
                if (socketSrv != null)
                {
                    socketSrv.Close();
                    //shutdownEvent.WaitOne();
                    //ThreadPool.finish??             
                    //socketSrv.BeginDisconnect(false, DisconnectCallback, null);
                    //socketSrv.Disconnect(false);
                    //socketSrv.Shutdown(SocketShutdown.Both); // Make sure to do this
                    //socketSrv.DisconnectAsync(null);                
                }
            }
            catch
            {
                Logger.WriteLine("ERR: Stop!");
            }
            socketSrv = null;            
        }

        private void DisconnectCallback(IAsyncResult result)
        {
            socketSrv.EndDisconnect(result);
            socketSrv.Shutdown(SocketShutdown.Both);
            socketSrv.Disconnect(false);
            socketSrv.Close();
        }
    }
}
