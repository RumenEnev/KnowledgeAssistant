using Markdig.Extensions.Mathematics;
using System;
using System.Text;
using System.Windows;
using System.Windows.Documents;

namespace KnowledgeAssistant.Wpf.Markdown
{
    /// <summary>
    /// Renders a block LaTeX math expression (<c>$$...$$</c>) using WpfMath.
    /// </summary>
    public class MathBlockRenderer : Markdig.Renderers.Wpf.WpfObjectRenderer<MathBlock>
    {
        protected override void Write(Markdig.Renderers.WpfRenderer renderer, MathBlock obj)
        {
            if (renderer == null) throw new ArgumentNullException(nameof(renderer));
            if (obj == null) throw new ArgumentNullException(nameof(obj));

            var latex = ExtractLatex(obj);

            var element = MathRenderHelper.CreateFormulaElement(latex, scale: 20);
            element.HorizontalAlignment = HorizontalAlignment.Center;

            var container = new BlockUIContainer(element)
            {
                Tag = obj
            };

            renderer.WriteBlock(container);
        }

        private static string ExtractLatex(MathBlock obj)
        {
            var lines = obj.Lines;
            if (lines.Lines == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            var slices = lines.Lines;
            for (var i = 0; i < lines.Count; i++)
            {
                if (i != 0)
                {
                    builder.Append('\n');
                }

                builder.Append(slices[i].Slice.ToString());
            }

            return builder.ToString();
        }
    }
}
