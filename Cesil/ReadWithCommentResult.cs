namespace Cesil
{
    /// <summary>
    /// Represents the result of an attempted read operation which supports comments.
    /// </summary>
    [NotEquatable("Value is open, hashcode and equality may not be sensible")]
    public readonly struct ReadWithCommentResult<T>
    {
        internal static readonly ReadWithCommentResult<T> Empty = new ReadWithCommentResult<T>(ReadWithCommentResultType.NoValue);

        /// <summary>
        /// Indicates what, if anything, is available on this result.
        /// 
        /// If NoValue, no more results will be read.
        /// </summary>
        public ReadWithCommentResultType ResultType { get; }

        /// <summary>
        /// Convenience method for checking ResultType == HasValue
        /// </summary>
        [IntentionallyExposedPrimitive("Best way to expose a presense, it's fine")]
        public bool HasValue => ResultType == ReadWithCommentResultType.HasValue;

        /// <summary>
        /// Convenience method for checking ResultType == HasComment
        /// </summary>
        [IntentionallyExposedPrimitive("Best way to expose a presense, it's fine")]
        public bool HasComment => ResultType == ReadWithCommentResultType.HasComment;

        private readonly T _Value;

        /// <summary>
        /// The value read, if ResultType == HasValue.
        /// 
        /// Throws otherwise.
        /// </summary>
        public T Value
        {
            get
            {
                if (ResultType != ReadWithCommentResultType.HasValue)
                {
                    Throw.InvalidOperationException($"{nameof(ReadWithCommentResult<T>)} has no value");
                }

                return _Value;
            }
        }

        private readonly string _Comment;

        /// <summary>
        /// The comment read, if ResultType == HasComment.
        /// 
        /// Throws otherwise.
        /// </summary>
        public string Comment
        {
            get
            {
                if (ResultType != ReadWithCommentResultType.HasComment)
                {
                    Throw.InvalidOperationException($"{nameof(ReadWithCommentResult<T>)} has no comment");
                }

                return _Comment;
            }
        }

        internal ReadWithCommentResult(T val)
        {
            ResultType = ReadWithCommentResultType.HasValue;
            _Value = val;
            _Comment = null;
        }

        internal ReadWithCommentResult(string comment)
        {
            ResultType = ReadWithCommentResultType.HasComment;
            _Value = default;
            _Comment = comment;
        }

        private ReadWithCommentResult(ReadWithCommentResultType t)
        {
            ResultType = t;
            _Value = default;
            _Comment = null;
        }

        /// <summary>
        /// Returns a representation of this ReadResult struct.
        /// 
        /// Only for debugging, this value is not guaranteed to be stable.
        /// </summary>
        public override string ToString()
        {
            switch (ResultType)
            {
                case ReadWithCommentResultType.NoValue:
                    return "(Empty)";
                case ReadWithCommentResultType.HasValue:
                    return Value?.ToString();
                case ReadWithCommentResultType.HasComment:
                    return $"Comment: {Comment}";
                default:
                    Throw.InvalidOperationException($"Unexpected {nameof(ReadWithCommentResultType)}: {ResultType}");
                    // just for control flow
                    return default;
            }
        }
    }
}
