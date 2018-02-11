﻿using System;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Kontur.ImageTransformer.Handlers
{
    class RequestHandler
    { 
        private static UrlToHandlerMatch[] uthm;
        public RequestHandler()
        {
            uthm = new[] { new UrlToHandlerMatch(@"^/process/sepia/(-?\d+,){3}-?\d+$", GetSepiaImage),
                new UrlToHandlerMatch(@"^/process/grayscale/(-?\d+,){3}-?\d+$", GetGrayScaleImage),
                new UrlToHandlerMatch(@"^/process/threshold\(\d{1,3}\)/(-?\d+,){3}-?\d+$", GetThresholdImage)
            };
        }

        private class UrlToHandlerMatch
        {
            public string Pattern { get; }
            public Func<string, Bitmap, Response> handler;

            public UrlToHandlerMatch( string pattern, Func<string, Bitmap, Response> h)
            {
                Pattern = pattern;
                handler = h;
            }
        }
        public Response GetResponse(string Url, Bitmap image)
        {
            var t = uthm.FirstOrDefault(i => Regex.IsMatch(Url, i.Pattern));
            if ( t == null )
                return new Response(HttpStatusCode.BadRequest, null);

            return t.handler(Url, image);
        }

        private Response GetSepiaImage(string Url, Bitmap image)
        {
            var c = GetRectangleFromUrl(Url);

            var intersection = Rectangle.Intersect(new Rectangle(0, 0, image.Width, image.Height), c);
            if (intersection.IsEmpty || intersection.Width == 0 || intersection.Height == 0)
                return new Response(HttpStatusCode.NoContent, null);

            image = GetCroppedBitmap(image, intersection);
            
            BitmapData bmp = image.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.ReadWrite, image.PixelFormat);

            byte[] argb = new byte[bmp.Stride * image.Height];
            Marshal.Copy(bmp.Scan0, argb, 0, argb.Length);

            int bytesPerPixel = Bitmap.GetPixelFormatSize(image.PixelFormat) / 8;
            int width = bmp.Width * bytesPerPixel;

            for (int y = 0; y < bmp.Height; ++y)
            {
                int line = y * bmp.Stride;
                for (int x = 0; x < width; x += bytesPerPixel)
                {
                    int b = argb[line + x];
                    int g = argb[line + x + 1];
                    int r = argb[line + x + 2];

                    int tr = (int)(0.393 * r + 0.769 * g + 0.189 * b);
                    int tg = (int)(0.349 * r + 0.686 * g + 0.168 * b);
                    int tb = (int)(0.272 * r + 0.534 * g + 0.131 * b);

                    r = tr > 255 ? 255 : tr;
                    g = tg > 255 ? 255 : tg;
                    b = tb > 255 ? 255 : tb;

                    argb[line + x] = (byte)b;
                    argb[line + x + 1] = (byte)g;
                    argb[line + x + 2] = (byte)r;
                }
            }
            Marshal.Copy(argb, 0, bmp.Scan0, argb.Length);
            image.UnlockBits(bmp);
            return new Response(HttpStatusCode.OK, image);
        }

        private Response GetGrayScaleImage(string Url, Bitmap image)
        {
            var c = GetRectangleFromUrl(Url);

            var intersection = Rectangle.Intersect(new Rectangle(0, 0, image.Width, image.Height), c);
            if (intersection.IsEmpty || intersection.Width == 0 || intersection.Height == 0)
                return new Response(HttpStatusCode.NoContent, null);

            image = GetCroppedBitmap(image, intersection);
            BitmapData bmp = image.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.ReadWrite, image.PixelFormat);

            byte[] argb = new byte[bmp.Stride * image.Height];
            Marshal.Copy(bmp.Scan0, argb, 0, argb.Length);

            int bytesPerPixel = Bitmap.GetPixelFormatSize(image.PixelFormat) / 8;
            int width = bmp.Width * bytesPerPixel;

            for (int y = 0; y < bmp.Height; ++y)
            {
                int line = y * bmp.Stride;
                for (int x = 0; x < width; x += bytesPerPixel)
                {
                    int b = argb[line + x];
                    int g = argb[line + x + 1];
                    int r = argb[line + x + 2];

                    r = g = b = (r + g + b) / 3;

                    argb[line + x] = (byte)b;
                    argb[line + x + 1] = (byte)g;
                    argb[line + x + 2] = (byte)r;
                }
            }
            Marshal.Copy(argb, 0, bmp.Scan0, argb.Length);
            image.UnlockBits(bmp);
            return new Response(HttpStatusCode.OK, image);
        }

        private Response GetThresholdImage(string Url, Bitmap image)
        {
            var c = GetRectangleFromUrl(Url);

            var intersection = Rectangle.Intersect(new Rectangle(0, 0, image.Width, image.Height), c);
            if (intersection.IsEmpty || intersection.Width == 0 || intersection.Height == 0)
                return new Response(HttpStatusCode.NoContent, null);

            Regex pattern = new Regex(@"\d{1,3}");
            MatchCollection m = pattern.Matches(Url);
            int requestedX = int.Parse(m[0].Value);

            image = GetCroppedBitmap(image, intersection);
            BitmapData bmp = image.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.ReadWrite, image.PixelFormat);

            byte[] argb = new byte[bmp.Stride * image.Height];
            Marshal.Copy(bmp.Scan0, argb, 0, argb.Length);

            int bytesPerPixel = Bitmap.GetPixelFormatSize(image.PixelFormat) / 8;
            int width = bmp.Width * bytesPerPixel;

            for (int y = 0; y < bmp.Height; ++y)
            {
                int line = y * bmp.Stride;
                for (int x = 0; x < width; x += bytesPerPixel)
                {
                    int b = argb[line + x];
                    int g = argb[line + x + 1];
                    int r = argb[line + x + 2];

                    int intensity = (r + g + b) / 3;

                    if (intensity >= 255 * requestedX / 100)
                        r = g = b = 255;
                    else
                        r = g = b = 0;

                    argb[line + x] = (byte)b;
                    argb[line + x + 1] = (byte)g;
                    argb[line + x + 2] = (byte)r;
                }
            }
            Marshal.Copy(argb, 0, bmp.Scan0, argb.Length);
            image.UnlockBits(bmp);
            return new Response(HttpStatusCode.OK, image);
        }
        
        private Bitmap GetCroppedBitmap(Bitmap img, Rectangle rec)
        {
            return new Bitmap(img).Clone(rec, img.PixelFormat);
        }

        private Rectangle GetRectangleFromUrl(string url)
        {
            var c = url.Split('/')[3]
                .Split(',')
                .Select(i => int.Parse(i))
                .ToArray();
            
            if (c[2] < 0)
            {
                c[0] = c[0] + c[2];
                c[2] = Math.Abs(c[2]);
            }
            if (c[3] < 0)
            {
                c[1] = c[1] + c[3];
                c[3] = Math.Abs(c[3]);
            }
            return new Rectangle(c[0], c[1], c[2], c[3]);
        }
    }
}
