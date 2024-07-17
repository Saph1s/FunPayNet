namespace FunPayNet.Api.Items;

/// <summary>
/// Lot model
/// </summary>
public class Lot
{
    /// <summary>
    /// Lot ID
    /// </summary>
    public required int Id { get; set; }

    /// <summary>
    /// Lot title
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Lot price
    /// </summary>
    public required int Price { get; set; }

    /// <summary>
    /// Lot category ID
    /// </summary>
    public required int CategoryId { get; set; }

    /// <summary>
    /// Lot category title
    /// </summary>
    public string? Server { get; set; }
    //public int? GameId { get; set; }
}