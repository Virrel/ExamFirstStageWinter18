using System;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Kontur.ImageTransformer.Classes;
using System.Threading;

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
                new UrlToHandlerMatch(@"^/process/flip-v/(-?\d+,){3}-?\d+$", FlipVertical)
            };
        }

        private class UrlToHandlerMatch
        {
            public string Pattern { get; }
            public Func<string, Bitmap, CancellationToken, Response> handler;

            public UrlToHandlerMatch(string pattern, Func<string, Bitmap, CancellationToken, Response> h)
            {
                Pattern = pattern;
                handler = h;
            }
        }

        public Response GetResponse(string Url, Bitmap image, CancellationToken token)
        {
            var t = uthm.FirstOrDefault(i => Regex.IsMatch(Url, i.Pattern));
            if (t == null)
                return new Response(HttpStatusCode.BadRequest, null);

            return t.handler(Url, image, token);
        }

        private Response Rotate90(string Url, Bitmap image, CancellationToken token)
        {
            Rectangle coordinates = GetRectangleFromUrl(Url);

            var intersection = Rectangle.Intersect(
                new Rectangle(0, 0, image.Height, image.Width),
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

            int destinationPosition = newWidth - 1;
            int destinationShiftX = newWidth - 1;
            int lineOffset = originalWidth * (originalHeight - shiftX - newWidth - 1);
            int heightMinShiftX = originalHeight - shiftX;
            int newHeightPlusShiftY = shiftY + newHeight;

            if (token.IsCancellationRequested)
                return new Response(HttpStatusCode.ServiceUnavailable, null);

            unsafe
            {
                int* originalPointer = (int*)originalData.Scan0.ToPointer();
                int* transfPntr = (int*)transfData.Scan0.ToPointer();

                for (int y = originalHeight - shiftX - newWidth; y < heightMinShiftX; ++y)
                {
                    //
                    lineOffset += originalWidth;
                    destinationPosition = destinationShiftX;
                    for (int x = shiftY; x < newHeightPlusShiftY; ++x)
                    {
                        int sourcePosition = (x + lineOffset);
                        transfPntr[destinationPosition] =
                            originalPointer[sourcePosition];
                        destinationPosition += newWidth;
                    }
                    --destinationShiftX;
                }
                //sourcePntr = null;
                //transfPntr = null;
                image.UnlockBits(originalData);
                transfBmp.UnlockBits(transfData);
                image.Dispose();
            }

            if (token.IsCancellationRequested)
                return new Response(HttpStatusCode.ServiceUnavailable, null);
            return new Response(HttpStatusCode.OK, transfBmp);

        }

        private Response Rotate270(string Url, Bitmap image, CancellationToken token)
        {
            Rectangle coordinates = GetRectangleFromUrl(Url);

            var intersection = Rectangle.Intersect(
                new Rectangle(0, 0, image.Height, image.Width),
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

            int destinationPosition = 0;
            int destinationShiftX = newWidth * (newHeight);
            int lineOffset = originalWidth * (shiftX - 1);
            int widthMinShiftY = originalWidth - shiftY;
            int shiftXPlusNewWidth = shiftX + newWidth;
            int widthMinShiftYMinNewHight = widthMinShiftY - newHeight;

            if (token.IsCancellationRequested)
                return new Response(HttpStatusCode.ServiceUnavailable, null);

            unsafe
            {
                int* originalPointer = (int*)originalData.Scan0.ToPointer();
                int* transfPntr = (int*)transfData.Scan0.ToPointer();

                for (int y = shiftX; y < shiftXPlusNewWidth; ++y)
                {
                    //
                    lineOffset += originalWidth;
                    destinationPosition = destinationShiftX;
                    for (int x = widthMinShiftYMinNewHight; x < widthMinShiftY; ++x)
                    {
                        int sourcePosition = (x + lineOffset);
                        destinationPosition -= newWidth;
                        transfPntr[destinationPosition] =
                            originalPointer[sourcePosition];
                    }
                    ++destinationShiftX;
                }
                //sourcePntr = null;
                //transfPntr = null;
                image.UnlockBits(originalData);
                transfBmp.UnlockBits(transfData);
                image.Dispose();
            }

            if (token.IsCancellationRequested)
                return new Response(HttpStatusCode.ServiceUnavailable, null);
            return new Response(HttpStatusCode.OK, transfBmp);

        }

        private Response FlipVertical(string Url, Bitmap image, CancellationToken token)
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

            int heightMinShiftY = originalHeight - shiftY;
            int heightMinShiftYMinnewHeight = heightMinShiftY - newHeight;
            int newWidthPlusShiftX = newWidth + shiftX;
            int lineOffset = originalWidth * (originalHeight - shiftY);
            int destinationPosition = 0;

            if (token.IsCancellationRequested)
                return new Response(HttpStatusCode.ServiceUnavailable, null);
            unsafe
            {
                int* sourcePntr = (int*)originalData.Scan0.ToPointer();
                int* transfPntr = (int*)transfData.Scan0.ToPointer();

                for (int y = heightMinShiftY; y > heightMinShiftYMinnewHeight; --y)
                {
                    //
                    lineOffset -= originalWidth;
                    for (int x = shiftX; x < newWidthPlusShiftX; ++x)
                    {
                        int sourcePosition = x + lineOffset;
                        transfPntr[destinationPosition] = sourcePntr[sourcePosition];
                        destinationPosition++;
                    }
                }
                //sourcePntr = null;
                //transfPntr = null;
                image.UnlockBits(originalData);
                transfBmp.UnlockBits(transfData);
                image.Dispose();
            }
            if (token.IsCancellationRequested)
                return new Response(HttpStatusCode.ServiceUnavailable, null);
            //var bmp = GetCroppedBitmap(transfBmp, intersection);
            return new Response(HttpStatusCode.OK, transfBmp);

        }

        private Response FlipHorizontal(string Url, Bitmap image, CancellationToken token)
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

            if (token.IsCancellationRequested)
                return new Response(HttpStatusCode.ServiceUnavailable, null);
            unsafe
            {
                int* sourcePntr = (int*)originalData.Scan0.ToPointer();
                int* transfPntr = (int*)transfData.Scan0.ToPointer();

                for (int y = shiftY; y < newHeigtPlusShiftY; ++y)
                {
                    //
                    lineOffset += originalWidth;
                    for (int x = widthMinShiftX; x > widthMinShiftXMinNewWidth; --x)
                    {
                        int sourcePosition = x + lineOffset - 1;
                        transfPntr[destinationPosition] = sourcePntr[sourcePosition];
                        destinationPosition++;
                    }
                }

                //sourcePntr = null;
                //transfPntr = null;
                image.UnlockBits(originalData);
                transfBmp.UnlockBits(transfData);
                image.Dispose();

                if (token.IsCancellationRequested)
                    return new Response(HttpStatusCode.ServiceUnavailable, null);
                //var bmp = GetCroppedBitmap(transfBmp, intersection);
                return new Response(HttpStatusCode.OK, transfBmp);
            }
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
