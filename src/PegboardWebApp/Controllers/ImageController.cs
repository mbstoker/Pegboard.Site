using PegboardWebApp.Models;
using PegboardWebApp.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Mvc;

namespace PegboardWebApp.Controllers
{
    public class ImageController : Controller
    {
        TrackedRequestRepository _trackingService = new TrackedRequestRepository();

        // GET: Image
        public ActionResult Index(string id)
        {
            int hypen = id.IndexOf('-');
            int lastDot = id.LastIndexOf('.');
            string trackingId = string.Empty;
            string imageName;
            if (hypen != -1 && lastDot != -1)
            {
                trackingId = id.Substring(hypen+1, lastDot-hypen-1);
                imageName = id.Substring(0, hypen) + id.Substring(lastDot);
            }
            else
            {
                imageName = id;
            }

            if (!string.IsNullOrEmpty(trackingId))
            {
                Session["TrackingId"] = id;
            }
            else
            {
                trackingId = Session["TrackingId"] as string;
            }

            if(!string.IsNullOrEmpty(trackingId))
            {
                string ip = HttpContext.Request.ServerVariables["REMOTE_ADDR"];
                _trackingService.Insert(new TrackedRequestModel() { RequestedResource = imageName, TrackingId = trackingId, Timestamp = DateTime.Now, SourceIP = RequestHelper.GetClientIp(HttpContext) });
            }

            var dir = Server.MapPath("/Images");
            var path = Path.Combine(dir, imageName);
            return base.File(path, "image/jpeg");
        }

        // GET: Sessions
        public JsonResult Requests(DateTime? minTime = null)
        {
            List<TrackedRequestModel> requests = _trackingService.GetAll(minTime); 

            return Json(requests, JsonRequestBehavior.AllowGet);
        }
    }
}