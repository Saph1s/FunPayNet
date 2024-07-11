using FunPayNet.Api.Utils;

namespace FunPayNet.Api.Items;

/// <summary>
/// Category model
/// </summary>
public class Category
{
    public required int Id { get; set; }
    public int? GameId { get; set; }
    public required string Title { get; set; }
    public required string EditLotsUrl { get; set; }
    public required string AllLotsUrl { get; set; }
    public required CategoryType Type { get; set; }
}