using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Cesil
{
    // This is a helper for when a reference must always be
    // non-null, but it's initialization or management logic
    // is too complicated for the compiler.
    //
    // Prefer normal non-nullable references where possible,
    // as using this imposes a runtime check on get/set.
    //
    // If you're returning this from a property and expecting
    // to modify it, you'll want to return `ref NonNull` or
    // your updates will work on a copy.
    internal struct NonNull<T>
        where T: class
    {
        private T? _Value;

        public bool HasValue
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _Value != null;
        }

        [DisallowNull]
        [NotNull]
        public T Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Utils.NonNull(_Value);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
#if DEBUG
                // fail faster for debugging purposes
                Utils.NonNull(value);
#endif
                _Value = value;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetAllowNull(T? value)
        {
            _Value = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            _Value = null;
        }

        public override int GetHashCode()
        => _Value?.GetHashCode() ?? 0;

        public override string ToString()
        => _Value?.ToString() ?? "";
    }
}
