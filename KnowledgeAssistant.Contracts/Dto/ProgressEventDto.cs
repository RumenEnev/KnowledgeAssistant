namespace KnowledgeAssistant.Contracts.Dto
{
    /// <summary>
    /// Payload sent over SSE (event: "progress") to let the client display a short, human-readable
    /// status update about what the backend is currently doing (e.g. "Searching repositories...").
    /// </summary>
    public class ProgressEventDto
    {
        public string Message { get; set; } = string.Empty;
    }
}
