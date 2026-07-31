using Markdig;
using Markdig.Extensions.Mathematics;
using Markdig.Renderers;
using Markdig.Renderers.Wpf;

namespace KnowledgeAssistant.Wpf.Markdown
{
    /// <summary>
    /// Adds WPF rendering support for the LaTeX math nodes (<see cref="MathInline"/> and <see cref="MathBlock"/>)
    /// produced by Markdig's built-in <see cref="MathExtension"/> parser.
    /// Markdig.Wpf's <see cref="WpfRenderer"/> does not register any renderer for these node types by default,
    /// so without this extension math formulas silently disappear from the rendered <c>FlowDocument</c>.
    /// </summary>
    public class WpfMathRendererExtension : IMarkdownExtension
    {
        public void Setup(MarkdownPipelineBuilder pipeline)
        {
            // Parsing is already enabled by MarkdownExtensions.UseSupportedExtensions() (via UseMathematics()).
            // This extension only contributes the WPF rendering side.
        }

        public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
        {
            if (renderer is WpfRenderer wpfRenderer)
            {
                if (!wpfRenderer.ObjectRenderers.Contains<MathInlineRenderer>())
                {
                    wpfRenderer.ObjectRenderers.Insert(0, new MathInlineRenderer());
                }

                if (!wpfRenderer.ObjectRenderers.Contains<MathBlockRenderer>())
                {
                    wpfRenderer.ObjectRenderers.Insert(0, new MathBlockRenderer());
                }
            }
        }
    }
}
