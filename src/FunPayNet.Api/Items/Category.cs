using FunPayNet.Api.Utils;

namespace FunPayNet.Api.Items;

/// <summary>
/// Category model
/// </summary>
public class Category
{
    /// <summary>
    /// Category ID
    /// </summary>
    public required int Id { get; set; }

    /// <summary>
    /// Game ID to which the category belongs
    /// </summary>
    public int? GameId { get; set; }

    /// <summary>
    /// Category title
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Link to the edit active lots page for this category
    /// </summary>
    public required string EditLotsUrl { get; set; }

    /// <summary>
    /// Link to all lots page for this category
    /// </summary>
    public required string AllLotsUrl { get; set; }

    /// <summary>
    /// Category type
    /// </summary>
    public required CategoryType Type { get; set; }
}