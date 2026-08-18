using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using OrderManagementApp.Services;
using OrderManagementApp.ViewModels;

namespace OrderManagementApp.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IConfiguration _config;
    private readonly IEmailService _emailService;
    private readonly IMemoryCache _cache;

    public AccountController(
        SignInManager<IdentityUser> signInManager,
        UserManager<IdentityUser> userManager,
        IConfiguration config,
        IEmailService emailService,
        IMemoryCache cache)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _config = config;
        _emailService = emailService;
        _cache = cache;
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

        var otpCode = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
        var cacheKey = $"OTP_{model.Email.ToLowerInvariant()}";
        _cache.Set(cacheKey, otpCode, TimeSpan.FromMinutes(5));

        var emailBody = $@"
            <div style='font-family: Arial, sans-serif; padding: 20px; max-width: 500px; margin: auto; border: 1px solid #e2e8f0; border-radius: 12px;'>
                <h2 style='color: #1e293b;'>Password Reset Verification</h2>
                <p style='color: #64748b; font-size: 14px;'>Use the following 6-digit code to complete your password reset:</p>
                <div style='background-color: #f1f5f9; padding: 15px; text-align: center; border-radius: 8px; margin: 20px 0;'>
                    <span style='font-size: 32px; font-weight: bold; letter-spacing: 6px; color: #4f46e5; font-family: monospace;'>{otpCode}</span>
                </div>
                <p style='color: #94a3b8; font-size: 12px;'>This code will expire in 5 minutes. If you did not request this code, you can safely ignore this email.</p>
            </div>";

        try
        {
            await _emailService.SendEmailAsync(model.Email, "Your Password Reset Verification Code", emailBody);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, "Unable to send email. Please check your SMTP configuration. Error: " + ex.Message);
            return View(model);
        }

        TempData["ResetEmail"] = model.Email;
        TempData["SuccessMessage"] = "Verification code has been sent to your email.";
        return RedirectToAction("ResetPassword");
    }

    [HttpGet]
    public IActionResult ResetPassword()
    {
        var email = TempData["ResetEmail"] as string ?? string.Empty;
        if (string.IsNullOrEmpty(email)) return RedirectToAction("ForgotPassword");

        return View(new ResetPasswordViewModel { Email = email });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var cacheKey = $"OTP_{model.Email.ToLowerInvariant()}";
        if (!_cache.TryGetValue(cacheKey, out string? cachedOtp) || cachedOtp != model.VerificationCode.Trim())
        {
            ModelState.AddModelError(nameof(model.VerificationCode), "Invalid or expired verification code.");
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            ModelState.AddModelError(nameof(model.Email), "User account not found.");
            return View(model);
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);

        if (result.Succeeded)
        {
            _cache.Remove(cacheKey);
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