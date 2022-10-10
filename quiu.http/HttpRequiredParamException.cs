using System;
using System.Net;

namespace quiu.http
{
    public class HttpRequiredParamException
        : HttpListenerException
    {
        public HttpRequiredParamException ()
            : base(420, "Missing required parameter")
        { }

        public HttpRequiredParamException (string message)
            : base (420, message)
        { }
    }
}

