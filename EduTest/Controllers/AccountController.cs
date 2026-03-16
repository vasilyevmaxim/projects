using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using StudentTestingSystem.Data;
using StudentTestingSystem.Models.ViewModels;
using StudentTestingSystem.Services;

namespace StudentTestingSystem.Controllers;

public class AccountController : Controller
{

    private readonly IAuthService _authService;
    private readonly AppDbContext _context;

    public AccountController(IAuthService authService, AppDbContext context)
    {
        _authService = authService;
        _context = context;
    }

    [HttpGet]
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", User.IsInRole("Teacher") ? "Teacher" : "Student");

        return View(new LoginViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _authService.AuthenticateStudentAsync(model.Email, model.Password);

        if (user == null)
        {
            ViewBag.Error = "Неверный email или пароль, либо аккаунт заблокирован";
            return View(model);
        }

        var principal = _authService.CreateClaimsPrincipal(user);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        return RedirectToAction("Index", "Student");
    }

    [HttpGet]
    public IActionResult TeacherLogin()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", User.IsInRole("Teacher") ? "Teacher" : "Student");

        return View(new LoginViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> TeacherLogin(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _authService.AuthenticateTeacherAsync(model.Email, model.Password);

        if (user == null)
        {
            ViewBag.Error = "Неверный email или пароль";
            return View(model);
        }

        var principal = _authService.CreateClaimsPrincipal(user);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        return RedirectToAction("Index", "Teacher");
    }

    [HttpGet]
    [HttpGet]
    public IActionResult Register()
    {
        var model = new RegisterViewModel
        {
            Groups = _context.Groups
                .OrderBy(g => g.Name)
                .Select(g => new SelectListItem
                {
                    Value = g.Id.ToString(),
                    Text = g.Name
                })
                .ToList()
        };

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.Groups = _context.Groups
                .OrderBy(g => g.Name)
                .Select(g => new SelectListItem
                {
                    Value = g.Id.ToString(),
                    Text = g.Name
                })
                .ToList();

            return View(model);
        }

        var (success, error) = await _authService.RegisterStudentAsync(
            model.FullName,
            model.Email,
            model.GroupNumber,
            model.Password,
            model.GroupId 
        );

        if (!success)
        {
            ViewBag.Error = error;

            model.Groups = _context.Groups
                .OrderBy(g => g.Name)
                .Select(g => new SelectListItem
                {
                    Value = g.Id.ToString(),
                    Text = g.Name
                })
                .ToList();

            return View(model);
        }

        TempData["Success"] = "Регистрация успешна! Войдите в систему.";
        return RedirectToAction("Login");
    }

    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View(new ForgotPasswordViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var success = await _authService.CreatePasswordResetRequestAsync(model.Email);

        if (success)
        {
            ViewBag.Success = "Запрос на восстановление пароля отправлен. Ожидайте подтверждения преподавателя.";
        }
        else
        {
            ViewBag.Error = "Пользователь с указанным email не найден.";
        }

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }
}
