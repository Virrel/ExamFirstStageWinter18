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
        public RequestHandler()
        {
            dd = new[] { @"^/process/sepia/\d+,\d+,\d+,\d+$" };
        }

        private string[] dd;

        public Response GetResponse(string Url, string statusCode, string data)
        {
            //if ( Regex.IsMatch(Url, h) )
            //{
            //    return new Response(HttpStatusCode.OK, null);
            //}
            //else
            //    return new Response(HttpStatusCode.BadRequest, null);
            if (!IsImageResolutionOk(data))
                return new Response(HttpStatusCode.BadRequest, null);
            if (!IsImageSizeOk(data))
                return new Response(HttpStatusCode.BadRequest, null);

            int[] coordinates = GetNormalizedCoords(Url);
            return new Response(HttpStatusCode.OK, data);
        }

        private bool IsImageResolutionOk(string data)
        {
            return true;
        }

        private bool IsImageSizeOk(string data)
        {
            return true;
        }

        private int[] GetNormalizedCoords(string url)
        {
            
            var coords = url.Split('/')[3]
                .Split(',')
                .Select(i => int.Parse(i))
                .ToArray();
            
            for (int i=0; i<4; ++i)
            {
                if (coords[i] < 0)
                    coords[i] = 0;
                else
                if (coords[i] > 99)
                    coords[i] = 99;
            }
            return coords;
        }
    }
}
