namespace Cesil
{
    internal enum NeedsEncodeMode : byte
    {
        None = 0,

        // no escapes or comments configured
        SeparatorAndLineEndings,

        // escapes configured, but not comments
        SeparatorLineEndingsEscapeStart,

        // comments configured, but no escapes
        SeparatorLineEndingsComment,

        // everything configured
        SeparatorLineEndingsEscapeStartComment
    }
}
