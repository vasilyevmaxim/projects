using Microsoft.AspNetCore.Mvc;

namespace StudentTestingSystem.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return User.IsInRole("Teacher") 
                ? RedirectToAction("Index", "Teacher") 
                : RedirectToAction("Index", "Student");
        }

        return View();
    }
}
