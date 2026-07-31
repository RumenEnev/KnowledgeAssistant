using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfMath.Controls;

namespace KnowledgeAssistant.Wpf.Markdown
{
    /// <summary>
    /// Shared helper for creating a rendered LaTeX formula element, with a
    /// graceful fallback when WpfMath cannot parse the (sanitized) LaTeX -
    /// showing the raw source text instead of an empty/blank control.
    /// </summary>
    internal static class MathRenderHelper
    {
        public static FrameworkElement CreateFormulaElement(string rawLatex, double scale)
        {
            var sanitized = LatexCommandSanitizer.Sanitize(rawLatex);

            var formula = new FormulaControl
            {
                Formula = sanitized,
                Scale = scale
            };

            if (!formula.HasError)
            {
                return formula;
            }

            // WpfMath couldn't render this formula (e.g. it uses a LaTeX command
            // it doesn't support) - fall back to showing the raw text so the
            // content is still visible instead of a blank box.
            return new TextBlock
            {
                Text = rawLatex,
                FontFamily = new FontFamily("Consolas"),
                FontStyle = FontStyles.Italic,
                TextWrapping = TextWrapping.Wrap,
                ToolTip = "This math expression could not be rendered."
            };
        }
    }
}
