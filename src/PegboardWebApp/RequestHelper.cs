using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PegboardWebApp
{
    public class RequestHelper
    {
        public static string GetClientIp(HttpContextBase context)
        {
            string ip = context.Request.ServerVariables["HTTP_X_FORWARDED_FOR"];

            if (!string.IsNullOrEmpty(ip))
            {
                // May contain multiple IPs, client IP is the first one
                string[] addresses = ip.Split(',');
                if (addresses.Length > 0)
                {
                    return addresses[0].Trim();
                }
            }

            // Fall back to REMOTE_ADDR
            return context.Request.ServerVariables["REMOTE_ADDR"];
        }
    }
}