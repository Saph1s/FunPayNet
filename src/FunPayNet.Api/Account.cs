using System.Globalization;
using System.Net;
using System.Web;
using FunPayNet.Api.Items;
using FunPayNet.Api.Utils;
using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FunPayNet.Api;

/// <summary>
/// Account model
/// </summary>
public class Account
{
    /// <summary>
    /// Account id
    /// </summary>
    public required int Id { get; set; }

    /// <summary>
    /// Username
    /// </summary>
    public required string Username { get; set; }

    /// <summary>
    /// Current balance
    /// </summary>
    public float Balance { get; set; }

    /// <summary>
    /// Account currency
    /// </summary>
    public string? Currency { get; set; }

    /// <summary>
    /// Number of active orders
    /// </summary>
    public int ActiveOrders { get; set; }

    /// <summary>
    /// golden_key
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// CSRF token
    /// </summary>
    public string? CsrfToken { get; set; }

    /// <summary>
    /// PHPSESSID
    /// </summary>
    private string? SessionId { get; set; }

    /// <summary>
    /// Saved chats
    /// </summary>

    public string? SavedChats { get; set; }

    /// <summary>
    /// Last updated
    /// </summary>
    public DateTime LastUpdated { get; set; }

    /// <summary>
    /// Get Account
    /// </summary>
    /// <param name="token">golden_key</param>
    /// <param name="timeout">Response timeout</param>
    /// <returns>Account instance</returns>
    /// <exception cref="Exception"></exception>
    public static async Task<Account> GetAccount(string token, double timeout = 10.0)
    {
        var httpHandler = new HttpClientHandler
        {
            CookieContainer = new CookieContainer(),
            UseCookies = true
        };
        var httpClient = new HttpClient(httpHandler);
        httpClient.Timeout = TimeSpan.FromSeconds(timeout);
        httpClient.DefaultRequestHeaders.Add("Cookie", $"golden_key={token}");
        var response = await httpClient.GetAsync($"{Links.BaseUrl}");
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception("Failed to get account info");
        }

        var htmlContent = await response.Content.ReadAsStringAsync();
        var htmlDocument = new HtmlDocument();
        htmlDocument.LoadHtml(htmlContent);

        var usernameNode = htmlDocument.DocumentNode.SelectSingleNode("//div[@class='user-link-name']");
        if (usernameNode == null)
        {
            throw new Exception("Failed to get username");
        }

        var username = usernameNode.InnerText;

        var bodyNode = htmlDocument.DocumentNode.SelectSingleNode("//body");
        var appDataJson = bodyNode?.GetAttributeValue("data-app-data", null);
        if (appDataJson == null) throw new Exception("data-app-data not found");


        var decodedAppData = HttpUtility.HtmlDecode(appDataJson);
        var appData = JObject.Parse(decodedAppData);
        var userId = (appData["userId"] ?? throw new Exception("Failed to parse User ID")).Value<int>();
        var csrfToken = (appData["csrf-token"] ?? throw new Exception("Failed to parse CSRF token")).Value<string>();

        var activeSalesNode = htmlDocument.DocumentNode.SelectSingleNode("//span[@class='badge badge-trade']");
        var activeSales = activeSalesNode != null ? int.Parse(activeSalesNode.InnerText) : 0;

        var balanceNode = htmlDocument.DocumentNode.SelectSingleNode("//span[@class='badge badge-balance']");
        float balance = 0;
        string? currency = null;
        if (balanceNode != null)
        {
            var balanceParts = balanceNode.InnerText.Split(' ');
            balance = balanceParts.Length > 0 ? float.Parse(balanceParts[0]) : 0;
            currency = balanceParts.Length > 1 ? balanceParts[1] : null;
        }

        var cookies = httpHandler.CookieContainer.GetCookies(new Uri(Links.BaseUrl));
        var sessionId = cookies["PHPSESSID"]?.Value;

