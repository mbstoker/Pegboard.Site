using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PegboardWebSite
{
    public class RequestHelper
    {
        public static string GetClientIp(HttpContext context)
        {
            return context.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
        }
    }
}