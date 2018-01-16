using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Kontur.ImageTransformer.Handlers
{
    class RequestHandler
    {
        public RequestHandler()
        {

        }

        public Response GetResponse(string Url, string statusCode, string data)
        {
            return new Response(HttpStatusCode.OK, data);
        }
    }
}
