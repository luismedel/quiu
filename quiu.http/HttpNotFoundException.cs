using System;
using System.Net;

namespace quiu.http
{
    public class HttpNotFoundException
        : HttpListenerException
    {
        public HttpNotFoundException()
            : base(404, "Not found")
        { }

        public HttpNotFoundException (string message)
            : base (404, message)
        { }
    }
}

