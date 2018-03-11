using Kontur.ImageTransformer.Handlers;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing.Imaging;
using System.Drawing;
using System.Collections.Generic;
using Kontur.ImageTransformer.Classes;
using System.Collections.Concurrent;
using System.Linq;

namespace Kontur.ImageTransformer
{
    internal class AsyncHttpServer : IDisposable
    {
        public AsyncHttpServer()
        {
            listener = new HttpListener();

            var taskPCore = 4;
            maxTasks = (Environment.ProcessorCount - 1) * taskPCore;
            maxRequests = maxTasks * 8;
            requestHandler = new RequestHandler();
            listenerContextQueue = new ConcurrentQueue<HttpListenerContext>();
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

                    consumerThread = new Thread(ConsumeQueue)
                    {
                        IsBackground = true,
                        Priority = ThreadPriority.AboveNormal
                    };
                    consumerThread.Start();

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
            //var maxRequests = maxTasks*2;
            //TaskFactory factory = new TaskFactory(_scheduler);
            //ThreadPool.SetMaxThreads(2, 2);
            //var veryLimitedScheduler = new VeryLimitedScheduler();
            //var factory = new TaskFactory(veryLimitedScheduler);
            var count = 0;
            while (listener.IsListening)
            {
                try
                {
                    //var context = listener.GetContext();
                    //DEFAULT WAY
                    //var cts = new CancellationTokenSource(500);
                    //Task.Run(() => HandleContextAsync(context, cts.Token), cts.Token);

                    //Semaphore
                    //var context = listener.GetContext();
                    //var cts = new CancellationTokenSource(500);
                    //Task.Run(() => {
                    //    sem.Wait();
                    //    HandleContextAsync(context, cts.Token);
                    //    sem.Release();
                    //}, cts.Token);

                    //Hand made
                    //var context = listener.GetContext();
                    //if (listenerContextQueue.Count() > maxRequests)// && countTask > maxTasks)
                    //{
                    //    context.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                    //    context.Response.Close();
                    //}
                    //else
                    //{
                    //    listenerContextQueue.Enqueue(context);
                    //}

                    //Last resort
                    if (listener.IsListening)
                    {
                        var context = listener.GetContext();
                        //bool consumed = false;
                        //if (lockTaken || listenerContextQueue.Count() > maxRequests)// && countTask > maxTasks)
                        //{
                        //    consumed = true;
                        //    Task.Run(() =>
                        //    {
                        //        context.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                        //        context.Response.Close();
                        //    });
                        //}
                        //else
                        //{
                        //    if (runningTasksCount < maxTasks)
                        //    {
                        //        consumed = true;
                        //        listenerContextQueue.Enqueue(context);
                        //    }
                        //    //++count;
                        //    //if (count > maxTasks)
                        //    //{
                        //    //    count = 0;
                        //    //    startProcessing = true;
                        //    //}
                        //}
                        //if (consumed == false)
                        if (runningTasksCount < maxTasks && lockTaken == false)
                        {
                            listenerContextQueue.Enqueue(context);
                        }
                        else
                        {

                            Task.Run(() =>
                            {
                                context.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                                context.Response.Close();
                            });
                        }
                    }

                    //Custom Collection
                    //var context = listener.GetContext();
                    //var cts = new CancellationTokenSource(500);
                    //list.Add(new Tuple<HttpListenerContext, DateTime, CancellationToken>(context, DateTime.Now, cts.Token));

                    //Task.Run(() => HandleContextAsync(context, cts.Token), cts.Token);

                    //Custom collection handler
                    //if (list.Count >= 50)
                    //    ProcessParallel();
                    //if( list.Count < 50 && !lockTaken)
                    //    list.Add(new Tuple<HttpListenerContext, CancellationToken>(context, token));
                    //else
                    //{
                    //    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    //    context.Response.Close();
                    //}

                    //Custom task scheduler
                    //        Task.Factory.StartNew(
                    //() => { HandleContextAsync(context); },
                    //cts.Token,
                    //TaskCreationOptions.None,
                    //this._scheduler);
                }
                catch (ThreadAbortException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }
        }

        private void ConsumeQueue()
        {
            HttpListenerContext context;
            while (true)
            {
                if (!listenerContextQueue.IsEmpty && runningTasksCount < maxTasks)
                {
                    lockTaken = true;
                    lock (listenerContextQueue)
                    {
                        //buffer = listenerContextQueue;
                        //listenerContextQueue = new ConcurrentQueue<HttpListenerContext>();
                        while (listenerContextQueue.TryDequeue(out context))
                        //for (int i = 0; i < listenerContextQueue.Count; ++i)
                        {
                            //listenerContextQueue.TryDequeue(out context);
                            HttpListenerContext localContext = context;
                            Interlocked.Increment(ref runningTasksCount);
                            Task.Run(() =>
                            {
                                var cts = new CancellationTokenSource(950);
                                HandleContextAsync(localContext, cts.Token);
                            }).ContinueWith(delegate { Interlocked.Decrement(ref runningTasksCount); }); ;
                        }
                    }
                    lockTaken = false;
                }
                else
                    Thread.Sleep(50);
            }
        }
    

        private void HandleContextAsync(HttpListenerContext listenerContext, CancellationToken token)
        {
            try
            {
                token.ThrowIfCancellationRequested();

                if (listenerContext.Request.HttpMethod != "POST")
                    throw new Exception("Not POST request");

                if (listenerContext.Request.ContentLength64 > 1024 * 100)
                    throw new Exception("Request body size higher than 100kb");
                
                var requestUrl = WebUtility.UrlDecode(listenerContext.Request.Url.AbsolutePath);

                using (var requestBody = new Bitmap(listenerContext.Request.InputStream))
                {
                    if (requestBody.Height > 1000 || requestBody.Width > 1000)
                        throw new Exception("Image width or height larger 1000px");

                    var response = requestHandler.GetResponse(requestUrl, requestBody, token);
                    listenerContext.Response.StatusCode = (int)response.statusCode;
                    if (response.Picture != null)
                        response.Picture.Save(listenerContext.Response.OutputStream, ImageFormat.Png);
                }
            }
            catch(OperationCanceledException)
            {
                listenerContext.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
            }
            catch (Exception ex)
            {
                listenerContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            }
            finally
            {
                listenerContext.Response.Close();
            }
        }
        private readonly HttpListener listener;
        
        private Thread listenerThread;
        private bool disposed;
        private volatile bool isRunning;

        private readonly int maxTasks;
        private readonly int maxRequests;
        private RequestHandler requestHandler;
        private ConcurrentQueue<HttpListenerContext> listenerContextQueue;
        private Thread consumerThread;
        private int runningTasksCount;
        private volatile bool lockTaken = false;
    }
}