namespace FunPayNet.Api.Utils;

/// <summary>
/// Order status
/// </summary>
public enum OrderStatus
{
    /// <summary>
    /// Order is outstanding
    /// </summary>
    Outstanding,

    /// <summary>
    /// Order is completed
    /// </summary>
    Completed,

    /// <summary>
    /// Order is refunded
    /// </summary>
    Refunded
}