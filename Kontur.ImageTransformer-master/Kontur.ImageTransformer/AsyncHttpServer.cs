using Kontur.ImageTransformer.Handlers;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing.Imaging;
using System.Drawing;
using Kontur.ImageTransformer.Classes;
using ImageProcessor;

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
        private OrderTaskScheduler _scheduler = new OrderTaskScheduler("Name");

        private  void Listen()
        {
            //TaskFactory factory = new TaskFactory(_scheduler);
            ThreadPool.SetMaxThreads(2, 2);
            try
            {
                while (listener.IsListening)
                {
                    //DEFAULT WAY
                        var context = listener.GetContext();
                    Task.Run(() => HandleContextAsync(context));
                        //Task.Delay(100);
            //        var context = await listener.GetContextAsync();
            //        Task.Factory.StartNew(
            //() => { HandleContextAsync(context); },
            //CancellationToken.None,
            //TaskCreationOptions.None,
            //this._scheduler);
            //        Task.Delay(100);


                    //var context = */listener.BeginGetContext(new AsyncCallback(HandleContextAsync), listener).AsyncWaitHandle.WaitOne();
                    //var t = Task.Run(() => HandleContextAsync(context));
                    //RunWithTimeout(context, TimeSpan.FromMilliseconds(1000));
                    /*
                    if (GetRequestCount() > 100)
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        context.Response.Close();
                        //RemThread();
                    }
                    else
                    {
                        Task.Run(() => HandleContextAsync(context));
                        AddThread();
                        ///Метод ThreadPool.RegisterWaitForSingleObject 
                        //Action<object> action = (c) =>
                        //{
                        //    HandleContextAsync(c);
                        //};
                        //var task = new Task(action, context);
                        //task.Start();
                    }*/

                    //Console.WriteLine("concurrentThreadsCount = {0}; maxThreadsCount = {1}", GetRequestCount(), maxThreadsCount);
                }
            }
            catch (ThreadAbortException)
            {
                return;
            }
            catch (Exception)
            { }
        }

        private void Listen_original()
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
                          // HandleContextAsync(context);
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

        private int concurrentThreadsCount = 0;
        private int maxThreadsCount = 0;
        private System.Collections.Concurrent.BlockingCollection<bool> cq = new System.Collections.Concurrent.BlockingCollection<bool>();

        private int GetRequestCount()
        {
            return cq.Count;
        }

        private void AddThread()
        {
            //if (concurrentThreadsCount > maxThreadsCount)
            //    maxThreadsCount = concurrentThreadsCount;
            //concurrentThreadsCount += 1;
            cq.Add(false);
        }
        private void RemThread()
        {
            //--concurrentThreadsCount;
            cq.Take();
        }
        private void HandleContextAsync_mock(object Context)
        {
            var listenerContext = (HttpListenerContext)Context;
            listenerContext.Response.Close();
        }

        private async void HandleContextAsync(object Context)
        {
            var listenerContext =  (HttpListenerContext)Context;
            //AddThread();
            //Console.Error.WriteLine("Thread {0} started", Thread.CurrentThread.ManagedThreadId);
            //var listener = (HttpListener)Context.AsyncState;
            //var listenerContext = listener.EndGetContext(Context);
            try
            {
                
                if (listenerContext.Request.ContentLength64 > 1024 * 100)
                    throw new NotImplementedException("Request body size higher than 100kb");

                if (listenerContext.Request.HttpMethod != "POST")
                    throw new NotImplementedException("Not POST request");

                //if (listenerContext.Request.InputStream.Length <= 0)
                //    throw new NotImplementedException("Empty body");

                var requestUrl = WebUtility.UrlDecode(listenerContext.Request.Url.AbsolutePath);

                /*using (var bitmap = new Bitmap(listenerContext.Request.InputStream))
                //using (var image = Image.FromStream(listenerContext.Request.InputStream))
                {
                    var Rot = new Rotate();
                    var resultBitmap = await Rot.InternalRotateImage(UrlHandler.GetRotationType(requestUrl), bitmap);
                    //image.RotateFlip(UrlHandler.GetRotationType(requestUrl));
                    listenerContext.Response.StatusCode = (int)HttpStatusCode.OK;
                    //image.Save(listenerContext.Response.OutputStream, ImageFormat.Png);
                    resultBitmap.Save(listenerContext.Response.OutputStream, ImageFormat.Png);
                }*/
                /// OLD
                using (var requestBody = new Bitmap(listenerContext.Request.InputStream))
                {
                    var response = new RequestHandler().GetResponse(requestUrl, requestBody);
                    listenerContext.Response.StatusCode = (int)response.statusCode;
                    if (response.Image != null)
                        response.Image.Save(listenerContext.Response.OutputStream, ImageFormat.Png);
                }

            }
            catch (Exception ex)
            {
                listenerContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                Console.WriteLine(ex.ToString());
            }
            finally
            {
                //Console.Error.WriteLine("Thread {0} complete", Thread.CurrentThread.ManagedThreadId);
                try { listenerContext.Response.Close(); }
                catch{ }
                //Console.WriteLine("Elapsed {0} on thread {1}", sw.ElapsedMilliseconds, Task.CurrentId);
            }
        }

        private readonly HttpListener listener;
        
        private Thread listenerThread;
        private bool disposed;
        private volatile bool isRunning;
        private int maxThreads;
    }
}