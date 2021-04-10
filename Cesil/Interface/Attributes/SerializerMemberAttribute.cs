using System;

namespace Cesil
{
    /// <summary>
    /// Attach this attribute to a method, field, or property to expose it as serializable and configure it's "ShouldSerialize",
    /// "Formatter", "Order", and "EmitDefaultValue" behaviors.
    /// 
    /// If attached to a property, the propery must have a getter and take no parameters.
    /// 
    /// If attached to a method, the method must return a non-null value and either:
    ///  - be an instance method and
    ///    * take no parameters
    ///    * take one parameter, an in WriteContext
    ///  - be static and
    ///    * take no parameters
    ///    * take one parameter either
    ///      - an in WriteContext or
    ///      - a type to which the declaring type of this member can be assigned
    ///    * take two parameters
    ///      1. a type to which the declaring type of this member can be assigned
    ///      2. in WriteContext
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class SerializerMemberAttribute : Attribute, IEquatable<SerializerMemberAttribute>
    {
        /// <summary>
        /// The name of the column this member's returned value will be serialized under.
        /// 
        /// For fields and properties, this defaults to their declared name.
        /// 
        /// For methods, this must be explicitly set.
        /// </summary>
        [NullableExposed("Truly optional, except with method backed getters")]
        public string? Name { get; set; }

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
        /// Type to lookup a formatter method on, used with FormatterMethodName.
        /// 
        /// If null, defaults to a built in Formatter for the type
        /// of this member (if any).
        /// 
        /// If non-null, FormatterMethodName must also be set.  
        /// 
        /// The type must be public (or internal, if declared in the same assembly as the annotated type).
        /// </summary>
        [NullableExposed("Truly optional")]
        public Type? FormatterType { get; set; }

        /// <summary>
        /// Name of formatter method, used with FormatterType.
        /// 
        /// If non-null, FormatterType must also be set.
        /// 
        /// The method must:
        ///  - be public (or internal, if declared in the same assembly as the annotated type)
        ///  - static
        ///  - return bool
        ///  - have three paramters:
        ///    1. a type which the annotated member's type can be assigned to
        ///    2. in WriteContext
        ///    3. IBufferWriter(char)
        /// </summary>
        [NullableExposed("Truly optional")]
        public string? FormatterMethodName { get; set; }

        /// <summary>
        /// Type to lookup a should serialize method on, used with ShouldSerializeMethodName.
        /// 
        /// If null, no should seralize is used.
        /// 
        /// If non-null, ShouldSerializeMethodName must also be set.
        /// 
        /// The type must be public (or internal, if declared in the same assembly as the annotated type).
        /// </summary>
        [NullableExposed("Truly optional")]
        public Type? ShouldSerializeType { get; set; }

        /// <summary>
        /// Name of should serialize method, used with ShouldSerializeType.
        /// 
        /// If non-null, ShouldSerializeType must also be set.
        /// 
        /// The method must either
        ///  - be public (or internal, if declared in the same assembly as the annotated type)
        ///  - instance method
        ///  - take 
        ///    1. no parameters
        ///    2. one in WriteContext parameter
        ///  - return bool
        /// or
        ///  - be public (or internal, if declared in the same assembly as the annotated type)
        ///  - static
        ///  - take 
        ///    1. no parameters
        ///    2. one parameter of a type which declares the annotated member
        ///    3. two parameters
        ///       1. a type which declares the annotated member
        ///       2. in WriteContext
        ///  - return bool
        /// </summary>
        [NullableExposed("Truly optional")]
        public string? ShouldSerializeMethodName { get; set; }

        /// <summary>
        /// Whether or not to emit the default value.
        /// 
        /// Defaults to Yes.
        /// </summary>
        public EmitDefaultValue EmitDefaultValue { get; set; } = EmitDefaultValue.Yes;

        /// <summary>
        /// Create a SerializerMemberAttribute attribute.
        /// 
        /// This will expose the member for serialization if it would not otherwise be exposed.
        /// 
        /// If on a method, a Name must be set.
        /// </summary>
        public SerializerMemberAttribute() { }

        /// <summary>
        /// Returns true if the given SerializerMemberAttribute is equal
        /// to this one.
        /// </summary>
        public bool Equals(SerializerMemberAttribute? attribute)
        {
            if (ReferenceEquals(attribute, null))
            {
                return false;
            }

            return
                attribute.EmitDefaultValue == EmitDefaultValue &&
                attribute.FormatterMethodName == FormatterMethodName &&
                attribute.FormatterType == FormatterType &&
                attribute.Name == Name &&
                attribute._Order == _Order &&
                attribute.ShouldSerializeMethodName == ShouldSerializeMethodName &&
                attribute.ShouldSerializeType == ShouldSerializeType;
        }

        /// <summary>
        /// Return true if the given object is a SerializerMemberAttribute 
        ///   equal to this one.
        /// </summary>
        public override bool Equals(object? obj)
        => Equals(obj as SerializerMemberAttribute);


        /// <summary>
        /// Returns a hash code for this SerializerMemberAttribute.
        /// </summary>
        public override int GetHashCode()
        => HashCode.Combine(EmitDefaultValue, FormatterMethodName, FormatterType, Name, _Order, ShouldSerializeMethodName, ShouldSerializeType);

        /// <summary>
        /// Compare two SerializerMemberAttributes for equality
        /// </summary>
        public static bool operator ==(SerializerMemberAttribute a, SerializerMemberAttribute b)
        => Utils.NullReferenceEquality(a, b);

        /// <summary>
        /// Compare two SerializerMemberAttributes for inequality
        /// </summary>
        public static bool operator !=(SerializerMemberAttribute a, SerializerMemberAttribute b)
        => !(a == b);

        /// <summary>
        /// Returns a representation of this SerializerMemberAttribute object.
        /// 
        /// Only for debugging, this value is not guaranteed to be stable.
        /// </summary>
        public override string ToString()
        => $"{nameof(SerializerMemberAttribute)} with {nameof(EmitDefaultValue)}={EmitDefaultValue}, {nameof(FormatterMethodName)}={FormatterMethodName}, {nameof(FormatterType)}={FormatterType}, {nameof(Name)}={Name}, {nameof(HasOrder)}={HasOrder}, {nameof(Order)}={_Order}, {nameof(ShouldSerializeMethodName)}={ShouldSerializeMethodName}, {nameof(ShouldSerializeType)}={ShouldSerializeType}";
    }
}
