﻿using Kontur.ImageTransformer.Handlers;
using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing.Imaging;
using System.Drawing;

namespace Kontur.ImageTransformer
{
    internal class AsyncHttpServer : IDisposable
    {
        public AsyncHttpServer()
        {
            listener = new HttpListener();
            requestHandler = new RequestHandler();
        }
        
        public void Start(string prefix)
        {
            lock (listener)
            {
                if (!isRunning)
                {
                    listener.Prefixes.Clear();
                    listener.Prefixes.Add(prefix);
                    listener.Start();

                    listenerThread = new Thread(Listen)
                    {
                        IsBackground = true,
                        Priority = ThreadPriority.Highest
                    };
                    listenerThread.Start();
                    
                    isRunning = true;
                }
            }
        }

        public void Stop()
        {
            lock (listener)
            {
                if (!isRunning)
                    return;

                listener.Stop();

                listenerThread.Abort();
                listenerThread.Join();
                
                isRunning = false;
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;

            Stop();

            listener.Close();
        }
        
        private void Listen()
        {
            while (true)
            {
                try
                {
                    if (listener.IsListening)
                    {
                        var context = listener.GetContext();
                        Task.Run(() => HandleContextAsync(context));
                    }
                    else Thread.Sleep(0);
                }
                catch (ThreadAbortException)
                {
                    return;
                }
                catch (Exception error)
                {
                    // TODO: log errors
                }
            }
        }

        private async Task HandleContextAsync(HttpListenerContext listenerContext)
        {
            // TODO: implement request handling
            try
            {
                var requestUrl = WebUtility.UrlDecode(listenerContext.Request.Url.AbsolutePath);
                var requestMethod = listenerContext.Request.HttpMethod;
                //var requestBody = GetDataFromRequest(listenerContext);
                //var t = listenerContext.Request.InputStream.Length;
                var requestBody = Image.FromStream(listenerContext.Request.InputStream);
                

                var response = await requestHandler.GetResponse(requestUrl, requestMethod, requestBody);

                listenerContext.Response.StatusCode = (int)response.statusCode;
                if (response.Image != null)
                    using (var writer = new BinaryWriter(listenerContext.Response.OutputStream))
                        writer.Write(response.GetImageAsByteArray());
            }
            catch
            {
                listenerContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            }
            finally
            {
                listenerContext.Response.Close();
            }
        }
        
        //private byte[] GetDataFromRequest(HttpListenerContext context)
        //{
        //    using (var reader = new BinaryReader(context.Request.InputStream))
        //    {
        //        return reader.ReadBytes(160000);
        //    }
        //}

        private readonly HttpListener listener;

        private Thread listenerThread;
        private bool disposed;
        private volatile bool isRunning;
        private RequestHandler requestHandler;
    }
}