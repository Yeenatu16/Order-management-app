using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderManagementApp.Data;
using OrderManagementApp.Models;

namespace OrderManagementApp.Controllers;
//only Admin can create the products
[Authorize(Roles = "Admin")]
public class ProductsController : Controller
{
    private readonly ApplicationDbContext _context;

    public ProductsController(ApplicationDbContext context) => _context = context;

    public async Task<IActionResult> Index() => View(await _context.Products.ToListAsync());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save([FromBody] Product product)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (product.Id == 0)
        {
            _context.Products.Add(product);
        }
        else
        {
            var existing = await _context.Products.FindAsync(product.Id);
            if (existing == null) return NotFound();

            existing.Code = product.Code;
            existing.Name = product.Name;
            existing.Price = product.Price;
            existing.Description = product.Description;
        }

        await _context.SaveChangesAsync();
        return Ok(new { success = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var prod = await _context.Products.FindAsync(id);
        if (prod != null)
        {
            _context.Products.Remove(prod);
            await _context.SaveChangesAsync();
        }
        return Ok(new { success = true });
    }
}