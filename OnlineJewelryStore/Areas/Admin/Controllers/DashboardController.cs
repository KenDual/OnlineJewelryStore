﻿using OnlineJewelryStore.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace OnlineJewelryStore.Areas.Admin.Controllers
{
    [AdminAuthorize]
    public class DashboardController : Controller
    {
        // GET: Admin/Dashboard
        public ActionResult Index()
        {
            return View();
        }
    }
}