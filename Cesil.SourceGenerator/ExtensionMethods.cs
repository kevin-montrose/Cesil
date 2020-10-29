using Microsoft.CodeAnalysis;

namespace Cesil.SourceGenerator
{
    internal static class ExtensionMethods
    {
        internal static T? ParentOrSelfOfType<T>(this SyntaxNode self)
            where T : SyntaxNode
        {
            var cur = self;

            while (cur != null)
            {
                if (cur is T asT)
                {
                    return asT;
                }

                cur = cur.Parent;
            }

            return null;
        }
    }
}
