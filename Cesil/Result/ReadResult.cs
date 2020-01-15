namespace Cesil
{
    /// <summary>
    /// Represents the result of an attempted read operation.
    /// </summary>
    [NotEquatable("Value is open, hash code and equality may not be sensible")]
    public readonly struct ReadResult<TRow>
    {
        internal static readonly ReadResult<TRow> Empty = new ReadResult<TRow>(false);

        /// <summary>
        /// True if a value was read, false if not.
        /// 
        /// If false, there are no more rows to be read.
        /// </summary>
        [IntentionallyExposedPrimitive("Best way to expose a presence, it's fine")]
        public bool HasValue { get; }

        private readonly TRow _Value;

        /// <summary>
        /// The value read, if HasValue == true.
        /// 
        /// Throws otherwise.
        /// </summary>
        public TRow Value
        {
            get
            {
                if (!HasValue)
                {
                    return Throw.InvalidOperationException<TRow>($"{nameof(ReadResult<TRow>)} has no value");
                }

                return _Value;
            }
        }

        internal ReadResult(TRow val)
        {
            HasValue = true;
            _Value = val;
        }

        private ReadResult(bool v)
        {
            HasValue = v;
            _Value = default!;
        }

        /// <summary>
        /// Returns a representation of this ReadResult struct.
        /// 
        /// Only for debugging, this value is not guaranteed to be stable.
        /// </summary>
        public override string ToString()
        {
            if (!HasValue)
            {
                return $"{nameof(ReadResult<TRow>)} which is empty";
            }

            if (_Value == null)
            {
                return $"{nameof(ReadResult<TRow>)} with (null) Value";
            }

            return $"{nameof(ReadResult<TRow>)} with {_Value}";
        }
    }
}
