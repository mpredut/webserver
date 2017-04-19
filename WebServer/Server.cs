
/**
 * Server class store the server resources and start waiting for clients on the server port.
 * It include the resource manager and the request decoder to feed the client connected.
 * Here is start the main thread
 **/

using System;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using HttpMachine;
using WebServer.Decoder;

namespace WebServer
{
    class Server
    {

        public bool running = false; // if web server running or not 

        SocketContextPool socketContextPool = null;
        Thread threadSrv = null;
        public ResourceManager resourceManager = new ResourceManager();
        private readonly BodyDecoderCollection bodyDecoders = new BodyDecoderCollection();


        public Server()
        {
            //for POST method 
            bodyDecoders.Add(new MultiPartDecoder());
            bodyDecoders.Add(new UrlDecoder());
        }


        private static void SafeExecute(Action a, out Exception exception)
        {
            exception = null;
            try
            {
                a.Invoke();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        }


        public bool start(IPAddress ipAddress, int port, int maxConn, string contentPath)
        {
            Exception exception = null;

            //for GET method
            resourceManager.Resources.Add(new FileResources("/", contentPath));

            try
            {
                if (running) return false;
                running = true;

                socketContextPool = new SocketContextPool(resourceManager, bodyDecoders);

                threadSrv = new Thread(() =>
                    SafeExecute(() =>
                    {
                        bool ok = socketContextPool.start(ipAddress, port, maxConn);
                        if (!ok)
                        {
                            Logger.WriteLine("ERROR :start!");
                            //todo: tell to main thread we have error!
                            Thread.CurrentThread.Abort();
                        }
                    },
                    out exception)
                );

                threadSrv.Start();
                threadSrv.Join();

            }
            catch
            {
                running = false;
                return false;
            }

            if (exception != null)
            {
                running = false;
                return false;
            }

            return true;
        }


        public void stop()
        {
            if (running)
            {
                running = false;
                try
                {
                    if (!socketContextPool.Equals(null))
                    {
                        socketContextPool.stop();
                    };
                    threadSrv.Abort();
                }
                catch { }
            }
        }
    }
}
