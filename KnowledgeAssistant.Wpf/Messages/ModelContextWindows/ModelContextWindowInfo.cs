namespace KnowledgeAssistant.Wpf.Messages.ModelContextWindows
{
    public record ModelContextWindowInfo(
        Guid Id,
        string Name,
        long Size,
        int? ContextLength,
        string? Family,
        string? QuantizationLevel,
        string? ParameterSize,
        bool InternalUseOnly,
        bool CanCallTools);
}
