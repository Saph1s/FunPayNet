namespace FunPayNet.Api.Items;

/// <summary>
/// UserLots model
/// </summary>
public class UserLots
{
    /// <summary>
    /// List of categories
    /// </summary>
    public required List<Category> Categories { get; set; }

    /// <summary>
    /// List of lots
    /// </summary>
    public required List<Lot> Lots { get; set; }
}