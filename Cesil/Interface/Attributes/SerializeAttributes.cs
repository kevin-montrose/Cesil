using System;

namespace Cesil
{
    /// <summary>
    /// When using Cesil's Source Generator (see Nuget.org for Cesil.SourceGenerator) marks a class or struct
    /// as needing a serializer generated at compile time.
    /// 
    /// When using the DefaultTypeDescriber or the SourceGeneratorTypeDescriber, the created I(Async)Writers
    /// for this type will do no runtime code generation.
    /// 
    /// You can customize the behavior of the generated serialize with [DataMemberAttribute],
    /// and [GenerateSerializableMemberAttribute] attributes.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = true)]
    public sealed class GenerateSerializableAttribute: Attribute
    {
        /// <summary>
        /// Create a GenerateSerializableAttribute attribute.
        /// </summary>
        public GenerateSerializableAttribute() { }
    }

    /// <summary>
    /// Attach this attribute to a method, field, or property to expose it as serializable and configure it's "ShouldSerialize" and
    /// "Formatter" behaviors.
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
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class GenerateSerializableMemberAttribute: Attribute
    {
        /// <summary>
        /// The name of the column this member's returned value will be serialized under.
        /// 
        /// For fields and properties, this defaults to their declared name.
        /// 
        /// For methods, this must be explicitly set.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Value used to order columns.
        /// 
        /// Set to null to leave order unspecified.
        /// 
        /// Orders with explicit values are sorted before those with null values.
        /// </summary>
        public int? Order { get; set; }

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
        public Type? FormatterType { get; set; }

        /// <summary>
        /// Name of formatter method, used with FormatterType.
        /// 
        /// If non-null, FormatterType must also be set.
        /// 
        /// The method must:
        ///  - be public (or internal, if declared in the same assembly as the annotated type)
        ///  - static
        ///  - have three paramters:
        ///    1. a type which the annotated member's type can be assigned to
        ///    2. in WriteContext
        ///    3. IBufferWriter(char)
        /// </summary>
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
        public string? ShouldSerializeMethodName { get; set; }

        /// <summary>
        /// Whether or not to emit the default value.
        /// 
        /// Defaults to true.
        /// </summary>
        public bool EmitDefaultValue { get; set; } = true;

        /// <summary>
        /// Create a GenerateSerializableMemberAttribute attribute.
        /// 
        /// This will expose the member for serialization if it would not otherwise be exposed.
        /// 
        /// If on a method, a Name must be set.
        /// </summary>
        public GenerateSerializableMemberAttribute() { }
    }
}
