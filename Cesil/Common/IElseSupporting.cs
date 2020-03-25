using System.Collections.Immutable;

namespace Cesil
{
    internal interface IElseSupporting<T>
        where T : class, IElseSupporting<T>
    {
        ImmutableArray<T> Fallbacks { get; }
        T Clone(ImmutableArray<T> newFallbacks);
    }

    internal static class IElseSupportingExtensionMethods
    {
        // this creates a new T from baseT, and sets up fallbacks such
        //   that each fallback T should be checked in reverse order
        //   of how it appears in the chain AFTER checking the returned T
        internal static T DoElse<T>(this T baseT, T fallback)
            where T : class, IElseSupporting<T>
        {
            var builder = ImmutableArray.CreateBuilder<T>();
            builder.AddRange(baseT.Fallbacks);
            builder.Add(fallback);
            builder.AddRange(fallback.Fallbacks);

            return baseT.Clone(builder.ToImmutable());
        }
    }
}
