using MySql.Data.MySqlClient;
using PegboardWebApp.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace PegboardWebApp.Controllers
{
    public class HomeController : Controller
    {
        TrackedRequestRepository _trackingService = new TrackedRequestRepository();

        public ActionResult Index(string id)
        {
            ViewBag.Title = "Home Page";

            if (!string.IsNullOrEmpty(id))
            {
                Session["TrackingId"] = id;
            }
            else
            {
                id = Session["TrackingId"] as string;
            }

            _trackingService.Insert(new Models.TrackedRequestModel()
            {
                RequestedResource = "Home Page",
                TrackingId = id,
                Timestamp = DateTime.Now,
                SourceIP = RequestHelper.GetClientIp(HttpContext)
            });

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }

        public ActionResult Download()
        {
            ViewBag.Message = "Downloads page.";

            return View();
        }
    }
}
