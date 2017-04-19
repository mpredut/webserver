/*
 * SocketNetworkStream is a wrapper for the stream and the socket.
 * It let as to detect if a stream was destroyed, connected or not.
 *Also it permit to use more efficient the resources so that
 *close the socket with possibility to reuse without destroy resources 
 **/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net.Sockets;


namespace WebServer
{
    public class SocketNetworkStream : NetworkStream
    {

        public bool isDisposed;
        private static readonly byte[] POLLING_BYTE_ARRAY = new byte[0];
        private static void DummyCallback(IAsyncResult ar) { }
        private static AsyncCallback aDummyCallback = new AsyncCallback(DummyCallback);
      
        public SocketNetworkStream(Socket socket)
            : base(socket)
        {
        }
        public SocketNetworkStream(Socket socket, bool ownsSocket)
            : base(socket, ownsSocket)
        {
        }
        public SocketNetworkStream(Socket socket, FileAccess access)
            : base(socket, access)
        {
        }

        public SocketNetworkStream
            (Socket socket, FileAccess access, bool ownsSocket)
            : base(socket, access, ownsSocket)
        {
        }
        
        public override void Close()
        {
            if (isDisposed)
                throw new ObjectDisposedException(GetType().FullName);

            if (Socket != null && Socket.Connected)
            {
                //this.BeginRead(POLLING_BYTE_ARRAY, 0, POLLING_BYTE_ARRAY.Length, aDummyCallback, null);
                //this.BeginWrite(POLLING_BYTE_ARRAY, 0, POLLING_BYTE_ARRAY.Length, aDummyCallback, null);
                Socket.Close();
            }
            base.Close();
        }

        public bool IsConnected()
        {
            try
            {
                if (!this.isDisposed && this.Socket.Connected)
                {
                    // Twice because the first time will return without issue but
                    // cause the Stream to become closed (if the Stream is actually
                    // closed.)
                    this.Write(POLLING_BYTE_ARRAY, 0, POLLING_BYTE_ARRAY.Length);
                    this.Write(POLLING_BYTE_ARRAY, 0, POLLING_BYTE_ARRAY.Length);
                    return true;
                }
                return false;
            }
            catch (ObjectDisposedException)
            {
                // Since we're disposing of both Streams at the same time, one
                // of the streams will be checked after it is disposed.
                return false;
            }
            catch (IOException)
            {
                // This will be thrown on the second stream.Write when the Stream
                // is closed on the client side.
                return false;
            }
        }


        protected override void Dispose(bool disposing)
        {
            if (isDisposed) return;

            try
            {
                if (disposing)
                {
                    if (Socket != null && Socket.Connected)
                    {
                        try
                        {
                            Socket.Disconnect(true);
                        }
                        catch (ObjectDisposedException) { }
                    }
                }
            }
            finally
            {
                base.Dispose(disposing);
                isDisposed = true;
            }
        }

    }
}