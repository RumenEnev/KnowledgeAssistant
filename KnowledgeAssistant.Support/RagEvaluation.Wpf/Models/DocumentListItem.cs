namespace RagEvaluation.Desktop.Models;

public class DocumentListItem
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string TopicsDisplay { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}