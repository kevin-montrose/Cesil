using System;

namespace Cesil
{
    /// <summary>
    /// An exception that should never happen.
    /// 
    /// If it does, that indicates a bug in Cesil.  Kindly report it on https://github.com/kevin-montrose/Cesil/issues/new.
    /// </summary>
    [NotEquatable("It's an exception, equality is nonsensical")]
    public sealed class ImpossibleException : Exception
    {
        private ImpossibleException(string message) : base(message) { }

        internal static ImpossibleException Create(string reason, string fileName, string memberName, int lineNumber)
        => new ImpossibleException(
            DefaultMessage(reason, fileName, memberName, lineNumber)
        );

        internal static ImpossibleException Create(string reason, string fileName, string memberName, int lineNumber, Options options)
        => new ImpossibleException(
            DefaultMessage(reason, fileName, memberName, lineNumber) + "\r\n" +
            OptionsMessage(options)
        );

        internal static ImpossibleException Create<T>(string reason, string fileName, string memberName, int lineNumber, IBoundConfiguration<T> config)
        => new ImpossibleException(
            DefaultMessage(reason, fileName, memberName, lineNumber) + "\r\n" +
            $"Bound to {typeof(T).FullName ?? typeof(T).Name}\r\n" +
            (config is ConcreteBoundConfiguration<T> ? "Concrete binding" : "Dynamic binding") + "\r\n" +
            OptionsMessage(config.Options)
        );

        private static string OptionsMessage(Options options)
        => $"With options: {options}";

        private static string DefaultMessage(string reason, string fileName, string memberName, int lineNumber)
        => $"The impossible has happened!\r\n{reason}\r\nFile: {fileName}\r\nMember: {memberName}\r\nLine: {lineNumber}\r\nPlease report this to https://github.com/kevin-montrose/Cesil/issues/new";

        /// <summary>
        /// Returns a representation of this ImpossibleException object.
        /// 
        /// Only for debugging, this value is not guaranteed to be stable.
        /// </summary>
        public override string ToString()
        => $"{nameof(ImpossibleException)} with Message:\r\n{Message}\r\n\r\nAnd StackTrace:\r\n{StackTrace}";
    }
}
