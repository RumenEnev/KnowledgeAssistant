using HtmlAgilityPack;

namespace TablesExtractor.Models;

public class TableExtractor
{
    private readonly HttpClient _httpClient;

    public TableExtractor()
    {
        _httpClient = CreateDefaultClient();
    }

    public async Task<List<string>> ExtractTablesAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("URL must not be null or empty.", nameof(url));
        }

        string html = await DownloadHtmlAsync(url);
        return ExtractTablesFromHtml(html);
    }

    private async Task<string> DownloadHtmlAsync(string url)
    {
        using var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private List<string> ExtractTablesFromHtml(string html)
    {
        var result = new List<string>();
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var tableNodes = doc.DocumentNode.SelectNodes("//table");
        if (tableNodes == null)
        {
            return result;
        }

        foreach (var tableNode in tableNodes)
        {
            result.Add(tableNode.OuterHtml);
        }

        return result;
    }

    private static HttpClient CreateDefaultClient()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.GZip
                | System.Net.DecompressionMethods.Deflate
                | System.Net.DecompressionMethods.Brotli
        };

        var client = new HttpClient(handler);

        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " + "(KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
        client.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
        client.DefaultRequestHeaders.Add("Sec-Fetch-Site", "none");
        client.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
        client.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");

        return client;
    }
}