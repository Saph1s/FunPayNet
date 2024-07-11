namespace FunPayNet.Api.Items;

/// <summary>
/// Lot model
/// </summary>
public class Lot
{
    public required int Id { get; set; }
    public required string Title { get; set; }
    public required string Price { get; set; }
    public int CategoryId { get; set; }
    public string? Server { get; set; }
    //public int? GameId { get; set; }
}