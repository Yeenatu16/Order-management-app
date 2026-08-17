using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OrderManagementApp.Models;

namespace OrderManagementApp.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        await context.Database.MigrateAsync();

        // 1. Seed Roles (Admin & User)
        string[] roles = { "Admin", "User" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // 2. Seed Default Product Catalog in Ethiopian Birr (ETB)
        if (!await context.Products.AnyAsync())
        {
            context.Products.AddRange(
                new Product { Code = "P001", Name = "Wireless Headphones Pro", Price = 4500.00m, Description = "Premium ANC wireless headphones with 30hr battery." },
                new Product { Code = "P002", Name = "Mechanical Keyboard TKL", Price = 3200.00m, Description = "Tenkeyless tactile switches with RGB backlighting." },
                new Product { Code = "P003", Name = "27\" 4K Monitor IPS", Price = 28500.00m, Description = "Factory-calibrated 99% sRGB display with USB-C hub." },
                new Product { Code = "P004", Name = "USB-C Multi-Port Adapter", Price = 1850.00m, Description = "4K HDMI, 3x USB-A 3.0, SD card reader, 100W PD." }
            );
            await context.SaveChangesAsync();
        }
    }
}