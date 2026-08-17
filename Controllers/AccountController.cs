using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OrderManagementApp.ViewModels;

namespace OrderManagementApp.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IConfiguration _config;

    public AccountController(
        SignInManager<IdentityUser> signInManager,
        UserManager<IdentityUser> userManager,
        IConfiguration config)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _config = config;
    }

    [HttpGet]
    public IActionResult Login() => User.Identity?.IsAuthenticated == true ? RedirectToAction("Index", "Orders") : View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            ModelState.AddModelError(nameof(model.Email), "Invalid email address. No account found with this email.");
            return View(model);
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: false);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(nameof(model.Password), "Incorrect password.");
            return View(model);
        }

        await _signInManager.SignInAsync(user, isPersistent: model.RememberMe);

        if (await _userManager.IsInRoleAsync(user, "Admin"))
            return RedirectToAction("Index", "Home");

        return RedirectToAction("Index", "Orders");
    }

    [HttpGet]
    public IActionResult Register() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        if (model.Role == "Admin")
        {
            var validCode = _config["AdminSecurity:SecretCode"] ?? "ADMIN2026";
            if (string.IsNullOrWhiteSpace(model.AdminSecretCode) || model.AdminSecretCode.Trim() != validCode)
            {
                ModelState.AddModelError(nameof(model.AdminSecretCode), "Invalid Admin Authorization Code.");
                return View(model);
            }
        }

        var existingUser = await _userManager.FindByEmailAsync(model.Email);
        if (existingUser != null)
        {
            ModelState.AddModelError(nameof(model.Email), "An account with this email address is already registered.");
            return View(model);
        }

        var user = new IdentityUser { UserName = model.Email, Email = model.Email };
        var result = await _userManager.CreateAsync(user, model.Password);

        if (result.Succeeded)
        {
            var targetRole = model.Role == "Admin" ? "Admin" : "User";
            await _userManager.AddToRoleAsync(user, targetRole);
            await _signInManager.SignInAsync(user, isPersistent: false);

            return targetRole == "Admin" ? RedirectToAction("Index", "Home") : RedirectToAction("Index", "Orders");
        }

        foreach (var error in result.Errors)
        {
            if (error.Code.Contains("Password"))
                ModelState.AddModelError(nameof(model.Password), error.Description);
            else
                ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    [HttpGet]
    public IActionResult ForgotPassword() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            ModelState.AddModelError(nameof(model.Email), "Invalid email address. No account found.");
            return View(model);
        }

        TempData["ResetEmail"] = model.Email;
        TempData["SuccessMessage"] = "Verification Code generated! Use '123456' for verification.";
        return RedirectToAction("ResetPassword");
    }

    [HttpGet]
    public IActionResult ResetPassword()
    {
        var email = TempData["ResetEmail"] as string ?? string.Empty;
        return View(new ResetPasswordViewModel { Email = email });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        if (model.VerificationCode != "123456")
        {
            ModelState.AddModelError(nameof(model.VerificationCode), "Invalid 6-digit verification code. Please enter 123456.");
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            ModelState.AddModelError(nameof(model.Email), "Invalid email address.");
            return View(model);
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);

        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = "Password reset successfully. Please log in with your new password.";
            return RedirectToAction("Login");
        }

        foreach (var error in result.Errors)
        {
            if (error.Code.Contains("Password"))
                ModelState.AddModelError(nameof(model.NewPassword), error.Description);
            else
                ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Login", "Account");
    }
}