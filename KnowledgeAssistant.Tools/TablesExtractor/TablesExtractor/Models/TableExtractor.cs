using System.Text.Json;
using HtmlAgilityPack;

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

        public string TableHtmlToJson(string tableHtml)
        {
            if (string.IsNullOrWhiteSpace(tableHtml))
            {
                throw new ArgumentException("Table HTML must not be null or empty.", nameof(tableHtml));
            }

            string cleanedHtml = CleanTableHtml(tableHtml);

            var doc = new HtmlDocument();
            doc.LoadHtml(cleanedHtml);

            var table = doc.DocumentNode.SelectSingleNode("//table");
            if (table == null)
            {
                return "[]";
            }

            var grid = BuildGrid(table);
            if (grid.Count == 0)
            {
                return "[]";
            }

            bool hasHeaderRow = table.SelectSingleNode(".//thead") != null || grid[0].RowIsHeader;

            List<object> result;

            if (hasHeaderRow && grid.Count > 1)
            {
                var headers = grid[0].Cells
                    .Select((c, i) => string.IsNullOrWhiteSpace(c) ? $"column_{i + 1}" : c)
                    .ToList();
                headers = Deduplicate(headers);

                result = grid.Skip(1)
                    .Select(row =>
                    {
                        var obj = new Dictionary<string, string>();
                        for (int i = 0; i < headers.Count; i++)
                        {
                            obj[headers[i]] = i < row.Cells.Count ? row.Cells[i] : "";
                        }
                        return (object)obj;
                    })
                    .ToList();
            }
            else
            {
                result = grid.Select(row => (object)row.Cells).ToList();
            }

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }

        public Task<string> TableHtmlToJsonAsync(string tableHtml)
        {
            return Task.FromResult(TableHtmlToJson(tableHtml));
        }

        private static List<GridRow> BuildGrid(HtmlNode table)
        {
            var rows = table.SelectNodes(".//tr");
            if (rows == null)
            {
                return new List<GridRow>();
            }

            var grid = new List<GridRow>();
            var pendingRowSpans = new Dictionary<int, (int remaining, string value)>();

            foreach (var tr in rows)
            {
                var cells = new List<string>();
                bool rowIsHeader = tr.SelectNodes("./th") != null && tr.SelectNodes("./td") == null;

                var cellNodes = tr.SelectNodes("./th|./td");
                int colIndex = 0;
                int cellNodeIdx = 0;

                while (cellNodeIdx < (cellNodes?.Count ?? 0) || pendingRowSpans.ContainsKey(colIndex))
                {
                    if (pendingRowSpans.TryGetValue(colIndex, out var pending))
                    {
                        cells.Add(pending.value);
                        if (pending.remaining - 1 > 0)
                        {
                            pendingRowSpans[colIndex] = (pending.remaining - 1, pending.value);
                        }
                        else
                        {
                            pendingRowSpans.Remove(colIndex);
                        }
                        colIndex++;
                        continue;
                    }

                    if (cellNodeIdx >= (cellNodes?.Count ?? 0))
                    {
                        break;
                    }

                    var node = cellNodes[cellNodeIdx++];
                    string text = CleanText(node.InnerText);
                    int colSpan = ParseSpan(node.GetAttributeValue("colspan", "1"));
                    int rowSpan = ParseSpan(node.GetAttributeValue("rowspan", "1"));

                    for (int i = 0; i < colSpan; i++)
                    {
                        cells.Add(text);
                        if (rowSpan > 1)
                        {
                            pendingRowSpans[colIndex] = (rowSpan - 1, text);
                        }
                        colIndex++;
                    }
                }

                grid.Add(new GridRow { Cells = cells, RowIsHeader = rowIsHeader });
            }

            return grid;
        }

        private static int ParseSpan(string value) =>
            int.TryParse(value, out int n) && n > 0 ? n : 1;

        private static string CleanText(string raw) =>
            System.Net.WebUtility.HtmlDecode(raw)
                .Replace('\u00A0', ' ')
                .Trim()
                .Replace("\r\n", " ")
                .Replace("\n", " ");

        private static List<string> Deduplicate(List<string> headers)
        {
            var seen = new Dictionary<string, int>();
            var result = new List<string>();
            foreach (var h in headers)
            {
                if (!seen.TryGetValue(h, out int count))
                {
                    seen[h] = 1;
                    result.Add(h);
                }
                else
                {
                    seen[h] = count + 1;
                    result.Add($"{h}_{count + 1}");
                }
            }
            return result;
        }

        private class GridRow
        {
            public List<string> Cells { get; set; } = new();
            public bool RowIsHeader { get; set; }
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
                {
                    sup.Remove();
                }
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
    }
}