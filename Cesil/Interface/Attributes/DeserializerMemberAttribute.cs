using System;

namespace Cesil
{
    /// <summary>
    /// Attach this attribute to a method, field, property, or parameter to expose it as deserializable and configure it's "Parser", 
    /// "MemberRequired", "Reset", and "Order" behaviors.
    /// 
    /// If attached to a property, the propery must have a setter and take no parameters.
    /// 
    /// If attached to a method it must return void.
    /// 
    /// If attached to a parameter, the parameter must be to a constructor, all parameters to the constructor must also be annotated, 
    /// and the constructor itself must be annotated with DeserializerInstanceProviderAttribute.
    /// 
    /// If attached to a static method it may:
    ///  - take 1 parameter (the result of the parser) or
    ///  - take 2 parameters, the result of the parser and an `in ReadContext` or
    ///  - take 2 parameters, the row type (which may be passed by ref), and the result of the parser or
    ///  - take 3 parameters, the row type (which may be passed by ref), the result of the parser, and `in ReadContext`
    /// 
    /// If attached to an instance method it may:
    ///  - be on the row type, and take 1 parameter (the result of the parser) or
    ///  - be on the row type, and take 2 parameters, the result of the parser and an `in ReadContext`
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public sealed class DeserializerMemberAttribute : Attribute, IEquatable<DeserializerMemberAttribute>
    {
        /// <summary>
        /// The name of the column which maps to this member.
        /// 
        /// For fields and properties, this defaults to their declared name.
        /// 
        /// For methods, this must be explicitly set.
        /// </summary>
        [NullableExposed("Truly optional, except with method backed getters")]
        public string? Name { get; set; }

        /// <summary>
        /// Type to lookup a parser method on, used with ParserMethodName.
        /// 
        /// If null, defaults to a built in Parser for the type
        /// of this member (if any).
        /// 
        /// If non-null, ParserMethodName must also be set.  
        /// 
        /// The type must be public (or internal, if declared in the same assembly as the annotated type).
        /// </summary>
        [NullableExposed("Truly optional")]
        public Type? ParserType { get; set; }

        /// <summary>
        /// Name of parser method, used with ParserType.
        /// 
        /// If non-null, ParserType must also be set.
        /// 
        ///  The method must:
        ///  - be static
        ///  - return a bool
        ///  - have 3 parameters
        ///     * ReadOnlySpan(char)
        ///     * in ReadContext, 
        ///     * out assignable to outputType
        /// </summary>
        [NullableExposed("Truly optional")]
        public string? ParserMethodName { get; set; }

        /// <summary>
        /// Whether or not this column is required.
        /// 
        /// Defaults to No, except for on constructor parameters (where it
        /// must be Yes).
        /// </summary>
        public MemberRequired MemberRequired { get; set; } = MemberRequired.No;

        /// <summary>
        /// Type to lookup a reset method on, used with ResetMethodName.
        /// 
        /// If non-null, ResetMethodName must also be set.
        /// </summary>
        [NullableExposed("Truly optional")]
        public Type? ResetType { get; set; }

        /// <summary>
        /// Name of reset method, used with ResetType.
        /// 
        /// If non-null, ResetType must also be set.
        /// 
        /// The method must return void.
        /// 
        /// If referring to an instance method it can take:
        ///  - zero parameters or 
        ///  - a single `in ReadContext` parameter.
        /// 
        /// If referring to a static method it can take:
        ///  - zero parameters or
        ///  - a single parameter of the row type or
        ///  - a single parameter of `in ReadContext` or
        ///  - two parameters, the first of the row type (which may be by ref) and the second of `in ReadContext`
        /// </summary>
        [NullableExposed("Truly optional")]
        public string? ResetMethodName { get; set; }

        private int? _Order;

        /// <summary>
        /// Returns true if Order has been explicitly set.
        /// 
        /// Getting Order when HasOrder is false will throw an exception.
        /// </summary>
        [IntentionallyExposedPrimitive("Best way to indicate if set")]
        public bool HasOrder => _Order.HasValue;

        /// <summary>
        /// Value used to order columns.
        /// 
        /// Set to null to leave order unspecified.
        /// 
        /// Orders with explicit values are sorted before those without values.
        /// 
        /// Check HashOrder before getting Order, or an exception will be thrown.
        /// </summary>
        [IntentionallyExposedPrimitive("Matches Order on SerializableMember")]
        [NullableExposed("Truly optional")]
        public int Order
        {
            get
            {
                if (_Order == null)
                {
                    Throw.InvalidOperationException($"{nameof(Order)} not set, check {nameof(HasOrder)} before calling this.");
                }

                return _Order.Value;
            }

            set
            {
                _Order = value;
            }
        }

        /// <summary>
        /// Create a GenerateDeserializableAttribute attribute.
        /// </summary>
        public DeserializerMemberAttribute() { }

        /// <summary>
        /// Returns true if the given DeserializerMemberAttribute is equal
        /// to this one.
        /// </summary>
        public bool Equals(DeserializerMemberAttribute? attribute)
        {
            if (ReferenceEquals(attribute, null))
            {
                return false;
            }

            return
                MemberRequired == attribute.MemberRequired &&
                Name == attribute.Name &&
                _Order == attribute._Order &&
                ParserMethodName == attribute.ParserMethodName &&
                ParserType == attribute.ParserType &&
                ResetMethodName == attribute.ResetMethodName &&
                ResetType == attribute.ResetType;
        }

        /// <summary>
        /// Return true if the given object is a DeserializerMemberAttribute 
        ///   equal to this one.
        /// </summary>
        public override bool Equals(object? obj)
        => Equals(obj as DeserializerMemberAttribute);


        /// <summary>
        /// Returns a stable hash code for this DeserializerMemberAttribute.
        /// </summary>
        public override int GetHashCode()
        => HashCode.Combine(Name, _Order, MemberRequired, ParserMethodName, ParserType, ResetMethodName, ResetType);

        /// <summary>
        /// Compare two DeserializerMemberAttribute for equality
        /// </summary>
        public static bool operator ==(DeserializerMemberAttribute a, DeserializerMemberAttribute b)
        => Utils.NullReferenceEquality(a, b);

        /// <summary>
        /// Compare two DeserializerMemberAttribute for inequality
        /// </summary>
        public static bool operator !=(DeserializerMemberAttribute a, DeserializerMemberAttribute b)
        => !(a == b);

        /// <summary>
        /// Returns a representation of this DeserializerMemberAttribute object.
        /// 
        /// Only for debugging, this value is not guaranteed to be stable.
        /// </summary>
        public override string ToString()
        => $"{nameof(DeserializerMemberAttribute)} with {nameof(Name)}={Name}, {nameof(Order)}={_Order}, {nameof(MemberRequired)}={MemberRequired}, {nameof(ParserMethodName)}={ParserMethodName}, {nameof(ParserType)}={ParserType}, {nameof(ResetMethodName)}={ResetMethodName}, {nameof(ResetType)}={ResetType}";
    }
}
