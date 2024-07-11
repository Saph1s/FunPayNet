namespace FunPayNet.Api.Items;

/// <summary>
/// UserLots model
/// </summary>
public class UserLots
{
    public required List<Category> Categories { get; set; }
    public required List<Lot> Lots { get; set; }
}