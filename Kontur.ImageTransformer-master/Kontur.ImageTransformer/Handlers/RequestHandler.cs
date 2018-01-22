using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Drawing;

namespace Kontur.ImageTransformer.Handlers
{
    class RequestHandler
    {
        private void Test()
        {
        }

        /// <summary>
        /// /////////////////////////////////////////
        /// </summary>
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
            public Func<string, Image, Task<Response>> handler;
            public UrlToHandlerMatch( string pattern, Func<string, Image, Task<Response>> h)
            {
                Pattern = pattern;
                handler = h;
            }
        }
        public async Task<Response> GetResponse(string Url, string code, Image image)
        {
            var t = uthm.FirstOrDefault(i => Regex.IsMatch(Url, i.Pattern));
            Console.WriteLine(Url);
            if (t == null)
                return new Response(HttpStatusCode.BadRequest, null);
            
            Console.WriteLine(String.Format("W: {0}, H: {1}, D: {2}", image.Width, image.Height, image.PixelFormat));
            //if ( (image.Height * image.Width * 48) >= (100 * 1024) )        //calculation is wrong yet
            //    return new Response(HttpStatusCode.BadRequest, null);

            //if (!IsImageResolutionOk(data))
            //    return new Response(HttpStatusCode.BadRequest, null);

            //if (!IsImageSizeOk(data))
            //    return new Response(HttpStatusCode.BadRequest, null);

            return await t.handler(Url, image);

            //return new Response(HttpStatusCode.OK, data);
        }

        private async Task<Response> GetSepiaImageAsync(string Url, Image image)
        {
            return await Task.Run(() =>
            {
                Bitmap bmp = new Bitmap(image);
                int width = bmp.Width;
                int height = bmp.Height;
                
                //color of pixel
                Color p;
                
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        //get pixel value
                        p = bmp.GetPixel(x, y);

                        //extract pixel component ARGB
                        int a = p.A;
                        int r = p.R;
                        int g = p.G;
                        int b = p.B;

                        //calculate temp value
                        int tr = (int)(0.393 * r + 0.769 * g + 0.189 * b);
                        int tg = (int)(0.349 * r + 0.686 * g + 0.168 * b);
                        int tb = (int)(0.272 * r + 0.534 * g + 0.131 * b);

                        //set new RGB value
                        r = tr > 255 ? 255 : tr;

                        g = tg > 255 ? 255 : tg;

                        b = tb > 255 ? 255 : tb;

                        //set the new RGB value in image pixel
                        bmp.SetPixel(x, y, Color.FromArgb(a, r, g, b));
                    }
                }
                return new Response(HttpStatusCode.OK, bmp);
            });
        }

        private async Task<Response> GetGrayScaleImageAsync(string Url, Image image)
        {
            return await Task.Run(() =>
            {
                var c = GetParsedCoords(Url);
                if (c == null)
                    return new Response(HttpStatusCode.NoContent, null);
                Rectangle rec = new Rectangle(0, 0, image.Width, image.Height);
                if (!rec.IntersectsWith(new Rectangle(c[0], c[1], c[2], c[3])))
                    return new Response(HttpStatusCode.NoContent, null);
                Bitmap bmp = CropImage(image, c[0], c[1], c[2], c[3]);
                Console.WriteLine(String.Format(@"Out. X: {0}, Y:{1}, W:{2}, H:{3}",c[0], c[1], c[2], c[3]));
                image.Dispose();
                //if (bmp == null)
                //    return new Response(HttpStatusCode.ExpectationFailed, null);
                int width = bmp.Width;
                int height = bmp.Height;

                //color of pixel
                Color p;

                for (int y = 0; y < height; y++)
                {
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

        private async Task<Response> GetThresholdImageAsync(string Url, Image image)
        {
            return await Task.Run(() =>
            {


                var c = GetParsedCoords_test(Url);
                Rectangle rec = new Rectangle(0, 0, image.Width, image.Height);
                var gg = Rectangle.Intersect(rec, c);
                if (gg.IsEmpty)
                    return new Response(HttpStatusCode.NoContent, null);
                Bitmap bmp = CropImage(image, c.X, c.Y, c.Width, c.Height);
                int width = bmp.Width;
                int height = bmp.Height;

                Regex pattern = new Regex(@"\d{1,3}");
                MatchCollection m = pattern.Matches(Url);
                int requestedX = int.Parse(m[0].Value);
                //int requestedX = int.Parse(m[0].Value.Substring(1, m[0].Value.Length - 2));

                //color of pixel
                Color p;

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        //get pixel value
                        p = bmp.GetPixel(x, y);

                        //extract pixel component ARGB
                        int a = p.A;
                        int r = p.R;
                        int g = p.G;
                        int b = p.B;

                        int intensity = (r + g + b) / 3;

                        if (intensity >= 255 * requestedX / 100)
                            r = g = b = 255;
                        else
                            r = g = b = 0;

                        //set the new RGB value in image pixel
                        bmp.SetPixel(x, y, Color.FromArgb(a, r, g, b));
                    }
                }
                return new Response(HttpStatusCode.OK, bmp);
            });
        }

        private Bitmap GetCroppedBitmap(Image img, int x, int y, int w, int h)
        {
            Rectangle rec = new Rectangle(x, y, w, h);
            return new Bitmap(img).Clone(rec, img.PixelFormat);
        }

        public Bitmap CropImage(Image img, int x, int y, int w, int h)
        {

            Rectangle section = new Rectangle(x, y, w, h);
            // An empty bitmap which will hold the cropped image
            Bitmap bmp = new Bitmap(section.Width, section.Height);

            Graphics g = Graphics.FromImage(bmp);

            // Draw the given area (section) of the source image
            // at location 0,0 on the empty bitmap (bmp)
            g.DrawImage(img, 0, 0, section, GraphicsUnit.Pixel);

            return bmp;
        }

        private int[] GetParsedCoords(string url)
        {
            try
            {
                var c = url.Split('/')[3]
                    .Split(',')
                    .Select(i => int.Parse(i))
                    .ToArray();

                Console.WriteLine(String.Format(@"In.  X: {0}, Y:{1}, W:{2}, H:{3}", c[0], c[1], c[2], c[3]));
                if (c[2] == 0 || c[3] == 0)
                    return null;
                if (c[2] < 0)
                {
                    c[2] = Math.Abs(c[2]);
                    c[0] = c[0] - c[2];
                }
                if (c[3] < 0)
                {
                    c[3] = Math.Abs(c[3]);
                    c[1] = c[1] - c[3];
                }
                return c;
            }
            catch
            {
                return null;
            }
        }

        private Rectangle GetParsedCoords_test(string url)
        {
            var c = url.Split('/')[3]
                .Split(',')
                .Select(i => int.Parse(i))
                .ToArray();

            return new Rectangle(c[0], c[1], c[2], c[3]);
        }
    }
}
