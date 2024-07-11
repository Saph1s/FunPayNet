﻿using System.Net;
using System.Web;
using FunPayNet.Api.Items;
using FunPayNet.Api.Utils;
using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FunPayNet.Api;

public class Account
{
    public required int Id { get; set; }
    public required string Username { get; set; }
    public float Balance { get; set; }
    public string? Currency { get; set; }
    public int ActiveOrders { get; set; }
    public required string Key { get; set; }
    public string? CsrfToken { get; set; }
    private string? SessionId { get; set; }
    public string? SavedChats { get; set; }
    public DateTime LastUpdated { get; set; }

    public async Task<Account> GetAccount(string token, double timeout = 10.0)
    {
        var httpHandler = new HttpClientHandler()
        {
            CookieContainer = new CookieContainer(),
            UseCookies = true,
        };
        var httpClient = new HttpClient(httpHandler);
        httpClient.Timeout = TimeSpan.FromSeconds(timeout);
        httpClient.DefaultRequestHeaders.Add("Cookie", $"golden_key={Key}");
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
        var userId = appData["userId"].Value<int>();
        var csrfToken = appData["csrf-token"].Value<string>();

        var activeSalesNode = htmlDocument.DocumentNode.SelectSingleNode("//span[@class='badge badge-trade']");
        var activeSales = activeSalesNode != null ? int.Parse(activeSalesNode.InnerText) : 0;

        var balanceNode = htmlDocument.DocumentNode.SelectSingleNode("//span[@class='badge badge-balance']");
        float balance = 0;
        string currency = null;
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

    public async Task<List<Order>> GetOrders(List<string> excludeOrders, bool includeOutstanding = true,
        bool includeCompleted = false, bool includeRefunded = false, double timeout = 10.0)
    {
        var httpClient = new HttpClient()
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

            var orderId = htmlDocument.DocumentNode.SelectSingleNode("//div[@class='tc-order']")?.InnerText;
            if (excludeOrders.Contains(orderId)) continue;
            var title = orderNode.SelectSingleNode("//div[@class='order-desc']//div")?.InnerText;
            var price = float.Parse(orderNode.SelectSingleNode("//div[@class='tc-price']")?.InnerText.Split(" ")[0]);

            var customer = htmlDocument.DocumentNode.SelectSingleNode("//div[@class='media-user-name']//span");
            var customerName = customer?.InnerText;
            var customerId = int.Parse(customer?.GetAttributeValue("data-href", null).TrimEnd('/').Split('/').Last());

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
            var value = selectField.SelectSingleNode("//option[@selected]")?.GetAttributeValue("value", null);
            list.Add(name, value);
        }

        return list;
    }

    public async Task<int> GetCategoryGameId(Category category, double timeout = 10.0)
    {
        var link = category.Type == CategoryType.Currency
            ? $"{Links.BaseUrl}/chips/{category.Id}/trade"
            : $"{Links.BaseUrl}/lots/{category.Id}/trade";
        var httpClient = new HttpClient()
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

    public async Task<dynamic> SendMessage(int chatId, string message, double timeout = 10.0)
    {
        var httpClient = new HttpClient()
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

    public async Task<string> RequestLotsRaise(Category category, double timeout = 10.0)
    {
        var httpClient = new HttpClient()
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
            node_id = category.Id,
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