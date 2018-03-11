using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;

namespace Kontur.ImageTransformer.Classes
{
    class Response
    {
        public HttpStatusCode statusCode { get; }
        public Bitmap Picture { get; }
        
        public Response(HttpStatusCode code, Bitmap image)
        {
            statusCode = code;
            Picture = image;
        }
    }
}
