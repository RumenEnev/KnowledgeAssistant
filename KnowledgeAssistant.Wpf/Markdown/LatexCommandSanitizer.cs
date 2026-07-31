using System.Text.RegularExpressions;

namespace KnowledgeAssistant.Wpf.Markdown
{
    /// <summary>
    /// WpfMath (the LaTeX rendering engine used for math formulas in the WPF
    /// chat UI) implements only a subset of LaTeX commands - it has no
    /// support for common commands LLMs frequently emit, such as
    /// "\mathbf", "\boldsymbol", "\implies", "\dfrac", etc. Rather than let
    /// these formulas fail to render entirely, this sanitizer rewrites them
    /// to the closest command that WpfMath does support.
    /// </summary>
    public static class LatexCommandSanitizer
    {
        private static readonly (Regex Pattern, string Replacement)[] Replacements =
        {
            // Bold/sans/typewriter font commands aren't supported - fall back to
            // upright roman text so the content still renders (without styling).
            (new Regex(@"\\boldsymbol\b"), @"\mathrm"),
            (new Regex(@"\\mathbf\b"), @"\mathrm"),
            (new Regex(@"\\mathsf\b"), @"\mathrm"),
            (new Regex(@"\\mathtt\b"), @"\mathrm"),

            // Fraction variants that WpfMath doesn't know about.
            (new Regex(@"\\dfrac\b"), @"\frac"),
            (new Regex(@"\\tfrac\b"), @"\frac"),
            (new Regex(@"\\cfrac\b"), @"\frac"),

            // Logic/arrow commands not present in WpfMath's symbol table.
            (new Regex(@"\\implies\b"), @"\Rightarrow"),
            (new Regex(@"\\impliedby\b"), @"\Leftarrow"),
            (new Regex(@"\\iff\b"), @"\Leftrightarrow"),
            (new Regex(@"\\Longrightarrow\b"), @"\Rightarrow"),
            (new Regex(@"\\Longleftarrow\b"), @"\Leftarrow"),
            (new Regex(@"\\longrightarrow\b"), @"\rightarrow"),
            (new Regex(@"\\longleftarrow\b"), @"\leftarrow"),
            (new Regex(@"\\longleftrightarrow\b"), @"\leftrightarrow"),

            // Spacing/formatting commands with no effect in WpfMath - safe to drop.
            (new Regex(@"\\displaystyle\b"), string.Empty),
            (new Regex(@"\\textstyle\b"), string.Empty),
            (new Regex(@"\\left\.\s*"), string.Empty),
            (new Regex(@"\\right\.\s*"), string.Empty),
        };

        public static string Sanitize(string latex)
        {
            if (string.IsNullOrEmpty(latex))
            {
                return latex;
            }

            var result = latex;
            foreach (var (pattern, replacement) in Replacements)
            {
                result = pattern.Replace(result, replacement);
            }

            return result;
        }
    }
}
