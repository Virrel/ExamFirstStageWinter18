using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Drawing.Imaging;
using ImageProcessor;

namespace Kontur.ImageTransformer.Handlers
{
    static class UrlHandler
    {
        private static Dictionary<string, RotationType> patterns = new Dictionary<string, RotationType>();
        static UrlHandler()
        {
            //    patterns.Add(@"^/process/rotate-cw/(-?\d+,){3}-?\d+$", RotateFlipType.Rotate90FlipNone);
            //    patterns.Add(@"^/process/rotate-ccw/(-?\d+,){3}-?\d+$", RotateFlipType.Rotate180FlipNone);
            //    patterns.Add(@"^/process/flip-h/(-?\d+,){3}-?\d+$", RotateFlipType.RotateNoneFlipY);
            //    patterns.Add(@"^/process/flip-v/(-?\d+,){3}-?\d+$", RotateFlipType.RotateNoneFlipX);

            patterns.Add(@"^/process/rotate-cw/(-?\d+,){3}-?\d+$", RotationType.RotateCW);
            patterns.Add(@"^/process/rotate-ccw/(-?\d+,){3}-?\d+$", RotationType.RotateCCW);
            patterns.Add(@"^/process/flip-h/(-?\d+,){3}-?\d+$", RotationType.FlipH);
            patterns.Add(@"^/process/flip-v/(-?\d+,){3}-?\d+$", RotationType.FlipV);
        }

        public static RotationType GetRotationType(string url)
        {
            return patterns.First(i => Regex.IsMatch(url, i.Key)).Value;
        }
    }

    public enum RotationType
    {
        RotateCW = 90,
        RotateCCW = 270,
        FlipH,
        FlipV
    }

    public class Rotate
    {
        public async Task<Bitmap> InternalRotateImage(RotationType rotationType,
                                        Bitmap originalBitmap)
        {
            await Task.Delay(1);
            int newWidth, newHeight;
            if (rotationType == RotationType.RotateCW || rotationType == RotationType.RotateCCW)
            {
                newWidth = originalBitmap.Height;
                newHeight = originalBitmap.Width;
            }
            else
            {
                newWidth = originalBitmap.Width;
                newHeight = originalBitmap.Height;
            }

            Bitmap transfBmp = new Bitmap(newWidth, newHeight, PixelFormat.Format32bppArgb);

            int originalWidth = originalBitmap.Width;
            int originalHeight = originalBitmap.Height;

            // We're going to use the new width and height minus one a lot so lets 
            // pre-calculate that once to save some more time
            int newWidthMinusOne = newWidth - 1;
            int newHeightMinusOne = newHeight - 1;

            int yOffset = 0;
            int destinationY = 0;

            // To grab the raw bitmap data into a BitmapData object we need to
            // "lock" the data (bits that make up the image) into system memory.
            // We lock the source image as ReadOnly and the destination image
            // as WriteOnly and hope that the .NET Framework can perform some
            // sort of optimization based on this.
            // Note that this piece of code relies on the PixelFormat of the 
            // images to be 32 bpp (bits per pixel). We're not interested in 
            // the order of the components (red, green, blue and alpha) as 
            // we're going to copy the entire 32 bits as they are.
            BitmapData originalData = originalBitmap.LockBits(
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

                switch (rotationType)
                {
                    case RotationType.RotateCW:
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
                        break;
                    case RotationType.FlipH:
                        yOffset = -originalWidth;
                        for (int y = 0; y < originalHeight; ++y)
                        //Parallel.For(0, originalHeight, (y) =>
                        {
                            yOffset += originalWidth;
                            for (int x = 0; x < originalWidth; ++x)
                            {
                                int sourcePosition = x + yOffset;
                                destinationY = originalWidth - 1 - x;
                                int destinationPosition = yOffset + destinationY;
                                transfPntr[destinationPosition] = originalPointer[sourcePosition];
                            }
                        }
                        break;
                    case RotationType.RotateCCW:
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
                        break;
                    case RotationType.FlipV:
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
                        break;
                }
                originalPointer = null;
                transfPntr = null;
                // We have to remember to unlock the bits when we're done.
                originalBitmap.UnlockBits(originalData);
                transfBmp.UnlockBits(transfData);
                originalBitmap.Dispose();

                return transfBmp;
            }
        }
    }
}
