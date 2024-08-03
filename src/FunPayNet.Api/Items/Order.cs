using FunPayNet.Api.Utils;

namespace FunPayNet.Api.Items;

/// <summary>
/// Order model
/// </summary>
public class Order
{
    /// <summary>
    /// Order ID
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Order title
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Order price
    /// </summary>
    public required float Price { get; set; }

    /// <summary>
    /// Order customer ID
    /// </summary>
    public required int CustomerId { get; set; }

    /// <summary>
    /// Order customer username
    /// </summary>
    public required string CustomerUsername { get; set; }

    /// <summary>
    /// Order status
    /// </summary>
    public required OrderStatus Status { get; set; }
}