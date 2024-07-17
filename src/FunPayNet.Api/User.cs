using System.Net;
using FunPayNet.Api.Items;
using FunPayNet.Api.Utils;
using HtmlAgilityPack;

namespace FunPayNet.Api;

/// <summary>
/// User model
/// </summary>
public class User
{
    /// <summary>
    /// Get info about user active lots
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="includeCurrency">Add categories to the list that relate to in-game currency?</param>
    /// <param name="timeout">Request timeout</param>
    /// <returns>UserLots object</returns>
    /// <exception cref="Exception"></exception>
    public static async Task<UserLots> GetUserLots(int userId, bool includeCurrency = false, double timeout = 10.0)
    {
        var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(timeout)
        };
        var request = await httpClient.GetAsync($"{Links.UserUrl}/{userId}/");
        switch (request.StatusCode)
        {
            case HttpStatusCode.NotFound:
                throw new Exception("User not found");
            case HttpStatusCode.OK:
                break;
            default:
                throw new Exception("Failed to get user lots");
        }

        var htmlContent = await request.Content.ReadAsStringAsync();
        var htmlDocument = new HtmlDocument();
        htmlDocument.LoadHtml(htmlContent);

        var categories = new List<Category>();
        var lots = new List<Lot>();

        var offerNodes = htmlDocument.DocumentNode.SelectNodes("//div[@class='offer']");
        if (offerNodes == null)
        {
            return new UserLots
            {
                Categories = categories,
                Lots = lots
            };
        }

        foreach (var offerNode in offerNodes)
        {
            var categoryNode = offerNode.SelectSingleNode(".//div[@class='offer-list-title-container']");
            var categoryLinkNode = categoryNode.SelectSingleNode(".//div[@class='offer-list-title']//a");

            var categoryLink = categoryLinkNode.GetAttributeValue("href", null);

            var categoryType = categoryLink.Contains("chips") ? CategoryType.Currency : CategoryType.Lot;
            if (categoryType == CategoryType.Currency && !includeCurrency) continue;

            var editLotsUrl = categoryLink + "trade";
            var categoryTitle = categoryLinkNode.InnerText;
            var categoryId = int.Parse(categoryLink.TrimEnd('/').Split('/').Last());
            var categoryObject = new Category
            {
                Id = categoryId,
                GameId = null,
                Title = categoryTitle,
                EditLotsUrl = editLotsUrl,
                AllLotsUrl = categoryLink,
                Type = categoryType
            };
            categories.Add(categoryObject);

            var lotNodes = offerNode.SelectNodes(".//div[@class='offer-tc-container']//a");
            foreach (var lotNode in lotNodes)
            {
                var lotId = int.Parse(lotNode.GetAttributeValue("href", null).Split("id=")[1]);
                var title = lotNode.SelectSingleNode("//div[@class='tc-desc-text']").InnerText;
                var price = (int)Math.Round(decimal.Parse(lotNode.SelectSingleNode("//div[@class='tc-price']")
                    .GetAttributeValue("data-s", "0")));
                var server = lotNode.SelectSingleNode(".//div[@class='tc-server']")?.InnerText;
                var lotObject = new Lot
                {
                    Id = lotId,
                    Title = title,
                    Price = price,
                    CategoryId = categoryId,
                    Server = server
                };
                lots.Add(lotObject);
            }
        }

        return new UserLots
        {
            Categories = categories,
            Lots = lots
        };
    }
}