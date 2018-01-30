using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Kontur.ImageTransformer.Handlers
{
    class Response : IDisposable
    {
        public HttpStatusCode statusCode { get; }
        public byte[] Data { get; }
        public Bitmap Image { get; private set; }
        

        public Response(HttpStatusCode code, Bitmap image)
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

        public void Dispose()
        {
            Image.Dispose();
        }
    }
}
