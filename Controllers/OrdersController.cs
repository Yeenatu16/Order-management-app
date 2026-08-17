using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderManagementApp.Data;
using OrderManagementApp.Models;
using OrderManagementApp.ViewModels;

namespace OrderManagementApp.Controllers;

[Authorize]
public class OrdersController : Controller
{
    private readonly ApplicationDbContext _context;

    public OrdersController(ApplicationDbContext context) => _context = context;

    // GET: /Orders
    public async Task<IActionResult> Index(string? search)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        IQueryable<Order> query = _context.Orders.Include(o => o.LineItems);

        // Standard Users can ONLY query their own orders.
        // Admins query orders from all users across the system.
        if (!User.IsInRole("Admin"))
        {
            query = query.Where(o => o.UserId == currentUserId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(o => o.OrderNumber.Contains(search) || 
                                     o.CustomerName.Contains(search) || 
                                     (User.IsInRole("Admin") && o.UserId.Contains(search)));
        }

        var orders = await query.OrderByDescending(o => o.OrderDate).ToListAsync();
        return View(orders);
    }

    // GET: /Orders/Create
    public async Task<IActionResult> Create()
    {
        ViewBag.Products = await _context.Products.ToListAsync();
        return View();
    }

    // POST: /Orders/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromBody] OrderCreateDto dto)
    {
        if (dto.Items == null || !dto.Items.Any())
            return BadRequest(new { message = "At least one product line item is required." });

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

        var productIds = dto.Items.Select(i => i.ProductId).Distinct().ToList();
        var catalog = await _context.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        var order = new Order
        {
            UserId = currentUserId,
            CustomerName = dto.CustomerName.Trim(),
            OrderNumber = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}",
            OrderDate = DateTime.UtcNow
        };

        decimal calculatedGrandTotal = 0;

        foreach (var item in dto.Items)
        {
            if (!catalog.TryGetValue(item.ProductId, out var product))
                return BadRequest(new { message = $"Invalid product ID: {item.ProductId}" });

            var subtotal = item.Quantity * product.Price;
            var vat = subtotal * 0.15m;
            var total = subtotal + vat;

            order.LineItems.Add(new OrderLineItem
            {
                ProductId = product.Id,
                Quantity = item.Quantity,
                UnitPrice = product.Price,
                Subtotal = subtotal,
                VAT = vat,
                Total = total
            });

            calculatedGrandTotal += total;
        }

        order.TotalAmount = calculatedGrandTotal;
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        return Ok(new { orderId = order.Id, orderNumber = order.OrderNumber });
    }

    // GET: /Orders/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var order = await _context.Orders
            .Include(o => o.LineItems)
            .ThenInclude(li => li.Product)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null) return NotFound();

        // Enforce data boundary: standard users cannot view other users' order details
        if (!User.IsInRole("Admin") && order.UserId != currentUserId)
        {
            return Forbid();
        }

        return View(order);
    }
}