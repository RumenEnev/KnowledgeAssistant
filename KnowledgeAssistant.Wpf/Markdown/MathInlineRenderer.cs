using Markdig.Extensions.Mathematics;
using System;
using System.Windows;
using System.Windows.Documents;

namespace KnowledgeAssistant.Wpf.Markdown
{
    /// <summary>
    /// Renders an inline LaTeX math expression (<c>$...$</c>) using WpfMath.
    /// </summary>
    public class MathInlineRenderer : Markdig.Renderers.Wpf.WpfObjectRenderer<MathInline>
    {
        protected override void Write(Markdig.Renderers.WpfRenderer renderer, MathInline obj)
        {
            if (renderer == null) throw new ArgumentNullException(nameof(renderer));
            if (obj == null) throw new ArgumentNullException(nameof(obj));

            var element = MathRenderHelper.CreateFormulaElement(obj.Content.ToString(), scale: 16);
            element.VerticalAlignment = VerticalAlignment.Center;

            var container = new InlineUIContainer(element)
            {
                BaselineAlignment = BaselineAlignment.Center,
                Tag = obj
            };

            renderer.WriteInline(container);
        }
    }
}
