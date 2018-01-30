using System;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Drawing;
using System.Threading;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Kontur.ImageTransformer.Handlers
{
    class RequestHandler
    { 
        private UrlToHandlerMatch[] uthm;
        public RequestHandler()
        {
            uthm = new[] { new UrlToHandlerMatch(@"^/process/sepia/(-?\d+,){3}-?\d+$", GetSepiaImageAsync),
                new UrlToHandlerMatch(@"^/process/grayscale/(-?\d+,){3}-?\d+$", GetGrayScaleImageAsync),
                new UrlToHandlerMatch(@"^/process/threshold\(\d{1,3}\)/(-?\d+,){3}-?\d+$", GetThresholdImageAsync)
            };
        }

        private class UrlToHandlerMatch
        {
            public string Pattern { get; }
            public Func<string, Bitmap, CancellationToken, Task<Response>> handler;
            public UrlToHandlerMatch( string pattern, Func<string, Bitmap, CancellationToken, Task<Response>> h)
            {
                Pattern = pattern;
                handler = h;
            }
        }
        public async Task<Response> GetResponse(string Url, string code, Image image, CancellationToken cts)
        {
            var t = uthm.FirstOrDefault(i => Regex.IsMatch(Url, i.Pattern));
            //Console.WriteLine(Url);
            if (t == null)
                return new Response(HttpStatusCode.BadRequest, null);
            
            //Console.WriteLine(String.Format("W: {0}, H: {1}, D: {2}", image.Width, image.Height, image.PixelFormat));
            
            return await t.handler(Url, new Bitmap(image), cts);
        }

        private async Task<Response> GetSepiaImageAsync(string Url, Bitmap image, CancellationToken cts)
        {
            return await Task.Run(() =>
            {
                var c = GetRectangleFromUrl(Url);

                var gg = Rectangle.Intersect(new Rectangle(0, 0, image.Width, image.Height), c);
                if (gg.IsEmpty)
                    return new Response(HttpStatusCode.NoContent, null);

                image = GetCroppedBitmap(image, gg);
                BitmapData bmp = image.LockBits(new Rectangle(0, 0, image.Width, image.Height), System.Drawing.Imaging.ImageLockMode.ReadWrite, image.PixelFormat);
                int bytesPerPixel = Bitmap.GetPixelFormatSize(image.PixelFormat) / 8;
                int totalBytes = bmp.Stride * image.Height;
                byte[] argb = new byte[totalBytes];
                IntPtr firstPixelPtr = bmp.Scan0;
                Marshal.Copy(firstPixelPtr, argb, 0, argb.Length);

                int height = bmp.Height;
                int width = bmp.Width * bytesPerPixel;

                for (int y = 0; y < height; ++y)
                {
                    int curL = y * bmp.Stride;
                    for (int x = 0; x < width - bytesPerPixel; x += bytesPerPixel)
                    {
                        int b = argb[curL + x];
                        int g = argb[curL + x + 1];
                        int r = argb[curL + x + 2];

                        int tr = (int)(0.393 * r + 0.769 * g + 0.189 * b);
                        int tg = (int)(0.349 * r + 0.686 * g + 0.168 * b);
                        int tb = (int)(0.272 * r + 0.534 * g + 0.131 * b);

                        r = tr > 255 ? 255 : tr;
                        g = tg > 255 ? 255 : tg;
                        b = tb > 255 ? 255 : tb;

                        argb[curL + x] = (byte)b;
                        argb[curL + x + 1] = (byte)g;
                        argb[curL + x + 2] = (byte)r;
                    }
                }
                Marshal.Copy(argb, 0, firstPixelPtr, argb.Length);
                image.UnlockBits(bmp);
                return new Response(HttpStatusCode.OK, image);

                //for (int y = 0; y < height; y++)
                //{
                //    for (int x = 0; x < width; x++)
                //    {
                //        //get pixel value
                //        p = bmp.GetPixel(x, y);

                //        //extract pixel component ARGB
                //        int a = p.A;
                //        int r = p.R;
                //        int g = p.G;
                //        int b = p.B;

                //        //calculate temp value
                //        int tr = (int)(0.393 * r + 0.769 * g + 0.189 * b);
                //        int tg = (int)(0.349 * r + 0.686 * g + 0.168 * b);
                //        int tb = (int)(0.272 * r + 0.534 * g + 0.131 * b);

                //        //set new RGB value
                //        r = tr > 255 ? 255 : tr;

                //        g = tg > 255 ? 255 : tg;

                //        b = tb > 255 ? 255 : tb;

                //        //set the new RGB value in image pixel
                //        bmp.SetPixel(x, y, Color.FromArgb(a, r, g, b));
                //    }
                //}
                //return new Response(HttpStatusCode.OK, bmp);
            });
        }

        private async Task<Response> GetGrayScaleImageAsync_OLD(string Url, Bitmap image, CancellationToken cts)
        {
            return await Task.Run(() =>
            {
                var c = GetRectangleFromUrl(Url);

                var gg = Rectangle.Intersect(new Rectangle(0, 0, image.Width, image.Height), c);
                if (gg.IsEmpty)
                    return new Response(HttpStatusCode.NoContent, null);
                Bitmap bmp = GetCroppedBitmap(image, gg);
                image.Dispose();
                //if (bmp == null)
                //    return new Response(HttpStatusCode.ExpectationFailed, null);
                int width = bmp.Width;
                int height = bmp.Height;

                //color of pixel
                Color p;

                for (int y = 0; y < height; y++)
                {
                    if (cts.IsCancellationRequested)
                        return new Response(HttpStatusCode.GatewayTimeout, null);
                    for (int x = 0; x < width; x++)
                    {
                        //get pixel value
                        p = bmp.GetPixel(x, y);

                        //extract pixel component ARGB
                        int a = p.A;
                        int r = p.R;
                        int g = p.G;
                        int b = p.B;
                        
                        r = g = b = (r + g + b) / 3;

                        //set the new RGB value in image pixel
                        bmp.SetPixel(x, y, Color.FromArgb(a, r, g, b));
                    }
                }
                return new Response(HttpStatusCode.OK, bmp);
            });
        }

        private async Task<Response> GetGrayScaleImageAsync(string Url, Bitmap image, CancellationToken cts)
        {
            var c = GetRectangleFromUrl(Url);

            var gg = Rectangle.Intersect(new Rectangle(0, 0, image.Width, image.Height), c);
            if (gg.IsEmpty)
                return new Response(HttpStatusCode.NoContent, null);

            image = GetCroppedBitmap(image, gg);
            BitmapData bmp = image.LockBits(new Rectangle(0,0,image.Width, image.Height), System.Drawing.Imaging.ImageLockMode.ReadWrite, image.PixelFormat);
            int bytesPerPixel = Bitmap.GetPixelFormatSize(image.PixelFormat) / 8;
            int totalBytes = bmp.Stride * image.Height;
            byte[] argb = new byte[totalBytes];
            IntPtr firstPixelPtr = bmp.Scan0;
            Marshal.Copy(firstPixelPtr, argb, 0, argb.Length);

            int height = bmp.Height;
            int width = bmp.Width * bytesPerPixel;

            for (int y = 0; y < height; ++y)
            {
                int curL = y * bmp.Stride;
                for (int x = 0; x < width - bytesPerPixel; x+= bytesPerPixel)
                {
                    int b = argb[curL + x];
                    int g = argb[curL + x +1];
                    int r = argb[curL + x + 2];

                    r = g = b = (r + g + b) / 3;

                    argb[curL + x] = (byte)b;
                    argb[curL + x +1] = (byte)g;
                    argb[curL + x + 2] = (byte)r;
                }
            }
            Marshal.Copy(argb, 0, firstPixelPtr, argb.Length);
            image.UnlockBits(bmp);
            return new Response(HttpStatusCode.OK, image);
        }

        private async Task<Response> GetThresholdImageAsync(string Url, Bitmap image, CancellationToken cts)
        {
            var c = GetRectangleFromUrl(Url);

            var gg = Rectangle.Intersect(new Rectangle(0, 0, image.Width, image.Height), c);
            if (gg.IsEmpty)
                return new Response(HttpStatusCode.NoContent, null);

            Regex pattern = new Regex(@"\d{1,3}");
            MatchCollection m = pattern.Matches(Url);
            int requestedX = int.Parse(m[0].Value);

            image = GetCroppedBitmap(image, gg);
            BitmapData bmp = image.LockBits(new Rectangle(0, 0, image.Width, image.Height), System.Drawing.Imaging.ImageLockMode.ReadWrite, image.PixelFormat);
            int bytesPerPixel = Bitmap.GetPixelFormatSize(image.PixelFormat) / 8;
            int totalBytes = bmp.Stride * image.Height;
            byte[] argb = new byte[totalBytes];
            IntPtr firstPixelPtr = bmp.Scan0;
            Marshal.Copy(firstPixelPtr, argb, 0, argb.Length);

            int height = bmp.Height;
            int width = bmp.Width * bytesPerPixel;

            for (int y = 0; y < height; ++y)
            {
                int curL = y * bmp.Stride;
                for (int x = 0; x < width - bytesPerPixel; x += bytesPerPixel)
                {
                    int b = argb[curL + x];
                    int g = argb[curL + x + 1];
                    int r = argb[curL + x + 2];

                    int intensity = (r + g + b) / 3;

                    if (intensity >= 255 * requestedX / 100)
                        r = g = b = 255;
                    else
                        r = g = b = 0;

                    argb[curL + x] = (byte)b;
                    argb[curL + x + 1] = (byte)g;
                    argb[curL + x + 2] = (byte)r;
                }
            }
            Marshal.Copy(argb, 0, firstPixelPtr, argb.Length);
            image.UnlockBits(bmp);
            return new Response(HttpStatusCode.OK, image);

            ////int requestedX = int.Parse(m[0].Value.Substring(1, m[0].Value.Length - 2));

            ////color of pixel
            //Color p;

            //return await Task.Run(() =>
            //{
            //    for (int y = 0; y < height; y++)
            //    {
            //        if (cts.IsCancellationRequested)
            //            return new Response(HttpStatusCode.GatewayTimeout, null);
            //        for (int x = 0; x < width; x++)
            //        {
            //            //get pixel value
            //            p = bmp.GetPixel(x, y);

            //            //extract pixel component ARGB
            //            int a = p.A;
            //            int r = p.R;
            //            int g = p.G;
            //            int b = p.B;


            //            //set the new RGB value in image pixel
            //            bmp.SetPixel(x, y, Color.FromArgb(a, r, g, b));
            //        }
            //    }
            //    return new Response(HttpStatusCode.OK, bmp);
            //});
        }

        private Bitmap GetCroppedBitmap(Bitmap img, Rectangle rec)
        {
            return new Bitmap(img).Clone(rec, img.PixelFormat);
        }

        //private int[] GetParsedCoords(string url)
        //{
        //    try
        //    {
        //        var c = url.Split('/')[3]
        //            .Split(',')
        //            .Select(i => int.Parse(i))
        //            .ToArray();

        //        //Console.WriteLine(String.Format(@"In.  X: {0}, Y:{1}, W:{2}, H:{3}", c[0], c[1], c[2], c[3]));
        //        if (c[2] == 0 || c[3] == 0)
        //            return null;
        //        if (c[2] < 0)
        //        {
        //            c[2] = Math.Abs(c[2]);
        //            c[0] = c[0] - c[2];
        //        }
        //        if (c[3] < 0)
        //        {
        //            c[3] = Math.Abs(c[3]);
        //            c[1] = c[1] - c[3];
        //        }
        //        return c;
        //    }
        //    catch
        //    {
        //        return null;
        //    }
        //}

        private Rectangle GetRectangleFromUrl(string url)
        {
            var c = url.Split('/')[3]
                .Split(',')
                .Select(i => int.Parse(i))
                .ToArray();
            return new Rectangle(c[0], c[1], c[2], c[3]);
        }
    }
}
