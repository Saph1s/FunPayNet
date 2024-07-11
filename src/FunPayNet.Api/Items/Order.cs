using FunPayNet.Api.Utils;

namespace FunPayNet.Api.Items;

/// <summary>
/// Order model
/// </summary>
public class Order
{
    public required string Id { get; set; }
    public required string Title { get; set; }
    public required float Price { get; set; }
    public required int CustomerId { get; set; }
    public required string CustomerUsername { get; set; }
    public required OrderStatus Status { get; set; }
}