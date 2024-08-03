namespace FunPayNet.Api.Utils;

/// <summary>
/// Basic links
/// </summary>
public static class Links
{
    /// <summary>
    /// FunPay URL
    /// </summary>
    public static readonly string BaseUrl = "https://funpay.com";

    /// <summary>
    /// Orders URL
    /// </summary>
    public static readonly string OrdersUrl = $"{BaseUrl}/orders/trade";

    /// <summary>
    /// Update lot URL
    /// </summary>
    public static readonly string SaveLotUrl = $"{BaseUrl}/lots/offerSave";

    /// <summary>
    /// User profile URL
    /// </summary>
    public static readonly string UserUrl = $"{BaseUrl}/users";

    /// <summary>
    /// Raise lot URL
    /// </summary>
    public static readonly string RaiseUrl = $"{BaseUrl}/lots/raise";

    /// <summary>
    /// Runner URL
    /// </summary>
    public static readonly string RunnerUrl = $"{BaseUrl}/runner/";
}