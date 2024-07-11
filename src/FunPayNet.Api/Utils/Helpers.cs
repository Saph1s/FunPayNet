namespace FunPayNet.Api.Utils;

public static class Helpers
{
    private static readonly Random random = new Random();

    /// <summary>
    /// Generates a random tag of the specified length.
    /// </summary>
    /// <param name="length">Tag length</param>
    /// <returns>Generated tag</returns>
    public static string GenerateRandomTag(int length = 10)
    {
        var digits = Enumerable.Range(0, 10).Select(i => (char)i);
        var lowerCaseLetters = Enumerable.Range('a', 26).Select(i => (char)i);
        var symbols = digits.Concat(lowerCaseLetters).ToArray();

        return new string(Enumerable.Repeat(symbols, length)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }

    /// <summary>
    /// Parses the response from the raise request and returns the waiting time.
    /// </summary>
    /// <param name="response">Response text</param>
    /// <returns>Approximate waiting time until the next raising of lots (in seconds).</returns>
    public static int GetWaitTimeFromRaiseResponse(string response)
    {
        if (response.Contains("секунду."))
        {
            return 1;
        }

        if (response.Contains("сек"))
        {
            var parts = response.Split();
            return int.Parse(parts[1]);
        }

        if (response.Contains("минуту."))
        {
            return 60;
        }

        if (response.Contains("мин"))
        {
            var parts = response.Split();
            return (int.Parse(parts[1]) - 1) * 60;
        }

        if (response.Contains("час"))
        {
            return 3600;
        }

        return 10;
    }
}