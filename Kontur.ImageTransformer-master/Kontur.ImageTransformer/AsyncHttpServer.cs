using Kontur.ImageTransformer.Handlers;
using System;
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
            maxThreads = Environment.ProcessorCount * 4;
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
            var sem = new Semaphore(maxThreads, maxThreads);
            while (true)
            {
                try
                {
                    sem.WaitOne();
                    listener.GetContextAsync().ContinueWith((c) =>
                    { 
                       sem.Release();

                       var context = c;
                       Task.Run(() =>
                       {
                           HandleContextAsync(context);
                       });
                    });
                }
                catch (ThreadAbortException)
                {
                    return;
                }
                catch (Exception ex)
                {
                }
            }
        }

        private async void HandleContextAsync(Task<HttpListenerContext> Context)
        {
            var listenerContext = await Context;
            try
            {
                if (listenerContext.Request.ContentLength64 > 1024 * 100)
                    throw new NotImplementedException("Request body size higher than 100kb");

                if (listenerContext.Request.HttpMethod != "POST")
                    throw new NotImplementedException("Not POST request");

                var requestUrl = WebUtility.UrlDecode(listenerContext.Request.Url.AbsolutePath);

                using (var image = Image.FromStream(listenerContext.Request.InputStream))
                {
                    using (var requestBody = new Bitmap(image))
                    {
                        var response = new RequestHandler().GetResponse(requestUrl, requestBody);

                        listenerContext.Response.StatusCode = (int)response.statusCode;
                        if (response.Image != null)
                            response.Image.Save(listenerContext.Response.OutputStream, ImageFormat.Png);

                    }
                }
            }
            catch (Exception ex)
            {
                listenerContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            }
            finally
            {
                try { listenerContext.Response.Close(); }
                catch{ }
            }
        }

        private readonly HttpListener listener;
        
        private Thread listenerThread;
        private bool disposed;
        private volatile bool isRunning;
        private int maxThreads;
    }
}