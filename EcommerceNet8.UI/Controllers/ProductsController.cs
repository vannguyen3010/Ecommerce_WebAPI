using Microsoft.AspNetCore.Mvc;

namespace EcommerceNet8.UI.Controllers
{
    public class ProductsController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
