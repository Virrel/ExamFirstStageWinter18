using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Kontur.ImageTransformer.Handlers
{
    class RequestHandler
    {
        private UrlToHandlerMatch[] uthm;
        public RequestHandler()
        {
            uthm = new[] { new UrlToHandlerMatch(@"^/process/sepia/(-?\d+,){3}-?\d+$", GetSepiaImageAsync),
                new UrlToHandlerMatch(@"^/process/grayscale/(-?\d+,){3}\d+$", GetGrayScaleImageAsync),
                new UrlToHandlerMatch(@"^/process/threshold\(\d{1,3}\)/(-?\d+,){3}-?\d+$", GetThresholdImageAsync)
            };
        }

        private class UrlToHandlerMatch
        {
            public string Pattern { get; }
            public Func<string, string, Task<Response>> handler;
            public UrlToHandlerMatch( string pattern, Func<string, string, Task<Response>> h)
            {
                Pattern = pattern;
                handler = h;
            }
        }

        public async Task<Response> GetResponse(string Url, string code, string data)
        {
            var t = uthm.FirstOrDefault(i => Regex.IsMatch(Url, i.Pattern));
            Console.WriteLine(Url);
            if (t == null)
                return new Response(HttpStatusCode.BadRequest, null);
            if (!IsImageResolutionOk(data))
                return new Response(HttpStatusCode.BadRequest, null);
            if (!IsImageSizeOk(data))
                return new Response(HttpStatusCode.BadRequest, null);

            return await t.handler(Url, data);

            //return new Response(HttpStatusCode.OK, data);
        }

        private async Task<Response> GetSepiaImageAsync(string Url, string image)
        {
            return await Task.Run(() =>
            {
                return new Response(HttpStatusCode.Accepted, "Sepia");
            });
        }

        private async Task<Response> GetGrayScaleImageAsync(string Url, string image)
        {
            return await Task.Run(() =>
            {
                return new Response(HttpStatusCode.Accepted, "GrayScale");
            });
        }

        private async Task<Response> GetThresholdImageAsync(string Url, string image)
        {
            return await Task.Run(() =>
            {
                return new Response(HttpStatusCode.Accepted, "Threshold");
            });
        }

        private bool IsImageResolutionOk(string data)
        {
            return true;
        }

        private bool IsImageSizeOk(string data)
        {
            return true;
        }

        private int[] GetParsedCoords(string url)
        {
            try
            {
                var coords = url.Split('/')[3]
                    .Split(',')
                    .Select(i => int.Parse(i))
                    .ToArray();
                for (int i = 0; i < 4; ++i)
                {
                    if (coords[i] < 0)
                        coords[i] = 0;
                    else
                    if (coords[i] > 99)
                        coords[i] = 99;
                }
                return coords;
            }
            catch (OverflowException)
            {
                return null;
            }
        }
    }
}
