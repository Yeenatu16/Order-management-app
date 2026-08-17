using OrderManagementApp.Models;

namespace OrderManagementApp.ViewModels;

public class DashboardViewModel
{
    public decimal TotalRevenue { get; set; }
    public int TotalOrders { get; set; }
    public int TotalProducts { get; set; }
    public decimal AverageOrderValue { get; set; }
    public List<Order> RecentOrders { get; set; } = new();
}