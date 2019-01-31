using System;

namespace Cesil
{
    /// <summary>
    /// Represents the result of an attempted read operation.
    /// </summary>
    public readonly struct ReadResult<T>
    {
        internal static readonly ReadResult<T> Empty = new ReadResult<T>(false);

        /// <summary>
        /// True if a value was read, false if not.
        /// 
        /// If false, there are no more rows to be read.
        /// </summary>
        public bool HasValue { get; }

        private readonly T _Value;

        /// <summary>
        /// The value read, if HasValue == true.
        /// </summary>
        public T Value
        {
            get
            {
                if (!HasValue)
                {
                    Throw.InvalidOperation($"{nameof(ReadResult<T>)} has no value");
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
    }
}
