using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderManagementApp.Data;
using OrderManagementApp.ViewModels;

namespace OrderManagementApp.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;

    public HomeController(ApplicationDbContext context) => _context = context;

    public async Task<IActionResult> Index()
    {
        if (!User.IsInRole("Admin"))
            return RedirectToAction("Index", "Orders");

        var orders = await _context.Orders.ToListAsync();
        var totalRev = orders.Sum(o => o.TotalAmount);

        var model = new DashboardViewModel
        {
            TotalOrders = orders.Count,
            TotalRevenue = totalRev,
            TotalProducts = await _context.Products.CountAsync(),
            AverageOrderValue = orders.Count > 0 ? totalRev / orders.Count : 0,
            RecentOrders = await _context.Orders
                .OrderByDescending(o => o.OrderDate)
                .Take(5)
                .ToListAsync()
        };

        return View(model);
    }
}