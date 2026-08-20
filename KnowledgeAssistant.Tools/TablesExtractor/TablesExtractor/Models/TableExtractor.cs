using HtmlAgilityPack;
using OllamaClients;
using TablesExtractor.Models;

namespace TableExtraction
{
    public class TableExtractor
    {
        private readonly HttpClient _httpClient;

        public TableExtractor()
        {
            _httpClient = CreateDefaultClient();
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

        public async Task<List<string>> ExtractTablesAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentException("URL must not be null or empty.", nameof(url));
            }

            string html = await DownloadHtmlAsync(url);
            return ExtractTablesFromHtml(html);
        }

        public async Task<string> TableHtmlToJsonAsync(OllamaClient ollamaClient, string tableHtml)
        {
            if (ollamaClient == null)
            {
                throw new ArgumentNullException(nameof(ollamaClient));
            }

            if (string.IsNullOrWhiteSpace(tableHtml))
            {
                throw new ArgumentException("Table HTML must not be null or empty.", nameof(tableHtml));
            }

            string cleanedHtml = CleanTableHtml(tableHtml);
            string rawResponse = await ollamaClient.GenerateAsync(Instructions.TableToJsonInstruction(), cleanedHtml);
            string json = ExtractJsonArray(rawResponse);
            if (IsValidJsonArray(json))
            {
                return json;
            }

            // The model added commentary or produced malformed JSON — retry once with a stricter nudge.
            string retryPrompt =
                "Convert this table to a JSON array. Respond with ONLY the JSON array, " +
                "starting with [ and ending with ], no words before or after:\n\n" + cleanedHtml;
            string retryResponse = await ollamaClient.GenerateAsync(Instructions.TableToJsonInstruction(), retryPrompt);
            string retryJson = ExtractJsonArray(retryResponse);

            return IsValidJsonArray(retryJson) ? retryJson : "[]";
        }

        private async Task<string> DownloadHtmlAsync(string url)
        {
            using var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        private static string CleanTableHtml(string tableHtml)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(tableHtml);

            var tableNode = doc.DocumentNode.SelectSingleNode("//table") ?? doc.DocumentNode;
            var supNodes = tableNode.SelectNodes(".//sup");
            if (supNodes != null)
            {
                foreach (var sup in supNodes)
                    sup.Remove();
            }

            var keepAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "colspan", "rowspan" };
            foreach (var node in tableNode.DescendantsAndSelf())
            {
                if (node.NodeType != HtmlNodeType.Element)
                {
                    continue;
                }

                var attrsToRemove = node.Attributes
                    .Where(a => !keepAttributes.Contains(a.Name))
                    .Select(a => a.Name)
                    .ToList();

                foreach (var attrName in attrsToRemove)
                {
                    node.Attributes.Remove(attrName);
                }
            }

            return tableNode.OuterHtml;
        }

        private static string ExtractJsonArray(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "[]";
            }

            text = StripCodeFences(text);
            int start = text.IndexOf('[');
            if (start < 0)
            {
                return "[]";
            }

            int depth = 0;
            for (int i = start; i < text.Length; i++)
            {
                if (text[i] == '[') depth++;
                else if (text[i] == ']')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return text.Substring(start, i - start + 1);
                    }
                }
            }

            return "[]"; // no matching closing bracket found
        }

        private static bool IsValidJsonArray(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                return doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array;
            }
            catch (System.Text.Json.JsonException)
            {
                return false;
            }
        }

        private static string StripCodeFences(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            text = text.Trim();
            if (text.StartsWith("```"))
            {
                int firstNewline = text.IndexOf('\n');
                if (firstNewline >= 0)
                {
                    text = text.Substring(firstNewline + 1);
                }

                int fenceEnd = text.LastIndexOf("```", StringComparison.Ordinal);
                if (fenceEnd >= 0)
                {
                    text = text.Substring(0, fenceEnd);
                }
            }

            return text.Trim();
        }

        private List<string> ExtractTablesFromHtml(string html)
        {
            var result = new List<string>();
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var tableNodes = doc.DocumentNode.SelectNodes("//table");
            if (tableNodes == null)
            {
                return result; // no tables found
            }

            foreach (var tableNode in tableNodes)
            {
                result.Add(tableNode.OuterHtml);
            }

            return result;
        }
    }
}