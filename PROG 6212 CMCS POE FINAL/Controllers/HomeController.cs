using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PROG_6212_CMCS_POE_FINAL.Models;

namespace PROG_6212_CMCS_POE_FINAL.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
