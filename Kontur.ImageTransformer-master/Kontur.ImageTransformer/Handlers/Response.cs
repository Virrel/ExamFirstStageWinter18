using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Kontur.ImageTransformer.Handlers
{
    class Response
    {
        public HttpStatusCode statusCode { get; }
        public string Data { get; }

        public Response(HttpStatusCode code, string data)
        {
            statusCode = code;
            Data = data;
        }
    }
}
