using Kontur.ImageTransformer.Handlers;
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
        private static Semaphore sem = new Semaphore(Environment.ProcessorCount * 4, Environment.ProcessorCount* 4);
        public AsyncHttpServer() 
        {
            listener = new HttpListener();
            requestHandler = new RequestHandler();
            //int maxThreadsCount = Environment.ProcessorCount * 4;
            //ThreadPool.SetMaxThreads(maxThreadsCount, maxThreadsCount);
            //ThreadPool.SetMinThreads(2, 2);
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
                        //ThreadPool.QueueUserWorkItem(new WaitCallback(HandleContext), context);
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

        private void HandleContextAsync(HttpListenerContext listenerContext)
        {
            sem.WaitOne();
            //var listenerContext = (HttpListenerContext)Context;
            // TODO: implement request handling
            var cts = new CancellationTokenSource();
            cts.CancelAfter(1000);
            try
            {
                if (listenerContext.Request.ContentLength64 > 1024 * 100)
                    throw new NotImplementedException("Request body size higher than 100kb");

                var requestUrl = WebUtility.UrlDecode(listenerContext.Request.Url.AbsolutePath);
                var requestMethod = listenerContext.Request.HttpMethod;
                
                //var requestBody = GetDataFromRequest(listenerContext);
                //var t = listenerContext.Request.InputStream.Length;
                //var t = new MemoryStream();
                //var t = ms.GetBuffer().Length;
                //t = 0;
                //byte[] buffer = new byte[listenerContext.Request.ContentLength64];

                var requestBody = new Bitmap(
                    Image.FromStream(listenerContext.Request.InputStream));
                //var requestBody = new Bitmap(listenerContext.Request.InputStream);
                //var requestBody = await Task.Factory.StartNew(() =>
                //{
                //    return Image.FromStream(listenerContext.Request.InputStream);
                //});

                //var ms = new MemoryStream();
                //listenerContext.Request.InputStream.CopyTo(ms);
                //var t = ms.ToArray();
                //Console.WriteLine(listenerContext.Request.ContentLength64);
                //Console.WriteLine(t.Length);
                //var requestBody = Image.FromStream(ms);

                var response = requestHandler.GetResponse(requestUrl, requestMethod, requestBody, cts.Token);

                listenerContext.Response.StatusCode = (int)response.statusCode;
                if (response.Image != null)
                    response.Image.Save(listenerContext.Response.OutputStream, System.Drawing.Imaging.ImageFormat.Png);
                
                //if (response.Image != null)
                //    using (var writer = new BinaryWriter(listenerContext.Response.OutputStream))
                //        writer.Write(response.GetImageAsByteArray());
            }
            catch (OperationCanceledException)
            {
                listenerContext.Response.StatusCode = (int)HttpStatusCode.GatewayTimeout;
            }
            catch (Exception ex)
            {
                listenerContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                Console.WriteLine(ex);
                Console.WriteLine("//////////////////////////////////////////////////////////");
                Console.WriteLine();
            }
            finally
            {
                sem.Release();
                cts = null;
                listenerContext.Response.Close();
            }
        }

        //private byte[] GetDataFromRequest(HttpListenerContext context)
        //{
        //    using (var reader = new BinaryReader(context.Request.InputStream))
        //    {
        //        return reader.ReadBytes(102400);
        //    }
        //}

        private readonly HttpListener listener;

        private Thread listenerThread;
        private bool disposed;
        private volatile bool isRunning;
        private RequestHandler requestHandler;
    }
}