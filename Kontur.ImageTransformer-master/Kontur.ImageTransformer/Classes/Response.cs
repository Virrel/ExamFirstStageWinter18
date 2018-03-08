using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;

namespace Kontur.ImageTransformer.Classes
{
    class Response
    {
        public HttpStatusCode statusCode { get; }
        public Bitmap Image { get; }
        
        public Response(HttpStatusCode code, Bitmap image)
        {
            statusCode = code;
            Image = image;
        }
    }
}
