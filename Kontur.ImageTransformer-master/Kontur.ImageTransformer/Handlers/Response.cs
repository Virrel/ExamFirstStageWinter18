using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Kontur.ImageTransformer.Handlers
{
    class Response
    {
        public HttpStatusCode statusCode { get; }
        public byte[] Data { get; }
        public System.Drawing.Image Image { get; }
        

        public Response(HttpStatusCode code, System.Drawing.Image image)
        {
            statusCode = code;
            Image = image;
        }

        public byte[] GetImageAsByteArray()
        {
            using (var ms = new MemoryStream())
            {
                Image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                return ms.ToArray();
            }
        }
    }
}
