using System;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Kontur.ImageTransformer.Classes;

namespace Kontur.ImageTransformer.Handlers
{
    class RequestHandler
    { 
        private static UrlToHandlerMatch[] uthm;
        public RequestHandler()
        {
            uthm = new[] 
            {
                new UrlToHandlerMatch(@"^/process/rotate-cw/(-?\d+,){3}-?\d+$", Rotate90),
                new UrlToHandlerMatch(@"^/process/rotate-ccw/(-?\d+,){3}-?\d+$", Rotate270),
                new UrlToHandlerMatch(@"^/process/flip-h/(-?\d+,){3}-?\d+$", FlipHorizontal),
                new UrlToHandlerMatch(@"^/process/flip-v/(-?\d+,){3}-?\d+$", FlipVertical),
                new UrlToHandlerMatch(@"^/process/sendBack", SendBack)
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

        private Response SendBack(string Url, Bitmap image)
        {
            return new Response(HttpStatusCode.OK, image);
        }

        private Response Rotate90(string Url, Bitmap image)
        {
            var c = GetRectangleFromUrl(Url);

            var intersection = Rectangle.Intersect(new Rectangle(0, 0, image.Width, image.Height), c);
            if (intersection.IsEmpty || intersection.Width == 0 || intersection.Height == 0)
                return new Response(HttpStatusCode.NoContent, null);

            int newWidth = image.Height;
               int newHeight = image.Width;

            Bitmap transfBmp = new Bitmap(newWidth, newHeight, PixelFormat.Format32bppArgb);

            int originalWidth = image.Width;
            int originalHeight = image.Height;
            int newWidthMinusOne = newWidth - 1;
            int newHeightMinusOne = newHeight - 1;

            int yOffset = 0;
            int destinationY = 0;
            BitmapData originalData = image.LockBits(
                new Rectangle(0, 0, originalWidth, originalHeight),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);
            BitmapData transfData = transfBmp.LockBits(
                new Rectangle(0, 0, transfBmp.Width, transfBmp.Height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppArgb);

            unsafe
            {
                int* originalPointer = (int*)originalData.Scan0.ToPointer();
                int* transfPntr = (int*)transfData.Scan0.ToPointer();
                
                        yOffset = -originalWidth;
                        for (int y = 0; y < originalHeight; ++y)
                        {
                            yOffset += originalWidth;
                            int destinationX = newWidthMinusOne - y;
                            destinationY = -newWidth;
                            for (int x = 0; x < originalWidth; ++x)
                            {
                                int sourcePosition = (x + yOffset);
                                destinationY += newWidth;
                                int destinationPosition =
                                        (destinationX + destinationY);
                                transfPntr[destinationPosition] =
                                    originalPointer[sourcePosition];
                            }
                        }
                originalPointer = null;
                transfPntr = null;
                image.UnlockBits(originalData);
                transfBmp.UnlockBits(transfData);
                image.Dispose();

                return new Response(HttpStatusCode.OK,
                    GetCroppedBitmap(transfBmp, intersection));
            }
        }
        
        private Response Rotate270(string Url, Bitmap image)
        {
            var c = GetRectangleFromUrl(Url);

            var intersection = Rectangle.Intersect(new Rectangle(0, 0, image.Width, image.Height), c);
            if (intersection.IsEmpty || intersection.Width == 0 || intersection.Height == 0)
                return new Response(HttpStatusCode.NoContent, null);

            int newWidth = image.Height;
            int newHeight = image.Width;

            Bitmap transfBmp = new Bitmap(newWidth, newHeight, PixelFormat.Format32bppArgb);

            int originalWidth = image.Width;
            int originalHeight = image.Height;
            int newWidthMinusOne = newWidth - 1;
            int newHeightMinusOne = newHeight - 1;

            int yOffset = 0;
            int destinationY = 0;
            BitmapData originalData = image.LockBits(
                new Rectangle(0, 0, originalWidth, originalHeight),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);
            BitmapData transfData = transfBmp.LockBits(
                new Rectangle(0, 0, transfBmp.Width, transfBmp.Height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppArgb);

            unsafe
            {
                int* originalPointer = (int*)originalData.Scan0.ToPointer();
                int* transfPntr = (int*)transfData.Scan0.ToPointer();

                yOffset = -originalWidth;
                for (int y = 0; y < originalHeight; ++y)
                //Parallel.For(0, originalHeight, (y) =>
                {
                    int destinationX = y;
                    yOffset += originalWidth;
                    for (int x = 0; x < originalWidth; ++x)
                    {
                        int sourcePosition = (x + yOffset);
                        destinationY = newHeightMinusOne - x;
                        int destinationPosition =
                            (destinationX + destinationY * newWidth);
                        transfPntr[destinationPosition] =
                            originalPointer[sourcePosition];
                    }
                }
                originalPointer = null;
                transfPntr = null;
                image.UnlockBits(originalData);
                transfBmp.UnlockBits(transfData);
                image.Dispose();

                return new Response(HttpStatusCode.OK,
                    GetCroppedBitmap(transfBmp, intersection));
            }
        }

        private Response FlipVertical(string Url, Bitmap image)
        {
            var c = GetRectangleFromUrl(Url);

            var intersection = Rectangle.Intersect(new Rectangle(0, 0, image.Width, image.Height), c);
            if (intersection.IsEmpty || intersection.Width == 0 || intersection.Height == 0)
                return new Response(HttpStatusCode.NoContent, null);

            int newWidth = image.Width;
            int newHeight = image.Height;

            Bitmap transfBmp = new Bitmap(newWidth, newHeight, PixelFormat.Format32bppArgb);

            int originalWidth = image.Width;
            int originalHeight = image.Height;
            int newWidthMinusOne = newWidth - 1;
            int newHeightMinusOne = newHeight - 1;

            int yOffset = 0;
            int destinationY = 0;
            BitmapData originalData = image.LockBits(
                new Rectangle(0, 0, originalWidth, originalHeight),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);
            BitmapData transfData = transfBmp.LockBits(
                new Rectangle(0, 0, transfBmp.Width, transfBmp.Height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppArgb);

            unsafe
            {
                int* originalPointer = (int*)originalData.Scan0.ToPointer();
                int* transfPntr = (int*)transfData.Scan0.ToPointer();

                yOffset = -originalWidth;
                destinationY = originalWidth * originalHeight;
                for (int y = 0; y < originalHeight; ++y)
                //Parallel.For(0, originalHeight, (y) =>
                {
                    destinationY -= originalWidth;
                    yOffset += originalWidth;
                    for (int x = 0; x < originalWidth; ++x)
                    {
                        int sourcePosition = x + yOffset;
                        //destinationY = originalWidth - 1 - x;
                        int destinationPosition = x + destinationY;
                        transfPntr[destinationPosition] = originalPointer[sourcePosition];
                    }
                }
                originalPointer = null;
                transfPntr = null;
                image.UnlockBits(originalData);
                transfBmp.UnlockBits(transfData);
                image.Dispose();

                return new Response(HttpStatusCode.OK,
                    GetCroppedBitmap(transfBmp, intersection));
            }
        }
        
        private Response FlipHorizontal(string Url, Bitmap image)
        {
            Rectangle coordinates = GetRectangleFromUrl(Url);

            var intersection = Rectangle.Intersect(
                new Rectangle(0, 0, image.Width, image.Height),
                coordinates);

            if (intersection.IsEmpty 
                || intersection.Width == 0 
                || intersection.Height == 0)
                return new Response(HttpStatusCode.NoContent, null);

            int newWidth = intersection.Width;
            int newHeight = intersection.Height;
            int shiftX = intersection.X;
            int shiftY = intersection.Y;
            int originalWidth = image.Width;
            int originalHeight = image.Height;

            Bitmap transfBmp = new Bitmap(newWidth, newHeight, PixelFormat.Format32bppArgb);

            BitmapData originalData = image.LockBits(
                new Rectangle(0, 0, originalWidth, originalHeight),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);
            BitmapData transfData = transfBmp.LockBits(
                new Rectangle(0, 0, transfBmp.Width, transfBmp.Height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppArgb);

            int widthMinShiftX = originalWidth - shiftX;
            int widthMinShiftXMinNewWidth = widthMinShiftX - newWidth;
            int newHeigtPlusShiftY = newHeight + shiftY;
            int lineOffset = originalWidth * (shiftY - 1);
            int destinationPosition = 0;

            unsafe
            {
                int* sourcePntr = (int*)originalData.Scan0.ToPointer();
                int* transfPntr = (int*)transfData.Scan0.ToPointer();

                for (int y = shiftY; y < newHeigtPlusShiftY; ++y)
                {
                    lineOffset += originalWidth;
                    for (int x = widthMinShiftX; x > widthMinShiftXMinNewWidth; --x)
                    {
                        int sourcePosition = x + lineOffset - 1;
                        transfPntr[destinationPosition] = sourcePntr[sourcePosition];
                        destinationPosition++;
                    }
                }
                sourcePntr = null;
                transfPntr = null;
                image.UnlockBits(originalData);
                transfBmp.UnlockBits(transfData);
                image.Dispose();
                //var bmp = GetCroppedBitmap(transfBmp, intersection);
                return new Response(HttpStatusCode.OK, transfBmp);
            }
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