        return new Account
        {
            Id = userId,
            Username = username,
            Balance = balance,
            Currency = currency,
            ActiveOrders = activeSales,
            Key = token,
            CsrfToken = csrfToken,
            SessionId = sessionId,
            LastUpdated = DateTime.Now
        };
    }

    /// <summary>
    /// Get account orders
    /// </summary>
    /// <param name="excludeOrders">List of orders to exclude</param>
    /// <param name="includeOutstanding">Include outstanding orders in the list</param>
    /// <param name="includeCompleted">Include completed orders in the list</param>
    /// <param name="includeRefunded">Include refunded orders in the list</param>
    /// <param name="timeout">Response timeout</param>
    /// <returns>List of orders</returns>
    /// <exception cref="Exception"></exception>
    public async Task<List<Order>> GetOrders(List<string> excludeOrders, bool includeOutstanding = true,
        bool includeCompleted = false, bool includeRefunded = false, double timeout = 10.0)
    {
        var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(timeout)
        };
        var cookieString = SessionId != null ? $"golden_key={Key}; PHPSESSID={SessionId}" : $"golden_key={Key}";
        httpClient.DefaultRequestHeaders.Add("Cookie", cookieString);
        var response = await httpClient.GetAsync($"{Links.OrdersUrl}");
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception("Failed to get orders");
        }

        var htmlContent = await response.Content.ReadAsStringAsync();
        var htmlDocument = new HtmlDocument();
        htmlDocument.LoadHtml(htmlContent);

        if (htmlDocument.DocumentNode.SelectSingleNode("//div[@class='user-link-name']") == null)
        {
            throw new Exception("Invalid token");
        }

        var ordersNodes = htmlDocument.DocumentNode.SelectNodes("//a[@class='tc-item']");
        if (ordersNodes == null)
        {
            return new List<Order>();
        }

        var orders = new List<Order>();
        foreach (var orderNode in ordersNodes)
        {
            OrderStatus status;
            var orderClassName = orderNode.GetAttributeValue("class", null);
            if (orderClassName.Contains("warning") && includeRefunded)
            {
                status = OrderStatus.Refunded;
            }
            else if (orderClassName.Contains("info") && includeOutstanding)
            {
                status = OrderStatus.Outstanding;
            }
            else if (includeCompleted)
            {
                status = OrderStatus.Completed;
            }
            else
            {
                continue;
            }

            var orderId = htmlDocument.DocumentNode.SelectSingleNode("//div[@class='tc-order']")?.InnerText ??
                          throw new Exception("Failed to get order ID");
            if (excludeOrders.Contains(orderId)) continue;
            var title = orderNode.SelectSingleNode("//div[@class='order-desc']//div")?.InnerText;
            var price = float.Parse(orderNode.SelectSingleNode("//div[@class='tc-price']")?.InnerText.Split(" ")[0] ??
                                    throw new Exception("Failed to get order price"));

            var customer = htmlDocument.DocumentNode.SelectSingleNode("//div[@class='media-user-name']//span");
            var customerName = customer?.InnerText ?? "Unkown";
            var customerId = int.Parse(customer?.GetAttributeValue("data-href", null).TrimEnd('/').Split('/').Last() ??
                                       throw new Exception("Failed to get customer ID"));

            var order = new Order
            {
                Id = orderId,
                Title = title ?? string.Empty,
                Price = price,
                CustomerId = customerId,
                CustomerUsername = customerName,
                Status = status
            };
            orders.Add(order);
        }

        return orders;
    }

    /// <summary>
    /// Get lot info
    /// </summary>
    /// <param name="lotId">Lot ID</param>
    /// <param name="gameId">Game ID</param>
    /// <returns>Dictionary with name and value fields</returns>
    /// <exception cref="Exception"></exception>
    public async Task<Dictionary<string, string>> GetLotInfo(int lotId, int gameId)
    {
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("Cookie", $"golden_key={Key}; PHPSESSID={SessionId}");
        httpClient.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
        httpClient.DefaultRequestHeaders.Add("Accept", "*/*");
        httpClient.DefaultRequestHeaders.Add("Content-Type", "application/json");

        var generatedTag = Helpers.GenerateRandomTag();
        var payload = new
        {
            tag = generatedTag,
            offer = lotId,
            node = gameId
        };
        var response =
            await httpClient.GetAsync(
                $"{Links.BaseUrl}/lots/offerEdit?tag={payload.tag}&offer={payload.offer}&node={payload.node}");
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception("Failed to get lot info");
        }

        var htmlContent = await response.Content.ReadAsStringAsync();
        var htmlDocument = new HtmlDocument();
        htmlDocument.LoadHtml(htmlContent);

        var lotFormNode = htmlDocument.DocumentNode.SelectSingleNode("//form[@class='form-offer-editor']");
        var inputFields = lotFormNode.SelectNodes("//input");
        var textFields = lotFormNode.SelectNodes("//textarea");
        var selectFields = lotFormNode.SelectNodes("//select");
        var list = new Dictionary<string, string>();
        foreach (var inputField in inputFields)
        {
            var name = inputField.GetAttributeValue("name", null);
            var value = inputField.GetAttributeValue("value", null);
            list.Add(name, value);
        }

        foreach (var textField in textFields)
        {
            var name = textField.GetAttributeValue("name", null);
            var text = textField.InnerText ?? string.Empty;
            list.Add(name, text);
        }

        foreach (var selectField in selectFields)
        {
            var name = selectField.GetAttributeValue("name", null);
            var value = selectField.SelectSingleNode("//option[@selected]")?.GetAttributeValue("value", null) ??
                        "UNKNOWN";
            list.Add(name, value);
        }

        return list;
    }

    /// <summary>
    /// Get the possible fields of the lot
    /// </summary>
    /// <param name="nodeId"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public async Task<Dictionary<string, string>> GetLotFields(int nodeId)
    {
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("Cookie", $"golden_key={Key}; PHPSESSID={SessionId}");
        httpClient.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
        httpClient.DefaultRequestHeaders.Add("Accept", "*/*");
        httpClient.DefaultRequestHeaders.Add("Content-Type", "application/json");

        var response = await httpClient.GetAsync($"{Links.BaseUrl}/lots/offerEdit?node={nodeId}");
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception("Failed to get lot fields");
        }

        var htmlContent = await response.Content.ReadAsStringAsync();
        var htmlDocument = new HtmlDocument();
        htmlDocument.LoadHtml(htmlContent);

        var lotFormNode = htmlDocument.DocumentNode.SelectSingleNode("//form[@class='form-offer-editor']");
        var inputFields = lotFormNode.SelectNodes("//input");
        var textFields = lotFormNode.SelectNodes("//textarea");
        var selectFields = lotFormNode.SelectNodes("//select");
        var list = new Dictionary<string, string>();
        foreach (var inputField in inputFields)
        {
            var name = inputField.GetAttributeValue("name", null);
            var value = inputField.GetAttributeValue("value", null);
            list.Add(name, value);
        }

        foreach (var textField in textFields)
        {
            var name = textField.GetAttributeValue("name", null);
            var text = textField.InnerText ?? string.Empty;
            list.Add(name, text);
        }

        foreach (var selectField in selectFields)
        {
            var name = selectField.GetAttributeValue("name", null);
            var options = selectField.SelectNodes("//option");
            var value = "";
            foreach (var option in options)
            {
                if (option.GetAttributeValue("value", null) == null) continue;
                value += option.GetAttributeValue("value", null) + "#";
            }

            list.Add(name, value);
        }

        return list;
    }

    /// <summary>
    /// Get game id of this category
    /// </summary>
    /// <param name="category">Category instance</param>
    /// <param name="timeout">Response timeout</param>
    /// <returns>Game ID</returns>
    /// <exception cref="Exception"></exception>
    public async Task<int> GetCategoryGameId(Category category, double timeout = 10.0)
    {
        var link = category.Type == CategoryType.Currency
            ? $"{Links.BaseUrl}/chips/{category.Id}/trade"
            : $"{Links.BaseUrl}/lots/{category.Id}/trade";
        var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(timeout)
        };
        httpClient.DefaultRequestHeaders.Add("Cookie", $"golden_key={Key}");
        var response = await httpClient.GetAsync(link);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new Exception("Category not found");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception("Failed to get category game id");
        }

        var htmlContent = await response.Content.ReadAsStringAsync();
        var htmlDocument = new HtmlDocument();
        htmlDocument.LoadHtml(htmlContent);

        var user = htmlDocument.DocumentNode.SelectSingleNode("//div[@class='user-link-name']");
        if (user == null)
        {
            throw new Exception("Invalid token");
        }

        var gameId = category.Type == CategoryType.Currency
            ? int.Parse(
                htmlDocument.DocumentNode.SelectSingleNode("//input[@name='game']")?.GetAttributeValue("value", null) ??
                throw new Exception("Failed to get game id"))
            : int.Parse(
                htmlDocument.DocumentNode.SelectSingleNode("//div[@class='col-sm-6']//button")
                    ?.GetAttributeValue("data-game", null) ?? throw new Exception("Failed to get game id"));
        return gameId;
    }

    /// <summary>
    /// Change lot state
    /// </summary>
    /// <param name="lotId">Lot ID</param>
    /// <param name="gameId">Game ID</param>
    /// <param name="state">State (on/off)</param>
    /// <returns>Response to request</returns>
    /// <exception cref="Exception"></exception>
    public async Task<string> ChangeLotState(int lotId, int gameId, bool state = true)
    {
        var lotInfo = await GetLotInfo(lotId, gameId);
        var httpClient = new HttpClient();
        if (state)
        {
            lotInfo["active"] = "on";
        }
        else
        {
            lotInfo.Remove("active");
        }

        lotInfo.Add("location", "trade");

        httpClient.DefaultRequestHeaders.Add("Cookie", $"golden_key={Key}; PHPSESSID={SessionId}");
        httpClient.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
        httpClient.DefaultRequestHeaders.Add("Accept", "*/*");
        httpClient.DefaultRequestHeaders.Add("Content-Type", "application/x-www-form-urlencoded; charset=UTF-8");

        var content = new FormUrlEncodedContent(lotInfo);
        var response = await httpClient.PostAsync($"{Links.BaseUrl}/lots/offerSave", content);
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception("Failed to change lot state");
        }

        var result = await response.Content.ReadAsStringAsync();
        return result;
    }

    /// <summary>
    /// Change lot price
    /// </summary>
    /// <param name="lotId">Lot ID</param>
    /// <param name="gameId">Game ID</param>
    /// <param name="price">New price</param>
    /// <returns>Response to request</returns>
    /// <exception cref="Exception"></exception>
    public async Task<string> ChangeLotPrice(int lotId, int gameId, float price)
    {
        var lotInfo = await GetLotInfo(lotId, gameId);
        var httpClient = new HttpClient();
        lotInfo["price"] = price.ToString(CultureInfo.InvariantCulture);

        httpClient.DefaultRequestHeaders.Add("Cookie", $"golden_key={Key}; PHPSESSID={SessionId}");
        httpClient.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
        httpClient.DefaultRequestHeaders.Add("Accept", "*/*");
        httpClient.DefaultRequestHeaders.Add("Content-Type", "application/x-www-form-urlencoded; charset=UTF-8");

        var content = new FormUrlEncodedContent(lotInfo);
        var response = await httpClient.PostAsync($"{Links.SaveLotUrl}", content);
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception("Failed to change lot price");
        }

        var result = await response.Content.ReadAsStringAsync();
        return result;
    }

    /// <summary>
    /// Create new lot
    /// </summary>
    /// <param name="lotId"></param>
    /// <param name="fields"></param>
    /// <returns>Response to request</returns>
    /// <exception cref="Exception"></exception>
    public async Task<string> CreateLot(int lotId, Dictionary<string, string> fields)
    {
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("Cookie", $"golden_key={Key}; PHPSESSID={SessionId}");
        httpClient.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
        httpClient.DefaultRequestHeaders.Add("Accept", "*/*");
        httpClient.DefaultRequestHeaders.Add("Content-Type", "application/x-www-form-urlencoded; charset=UTF-8");

        var nodeId = fields["node_id"];
        if (nodeId != lotId.ToString())
        {
            throw new Exception("Lot ID in fields does not match the specified lot ID");
        }

        var content = new FormUrlEncodedContent(fields);
        var response = await httpClient.PostAsync($"{Links.SaveLotUrl}", content);
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception("Failed to create lot");
        }

        var result = await response.Content.ReadAsStringAsync();
        return result;
    }


    /// <summary>
    /// Send message to chat
    /// </summary>
    /// <param name="chatId">Chat ID</param>
    /// <param name="message">Your message</param>
    /// <param name="timeout">Response timeout</param>
    /// <returns>Response to request</returns>
    /// <exception cref="Exception"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task<dynamic> SendMessage(int chatId, string message, double timeout = 10.0)
    {
        var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(timeout)
        };
        httpClient.DefaultRequestHeaders.Add("Accept", "*/*");
        httpClient.DefaultRequestHeaders.Add("Cookie", $"golden_key={Key}; PHPSESSID={SessionId}");
        httpClient.DefaultRequestHeaders.Add("Content-Type", "application/x-www-form-urlencoded; charset=UTF-8");
        httpClient.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
        var requestObject = new
        {
            action = "chat_message",
            data = new
            {
                node = chatId,
                last_message = -1,
                context = message
            }
        };
        var payload = new
        {
            objects = "",
            request = JsonConvert.SerializeObject(requestObject),
            csrf_token = CsrfToken
        };

        var content = new StringContent(JsonConvert.SerializeObject(payload));
        var response = await httpClient.PostAsync($"{Links.RunnerUrl}", content);
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception("Failed to send message");
        }

        var jsonResponse = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<dynamic>(jsonResponse) ??
               throw new InvalidOperationException("Failed to parse response");
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="category"></param>
    /// <param name="timeout"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public async Task<string> RequestLotsRaise(Category category, double timeout = 10.0)
    {
        var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(timeout)
        };
        httpClient.DefaultRequestHeaders.Add("Accept", "*/*");
        httpClient.DefaultRequestHeaders.Add("Content-Type", "application/x-www-form-urlencoded; charset=UTF-8");
        httpClient.DefaultRequestHeaders.Add("Cookie", $"locale=ru; golden_key={Key}");
        httpClient.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");

        var payload = new
        {
            game_id = category.GameId,
            node_id = category.Id
        };
        var content = new StringContent(JsonConvert.SerializeObject(payload));
        var response = await httpClient.PostAsync(Links.RaiseUrl, content);
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception("Failed to raise lots");
        }

        var result = await response.Content.ReadAsStringAsync();
        return result;
    }
}