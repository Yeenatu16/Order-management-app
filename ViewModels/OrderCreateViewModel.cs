using OrderManagementApp.Models;
namespace OrderManagementApp.ViewModels;

public class OrderCreateDto
{
    public string CustomerName { get; set; } = string.Empty;
    public List<OrderItemDto> Items { get; set; } = new();
}
public class OrderItemDto
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
}