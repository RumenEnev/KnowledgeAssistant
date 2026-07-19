namespace KnowledgeAssistant.Domain
{
    public class AiModel
    {
        public Guid Id { get; set; }

        public string? Name { get; set; }

        public string? DisplayName { get; set; }

        public string? Provider { get; set; }

        public string? Family { get; set; }

        public long? Size { get; set; }

        public bool? IsInstalled { get; set; }

        public DateTimeOffset? LastSeen { get; set; }
    }
}
