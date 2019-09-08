namespace Cesil
{
    /// <summary>
    /// Represents the result of an attempted read operation.
    /// </summary>
    [NotEquatable("Value is open, hashcode and equality may not be sensible")]
    public readonly struct ReadResult<T>
    {
        internal static readonly ReadResult<T> Empty = new ReadResult<T>(false);

        /// <summary>
        /// True if a value was read, false if not.
        /// 
        /// If false, there are no more rows to be read.
        /// </summary>
        [IntentionallyExposedPrimitive("Best way to expose a presense, it's fine")]
        public bool HasValue { get; }

        private readonly T _Value;

        /// <summary>
        /// The value read, if HasValue == true.
        /// 
        /// Throws otherwise.
        /// </summary>
        public T Value
        {
            get
            {
                if (!HasValue)
                {
                    Throw.InvalidOperationException($"{nameof(ReadResult<T>)} has no value");
                }

                return _Value;
            }
        }

        internal ReadResult(T val)
        {
            HasValue = true;
            _Value = val;
        }

        private ReadResult(bool v)
        {
            HasValue = v;
            _Value = default;
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
                return $"{nameof(ReadResult<T>)} which is empty";
            }

            if (_Value == null)
            {
                return $"{nameof(ReadResult<T>)} with (null) Value";
            }

            return $"{nameof(ReadResult<T>)} with {_Value}";
        }
    }
}
