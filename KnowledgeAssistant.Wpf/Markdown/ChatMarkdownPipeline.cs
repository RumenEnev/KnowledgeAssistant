using Markdig;
using Markdig.Wpf;

namespace KnowledgeAssistant.Wpf.Markdown
{
    public static class ChatMarkdownPipeline
    {
        public static MarkdownPipeline Instance { get; } = Build();

        private static MarkdownPipeline Build()
        {
            var builder = new MarkdownPipelineBuilder().UseSupportedExtensions().UseMathematics();
            builder.Extensions.Add(new WpfMathRendererExtension());
            return builder.Build();
        }
    }
}
