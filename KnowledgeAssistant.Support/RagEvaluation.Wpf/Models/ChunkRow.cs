namespace RagEvaluation.Desktop.Models;

public sealed class ChunkRow
{
    public ChunkRow(int id, int index, string text)
    {
        Id = id;
        Index = index;
        Text = text;
        var flat = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        Preview = flat.Length <= 200 ? flat : flat[..200] + "…";
    }

    public int Id { get; }
    public int Index { get; }
    public string Text { get; }
    public int Chars => Text.Length;
    public string Preview { get; }
}