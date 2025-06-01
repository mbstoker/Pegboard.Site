using PegboardWebApp.Models;
using PegboardWebApp.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Http.Results;
using System.Web.Mvc;

namespace PegboardWebApp.Controllers
{
    public class DownloadController : Controller
    {
        TrackedRequestRepository _trackingService = new TrackedRequestRepository();

        public ActionResult Index(string id, string v)
        {
            string trackingId = id;
            if (!string.IsNullOrEmpty(trackingId))
            {
                Session["TrackingId"] = trackingId;
            }
            else
            {
                trackingId = Session["TrackingId"] as string;
            }

            var version = string.IsNullOrEmpty(v) ? "latest" : v;
            var filePath = GetFilePath(version);

            if(filePath == null)
            {
                return HttpNotFound("Requested version not found.");
            }

            string fileName = Path.GetFileName(filePath);

            _trackingService.Insert(new TrackedRequestModel() { RequestedResource = fileName, TrackingId = trackingId, Timestamp = DateTime.Now, SourceIP = RequestHelper.GetClientIp(HttpContext) });

            if (!System.IO.File.Exists(filePath))
                return HttpNotFound("Requested version not found.");

            var contentType = "application/octet-stream";
            return File(filePath, contentType, fileName);
        }

        // GET: Sessions
        public JsonResult Requests(DateTime? minTime = null)
        {
            List<TrackedRequestModel> requests = _trackingService.GetAll(minTime); 

            return Json(requests, JsonRequestBehavior.AllowGet);
        }


        private string GetFilePath(string version)
        {
            var dir = Server.MapPath("/Downloads");

            string filePath;
            if(version == "latest")
            {
                var files = Directory.GetFiles(dir, "ePegboardSetup_*.msi");
                Version highest = null;
                string latestFile = null;

                foreach (var file in files)
                {
                    var name = Path.GetFileNameWithoutExtension(file); // ePegboardSetup_1.3.0
                    var versionPart = name.Substring("ePegboardSetup_".Length);

                    if (Version.TryParse(versionPart, out Version parsedVersion))
                    {
                        if (highest == null || parsedVersion > highest)
                        {
                            highest = parsedVersion;
                            latestFile = Path.GetFileName(file);
                        }
                    }
                }

                if (latestFile == null)
                    return null;

                return Path.Combine(dir, latestFile);
            }
            else
            {
                return Path.Combine(dir, $"ePegboardSetup_{version}.msi");
            }
        }
    }
}