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
            maxRequests = maxTasks * 9;
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

                    handlerThread = new Thread(ProcessParallel)
                    {
                        IsBackground = true,
                        Priority = ThreadPriority.AboveNormal
                    };
                    handlerThread.Start();

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

                    //Last resort
                    if (listener.IsListening)
                    {
                        var context = listener.GetContext();
                        if (lockTaken || listenerContextQueue.Count() > maxRequests)// && countTask > maxTasks)
                        {
                            context.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                            context.Response.Close();
                        }
                        else
                        {
                            listenerContextQueue.Enqueue(context);
                            ++count;
                            if (count > maxTasks)
                            {
                                count = 0;
                                limitReached = true;
                            }
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

        ConcurrentQueue<HttpListenerContext> buffer;
        private void ProcessParallel()
        {
            HttpListenerContext context;
            var generationPassed = false;
            while (true)
            {
                if (!listenerContextQueue.IsEmpty)
                //if (limitReached || generationPassed)
                {
                    generationPassed = false;
                    limitReached = false;
                    lockTaken = true;
                    lock (listenerContextQueue)
                    {
                        buffer = listenerContextQueue;
                        listenerContextQueue = new ConcurrentQueue<HttpListenerContext>();
                    }
                    lockTaken = false;
                    while (buffer.TryDequeue(out context))
                    {
                        HttpListenerContext localContext = context;
                        Interlocked.Increment(ref runningTasksCount);
                        Task.Run(() =>
                        {
                            var cts = new CancellationTokenSource(900);
                            var task = HandleContextAsync(localContext, cts.Token);
                            var workTime = TimeSpan.FromMilliseconds(900);

                            if (!task.Wait(workTime))
                            {
                                cts.Cancel();
                                context.Response.StatusCode = (int)HttpStatusCode.OK;
                                context.Response.Close();
                            }

                        }).ContinueWith(delegate { Interlocked.Decrement(ref runningTasksCount); });
                        
                    }
                }
                else
                {
                    Thread.Sleep(50);
                    //if (!listenerContextQueue.IsEmpty)
                    //    generationPassed = true;
                }
            }
        }
    

        private async Task HandleContextAsync(HttpListenerContext listenerContext, CancellationToken token)
        {
            try
            {
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
                //Console.WriteLine(ex.ToString());
            }
            catch (Exception ex)
            {
                listenerContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                Console.WriteLine(ex.ToString());
            }
            finally
            {
                //Console.Error.WriteLine("Thread {0} complete", Thread.CurrentThread.ManagedThreadId);
                if(!token.IsCancellationRequested)
                    listenerContext.Response.Close();
                //Console.WriteLine("Elapsed {0} on thread {1}", sw.ElapsedMilliseconds, Task.CurrentId);
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
        private Thread handlerThread;
        private int runningTasksCount;
        private bool lockTaken = false;
        private bool limitReached = false;
    }
}