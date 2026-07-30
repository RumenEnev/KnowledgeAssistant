namespace KnowledgeAssistant.Wpf.Models
{
    /// <summary>A model shown in the Model Context Windows window with read-only catalog information.</summary>
    public class ModelContextWindowDisplayModel
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public long Size { get; set; }

        public string SizeDisplay => FormatSize(Size);

        public int? ContextLength { get; set; }

        public string? Family { get; set; }

        public string? QuantizationLevel { get; set; }

        public string? ParameterSize { get; set; }

        private static string FormatSize(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double size = bytes;
            int unitIndex = 0;
            while (size >= 1024 && unitIndex < units.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }

            return $"{size:0.##} {units[unitIndex]}";
        }
    }
}
