using TablesExtractor.Models;
using static System.Runtime.InteropServices.JavaScript.JSType;

internal class Program
{
    private async static Task Main(string[] args)
    {
        string url = args.Length > 0 ? args[0] : "https://en.wikipedia.org/wiki/List_of_countries_by_population";

        var extractor = new TableExtractor();
        List<string> tables = await extractor.ExtractTablesAsync(url);

        Console.WriteLine($"Found {tables.Count} table(s) on {url}");
        for (int i = 0; i < tables.Count; i++)
        {
            Console.WriteLine($"--- Table {i + 1} (length {tables[i].Length} chars) ---");
        }
    }
}