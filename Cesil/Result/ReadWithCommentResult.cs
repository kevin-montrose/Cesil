namespace Cesil
{
    /// <summary>
    /// Represents the result of an attempted read operation which supports comments.
    /// </summary>
    [NotEquatable("Value is open, hash code and equality may not be sensible")]
    public readonly struct ReadWithCommentResult<TRow>
    {
        internal static readonly ReadWithCommentResult<TRow> Empty = new ReadWithCommentResult<TRow>(ReadWithCommentResultType.NoValue);

        /// <summary>
        /// Indicates what, if anything, is available on this result.
        /// 
        /// If NoValue, no more results will be read.
        /// </summary>
        public ReadWithCommentResultType ResultType { get; }

        /// <summary>
        /// Convenience method for checking ResultType == HasValue
        /// </summary>
        [IntentionallyExposedPrimitive("Best way to expose a presence, it's fine")]
        public bool HasValue => ResultType == ReadWithCommentResultType.HasValue;

        /// <summary>
        /// Convenience method for checking ResultType == HasComment
        /// </summary>
        [IntentionallyExposedPrimitive("Best way to expose a presence, it's fine")]
        public bool HasComment => ResultType == ReadWithCommentResultType.HasComment;

        private readonly TRow _Value;

        /// <summary>
        /// The value read, if ResultType == HasValue.
        /// 
        /// Throws otherwise.
        /// </summary>
        public TRow Value
        {
            get
            {
                if (ResultType != ReadWithCommentResultType.HasValue)
                {
                    Throw.InvalidOperationException($"{nameof(ReadWithCommentResult<TRow>)} has no value");
                }

                return _Value;
            }
        }

        private readonly string? _Comment;

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
                    Throw.InvalidOperationException($"{nameof(ReadWithCommentResult<TRow>)} has no comment");
                }

                return Utils.NonNull(_Comment);
            }
        }

        internal ReadWithCommentResult(TRow val)
        {
            ResultType = ReadWithCommentResultType.HasValue;
            _Value = val;
            _Comment = null;
        }

        internal ReadWithCommentResult(string comment)
        {
            ResultType = ReadWithCommentResultType.HasComment;
#pragma warning disable CES0005 // T is generic, and null is legal, but since T isn't known to be a class we have to forgive null here
            _Value = default!;
#pragma warning restore CES0005
            _Comment = comment;
        }

        private ReadWithCommentResult(ReadWithCommentResultType t)
        {
            ResultType = t;
#pragma warning disable CES0005 // T is generic, and null is legal, but since T isn't known to be a class we have to forgive null here
            _Value = default!;
#pragma warning restore CES0005
            _Comment = null;
        }

        /// <summary>
        /// Returns a representation of this ReadResult struct.
        /// 
        /// Only for debugging, this value is not guaranteed to be stable.
        /// </summary>
        public override string ToString()
        => ResultType switch
        {
            ReadWithCommentResultType.NoValue => $"{nameof(ReadWithCommentResult<TRow>)} which is empty)",
            ReadWithCommentResultType.HasValue => $"{nameof(ReadWithCommentResult<TRow>)} with {Value}",
            ReadWithCommentResultType.HasComment => $"{nameof(ReadWithCommentResult<TRow>)} with comment: {Comment}",
            _ => Throw.InvalidOperationException_Returns<string>($"Unexpected {nameof(ReadWithCommentResultType)}: {ResultType}")
        };
    }
}
