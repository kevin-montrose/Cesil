using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Cesil.Analyzers
{
    /// <summary>
    /// Abstract over the notion of "being used somewhere in this other node"
    /// </summary>
    public readonly struct SourceSpan
    {
        public string File { get; }
        public TextSpan Span { get; }

        private SourceSpan(string file, TextSpan span)
        {
            File = file;
            Span = span;
        }

        public bool Contains(SourceSpan other)
        {
            var path = other.File;
            if (path != File)
            {
                return false;
            }

            return Span.Contains(other.Span);
        }

        public static SourceSpan Create(SyntaxNode node)
        {
            var tree = node.SyntaxTree;
            return new SourceSpan(tree.FilePath, node.Span);
        }
    }
}
