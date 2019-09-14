using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Cesil
{
    /// <summary>
    /// How to behave if a ManualTypeDescriber needs to
    ///   describe a type that isn't explicitly configured.
    /// </summary>
    public enum ManualTypeDescriberFallbackBehavior : byte
    {   
        /// <summary>
        /// Throw if no type is configured.
        /// </summary>
        Throw = 1,
        /// <summary>
        /// Use DefaultTypeDescriber if no type is configured.
        /// </summary>
        UseDefault = 2
    }

    /// <summary>
    /// An ITypeDescriber that takes lets you register explicit members to return
    ///   when one of the EnumerateXXX() methods are called.
    /// </summary>
    [NotEquatable("Mutable")]
    public sealed class ManualTypeDescriber : ITypeDescriber
    {
        internal bool ThrowsOnNoConfiguredType { get; }

        private readonly Dictionary<TypeInfo, InstanceBuilder> Builders;

        private readonly Dictionary<TypeInfo, List<SerializableMember>> Serializers;
        private readonly Dictionary<TypeInfo, List<DeserializableMember>> Deserializers;

        /// <summary>
        /// Creates a new ManualTypeDescriber.
        /// </summary>
        public ManualTypeDescriber(ManualTypeDescriberFallbackBehavior fallbackBehavior)
        {
            switch (fallbackBehavior)
            {
                case ManualTypeDescriberFallbackBehavior.Throw: ThrowsOnNoConfiguredType = true; break;
                case ManualTypeDescriberFallbackBehavior.UseDefault: ThrowsOnNoConfiguredType = false; break;
                default:
                    Throw.ArgumentException<object>($"Unexpected {nameof(ManualTypeDescriberFallbackBehavior)}: {fallbackBehavior}", nameof(fallbackBehavior));
                    return;
            }

            Builders = new Dictionary<TypeInfo, InstanceBuilder>();
            Serializers = new Dictionary<TypeInfo, List<SerializableMember>>();
            Deserializers = new Dictionary<TypeInfo, List<DeserializableMember>>();
        }

        /// <summary>
        /// Creates a new ManualTypeDescriber.
        /// 
        /// Uses DefaultTypeDescriber if no type is configured for a given enumeration.
        /// </summary>
        public ManualTypeDescriber() : this(ManualTypeDescriberFallbackBehavior.UseDefault) { }

        // set builders

        /// <summary>
        /// Set the delegate to use when constructing new instances of 
        ///   type T.
        /// </summary>
        public void SetBuilder(InstanceBuilder build)
        => SetBuilder(build?.ConstructsType, build);

        /// <summary>
        /// Set the delegate to use when constructing new instances of 
        ///   the given type.
        /// </summary>
        public void SetBuilder(TypeInfo forType, InstanceBuilder builder)
        {
            if (forType == null)
            {
                Throw.ArgumentNullException<object>(nameof(forType));
            }

            if (builder == null)
            {
                Throw.ArgumentNullException<object>(nameof(builder));
            }

            var createdType = builder.ConstructsType;
            if (!forType.IsAssignableFrom(createdType))
            {
                Throw.InvalidOperationException<object>($"{forType} cannot be assigned from {createdType}, constructed by {builder}");
            }

            Builders[forType] = builder;
        }

        // explicit getter

        /// <summary>
        /// Add a getter for the given type, with the given name, using the given getter.
        /// </summary>
        public void AddExplicitGetter(TypeInfo forType, string name, Getter getter)
        {
            var formatter = getter != null ? Formatter.GetDefault(getter.Returns) : null;
            AddSerializableMember(forType, getter, name, formatter, null, WillEmitDefaultValue.Yes);
        }

        /// <summary>
        /// Add a getter for the given type, with the given name, using the given getter, and formatter.
        /// </summary>
        public void AddExplicitGetter(TypeInfo forType, string name, Getter getter, Formatter formatter)
        => AddSerializableMember(forType, getter, name, formatter, null, WillEmitDefaultValue.Yes);

        /// <summary>
        /// Add a getter for the given type, with the given name, using the given getter, formatter, and ShouldSerialize method.
        /// </summary>
        public void AddExplicitGetter(TypeInfo forType, string name, Getter getter, Formatter formatter, ShouldSerialize shouldSerialize)
        {
            if (shouldSerialize == null)
            {
                Throw.ArgumentNullException<object>(nameof(shouldSerialize));
            }

            AddSerializableMember(forType, getter, name, formatter, shouldSerialize, WillEmitDefaultValue.Yes);
        }

        /// <summary>
        /// Add a getter for the given type, with the given name, using the given getter, formatter, ShouldSerialize method, and whether to emit a default value.
        /// </summary>
        public void AddExplicitGetter(TypeInfo forType, string name, Getter getter, Formatter formatter, ShouldSerialize shouldSerialize, WillEmitDefaultValue emitDefaultValue)
        {
            if (shouldSerialize == null)
            {
                Throw.ArgumentNullException<object>(nameof(shouldSerialize));
            }

            AddSerializableMember(forType, getter, name, formatter, shouldSerialize, emitDefaultValue);
        }

        // serializing fields

        /// <summary>
        /// Add a field to serialize for the given type.
        /// </summary>
        public void AddSerializableField(TypeInfo forType, FieldInfo field)
        {
            var formatter = field != null ? Formatter.GetDefault(field.FieldType.GetTypeInfo()) : null;
            AddSerializableMember(forType, (Getter)field, field?.Name, formatter, null, WillEmitDefaultValue.Yes);
        }

        /// <summary>
        /// Add a field to serialize for the given type, using the given name.
        /// </summary>
        public void AddSerializableField(TypeInfo forType, FieldInfo field, string name)
        {
            var formatter = field != null ? Formatter.GetDefault(field.FieldType.GetTypeInfo()) : null;
            AddSerializableMember(forType, (Getter)field, name, formatter, null, WillEmitDefaultValue.Yes);
        }

        /// <summary>
        /// Add a field to serialize for the given type, using the given name and formatter.
        /// </summary>
        public void AddSerializableField(TypeInfo forType, FieldInfo field, string name, Formatter formatter)
        => AddSerializableMember(forType, (Getter)field, name, formatter, null, WillEmitDefaultValue.Yes);

        /// <summary>
        /// Add a field to serialize for the given type, using the given name, formatter, and ShouldSerialize method.
        /// </summary>
        public void AddSerializableField(TypeInfo forType, FieldInfo field, string name, Formatter formatter, ShouldSerialize shouldSerialize)
        {
            if (shouldSerialize == null)
            {
                Throw.ArgumentNullException<object>(nameof(shouldSerialize));
            }

            AddSerializableMember(forType, (Getter)field, name, formatter, shouldSerialize, WillEmitDefaultValue.Yes);
        }

        /// <summary>
        /// Add a field to serialize for the given type, using the given name, formatter, and ShouldSerialize method.
        /// </summary>
        public void AddSerializableField(TypeInfo forType, FieldInfo field, string name, Formatter formatter, ShouldSerialize shouldSerialize, WillEmitDefaultValue emitDefaultValue)
        {
            if (shouldSerialize == null)
            {
                Throw.ArgumentNullException<object>(nameof(shouldSerialize));
            }

            AddSerializableMember(forType, (Getter)field, name, formatter, shouldSerialize, emitDefaultValue);
        }

        /// <summary>
        /// Add a field to serialize for the type which declares the field.
        /// </summary>
        public void AddSerializableField(FieldInfo field)
        {
            var formatter = field != null ? Formatter.GetDefault(field.FieldType.GetTypeInfo()) : null;

            AddSerializableMember(field?.DeclaringType.GetTypeInfo(), (Getter)field, field?.Name, formatter, null, WillEmitDefaultValue.Yes);
        }

        /// <summary>
        /// Add a field to serialize with the given name - for the type which declares the field.
        /// </summary>
        public void AddSerializableField(FieldInfo field, string name)
        {
            var formatter = field != null ? Formatter.GetDefault(field.FieldType.GetTypeInfo()) : null;

            AddSerializableMember(field?.DeclaringType.GetTypeInfo(), (Getter)field, name, formatter, null, WillEmitDefaultValue.Yes);
        }

        /// <summary>
        /// Add a field to serialize with the given name and formatter - for the type which declares the field.
        /// </summary>
        public void AddSerializableField(FieldInfo field, string name, Formatter formatter)
        => AddSerializableMember(field?.DeclaringType.GetTypeInfo(), (Getter)field, name, formatter, null, WillEmitDefaultValue.Yes);

        /// <summary>
        /// Add a field to serialize with the given name, formatter, and ShouldSerialize method - for the type which declares the field.
        /// </summary>
        public void AddSerializableField(FieldInfo field, string name, Formatter formatter, ShouldSerialize shouldSerialize)
        {
            if (shouldSerialize == null)
            {
                Throw.ArgumentNullException<object>(nameof(shouldSerialize));
            }

            AddSerializableMember(field?.DeclaringType.GetTypeInfo(), (Getter)field, name, formatter, shouldSerialize, WillEmitDefaultValue.Yes);
        }

        /// <summary>
        /// Add a field to serialize with the given name, formatter, ShouldSerialize method, and whether to emit a default value - for the type which declares the field.
        /// </summary>
        public void AddSerializableField(FieldInfo field, string name, Formatter formatter, ShouldSerialize shouldSerialize, WillEmitDefaultValue emitDefaultValue)
        {
            if (shouldSerialize == null)
            {
                Throw.ArgumentNullException<object>(nameof(shouldSerialize));
            }

            AddSerializableMember(field?.DeclaringType.GetTypeInfo(), (Getter)field, name, formatter, shouldSerialize, emitDefaultValue);
        }

        // serializing properties

        /// <summary>
        /// Add a property to serialize for the given type.
        /// </summary>
        public void AddSerializableProperty(TypeInfo forType, PropertyInfo prop)
        {
            var formatter = prop != null ? Formatter.GetDefault(prop.PropertyType.GetTypeInfo()) : null;

            AddSerializableMember(forType, (Getter)prop?.GetMethod, prop?.Name, formatter, null, WillEmitDefaultValue.Yes);
        }

        /// <summary>
        /// Add a property to serialize for the given type with the given name.
        /// </summary>
        public void AddSerializableProperty(TypeInfo forType, PropertyInfo prop, string name)
        {
            var formatter = prop != null ? Formatter.GetDefault(prop.PropertyType.GetTypeInfo()) : null;

            AddSerializableMember(forType, (Getter)prop?.GetMethod, name, formatter, null, WillEmitDefaultValue.Yes);
        }

        /// <summary>
        /// Add a property to serialize for the given type with the given name and formatter.
        /// </summary>
        public void AddSerializableProperty(TypeInfo forType, PropertyInfo prop, string name, Formatter formatter)
        => AddSerializableMember(forType, (Getter)prop?.GetMethod, name, formatter, null, WillEmitDefaultValue.Yes);

        /// <summary>
        /// Add a property to serialize for the given type with the given name, formatter, and ShouldSerialize method.
        /// </summary>
        public void AddSerializableProperty(TypeInfo forType, PropertyInfo prop, string name, Formatter formatter, ShouldSerialize shouldSerialize)
        {
            if (shouldSerialize == null)
            {
                Throw.ArgumentNullException<object>(nameof(shouldSerialize));
            }

            AddSerializableMember(forType, (Getter)prop?.GetMethod, name, formatter, shouldSerialize, WillEmitDefaultValue.Yes);
        }

        /// <summary>
        /// Add a property to serialize for the given type with the given name, formatter, ShouldSerialize method, and whether to emit a default value.
        /// </summary>
        public void AddSerializableProperty(TypeInfo forType, PropertyInfo prop, string name, Formatter formatter, ShouldSerialize shouldSerialize, WillEmitDefaultValue emitDefaultValue)
        {
            if (shouldSerialize == null)
            {
                Throw.ArgumentNullException<object>(nameof(shouldSerialize));
            }

            AddSerializableMember(forType, (Getter)prop?.GetMethod, name, formatter, shouldSerialize, emitDefaultValue);
        }

        /// <summary>
        /// Add a property to serialize for the type which declares the property.
        /// </summary>
        public void AddSerializableProperty(PropertyInfo prop)
        {
            var formatter = prop != null ? Formatter.GetDefault(prop.PropertyType.GetTypeInfo()) : null;

            AddSerializableMember(prop?.DeclaringType.GetTypeInfo(), (Getter)prop?.GetMethod, prop?.Name, formatter, null, WillEmitDefaultValue.Yes);
        }

        /// <summary>
        /// Add a property to serialize with the given name - for the type which declares the property.
        /// </summary>
        public void AddSerializableProperty(PropertyInfo prop, string name)
        {
            var formatter = prop != null ? Formatter.GetDefault(prop.PropertyType.GetTypeInfo()) : null;

            AddSerializableMember(prop?.DeclaringType.GetTypeInfo(), (Getter)prop?.GetMethod, name, formatter, null, WillEmitDefaultValue.Yes);
        }

        /// <summary>
        /// Add a property to serialize with the given name and formatter - for the type which declares the property.
        /// </summary>
        public void AddSerializableProperty(PropertyInfo prop, string name, Formatter formatter)
        => AddSerializableMember(prop?.DeclaringType.GetTypeInfo(), (Getter)prop?.GetMethod, name, formatter, null, WillEmitDefaultValue.Yes);

        /// <summary>
        /// Add a property to serialize with the given name, formatter, and ShouldSerialize method - for the type which declares the property.
        /// </summary>
        public void AddSerializableProperty(PropertyInfo prop, string name, Formatter formatter, ShouldSerialize shouldSerialize)
        {
            if (shouldSerialize == null)
            {
                Throw.ArgumentNullException<object>(nameof(shouldSerialize));
            }

            AddSerializableMember(prop?.DeclaringType.GetTypeInfo(), (Getter)prop?.GetMethod, name, formatter, shouldSerialize, WillEmitDefaultValue.Yes);
        }

        /// <summary>
        /// Add a property to serialize with the given name, formatter, ShouldSerialize method, and whether to emit a default value - for the type which declares the property.
        /// </summary>
        public void AddSerializableProperty(PropertyInfo prop, string name, Formatter formatter, ShouldSerialize shouldSerialize, WillEmitDefaultValue emitDefaultValue)
        {
            if (shouldSerialize == null)
            {
                Throw.ArgumentNullException<object>(nameof(shouldSerialize));
            }

            AddSerializableMember(prop?.DeclaringType.GetTypeInfo(), (Getter)prop?.GetMethod, name, formatter, shouldSerialize, emitDefaultValue);
        }

        // internal for testing purposes
        internal void AddSerializableMember(TypeInfo forType, Getter getter, string name, Formatter formatter, ShouldSerialize shouldSerialize, WillEmitDefaultValue emitDefaultValue)
        {
            if (forType == null)
            {
                Throw.ArgumentNullException<object>(nameof(forType));
            }

            if (getter == null)
            {
                Throw.ArgumentNullException<object>(nameof(getter));
            }

            if (name == null)
            {
                Throw.ArgumentNullException<object>(nameof(name));
            }

            if (formatter == null)
            {
                Throw.ArgumentNullException<object>(nameof(name));
            }

            // shouldSerialize can be null

            if (getter.RowType != null)
            {
                var getterOnType = getter.RowType;
                var isLegal = false;
                var cur = forType;

                while (cur != null)
                {
                    if (cur == getterOnType)
                    {
                        isLegal = true;
                        break;
                    }

                    cur = cur.BaseType?.GetTypeInfo();
                }

                if (!isLegal)
                {
                    Throw.InvalidOperationException<object>($"Provided getter ({getter}) is not on {forType} or one of it's base types.");
                }
            }

            var toAdd = SerializableMember.Create(forType, name, getter, formatter, shouldSerialize, emitDefaultValue);

            if (!Serializers.TryGetValue(forType, out var s))
            {
                Serializers[forType] = s = new List<SerializableMember>();
            }

            s.Add(toAdd);
        }

        // explicit setter

        /// <summary>
        /// Add a setter for the given type, with the given name, using the given setter.
        /// </summary>
        public void AddExplicitSetter(TypeInfo forType, string name, Setter setter)
        {
            // tricky null stuff here to defer validation until the AddXXX call
            var defaultParser = setter != null ? Parser.GetDefault(setter.Takes) : null;

            AddDeserializeMember(forType, setter, name, defaultParser, IsMemberRequired.No, null);
        }

        /// <summary>
        /// Add a setter for the given type, with the given name, using the given setter and parser.
        /// </summary>
        public void AddExplicitSetter(TypeInfo forType, string name, Setter setter, Parser parser)
        => AddDeserializeMember(forType, setter, name, parser, IsMemberRequired.No, null);

        /// <summary>
        /// Add a setter for the given type, with the given name, using the given setter, parser, and whether the column is required.
        /// </summary>
        public void AddExplicitSetter(TypeInfo forType, string name, Setter setter, Parser parser, IsMemberRequired isRequired)
        => AddDeserializeMember(forType, setter, name, parser, isRequired, null);

        /// <summary>
        /// Add a setter for the given type, with the given name, using the given setter, parser, whether the column is required, and a reset method.
        /// </summary>
        public void AddExplicitSetter(TypeInfo forType, string name, Setter setter, Parser parser, IsMemberRequired isRequired, Reset reset)
        {
            if (reset == null)
            {
                Throw.ArgumentNullException<object>(nameof(reset));
            }

            AddDeserializeMember(forType, setter, name, parser, isRequired, reset);
        }

        // deserialize fields

        /// <summary>
        /// Add a field to deserialize for the given type.
        /// </summary>
        public void AddDeserializableField(TypeInfo forType, FieldInfo field)
        {
            var parser = field != null ? Parser.GetDefault(field.FieldType.GetTypeInfo()) : null;

            AddDeserializeMember(forType, (Setter)field, field?.Name, parser, IsMemberRequired.No, null);
        }

        /// <summary>
        /// Add a field to deserialize for the given type with the given name.
        /// </summary>
        public void AddDeserializableField(TypeInfo forType, FieldInfo field, string name)
        {
            var parser = field != null ? Parser.GetDefault(field.FieldType.GetTypeInfo()) : null;

            AddDeserializeMember(forType, (Setter)field, name, parser, IsMemberRequired.No, null);
        }

        /// <summary>
        /// Add a field to deserialize for the given type with the given name and parser.
        /// </summary>
        public void AddDeserializableField(TypeInfo forType, FieldInfo field, string name, Parser parser)
        => AddDeserializeMember(forType, (Setter)field, name, parser, IsMemberRequired.No, null);

        /// <summary>
        /// Add a field to deserialize for the given type with the given name, parser, and whether the column is required.
        /// </summary>
        public void AddDeserializableField(TypeInfo forType, FieldInfo field, string name, Parser parser, IsMemberRequired isRequired)
        => AddDeserializeMember(forType, (Setter)field, name, parser, isRequired, null);

        /// <summary>
        /// Add a field to deserialize for the given type with the given name, parser, whether the column is required, and a reset method.
        /// </summary>
        public void AddDeserializableField(TypeInfo forType, FieldInfo field, string name, Parser parser, IsMemberRequired isRequired, Reset reset)
        {
            if (reset == null)
            {
                Throw.ArgumentNullException<object>(nameof(reset));
            }

            AddDeserializeMember(forType, (Setter)field, name, parser, isRequired, reset);
        }

        /// <summary>
        /// Add a field to deserialize for the type which declares the field.
        /// </summary>
        public void AddDeserializableField(FieldInfo field)
        {
            var parser = field != null ? Parser.GetDefault(field.FieldType.GetTypeInfo()) : null;

            AddDeserializeMember(field?.DeclaringType.GetTypeInfo(), (Setter)field, field?.Name, parser, IsMemberRequired.No, null);
        }

        /// <summary>
        /// Add a field to deserialize with the given name - for the type which declares the field.
        /// </summary>
        public void AddDeserializableField(FieldInfo field, string name)
        {
            var parser = field != null ? Parser.GetDefault(field.FieldType.GetTypeInfo()) : null;

            AddDeserializeMember(field?.DeclaringType.GetTypeInfo(), (Setter)field, name, parser, IsMemberRequired.No, null);
        }

        /// <summary>
        /// Add a field to deserialize with the given name and parser - for the type which declares the field.
        /// </summary>
        public void AddDeserializableField(FieldInfo field, string name, Parser parser)
        => AddDeserializeMember(field?.DeclaringType.GetTypeInfo(), (Setter)field, name, parser, IsMemberRequired.No, null);

        /// <summary>
        /// Add a field to deserialize with the given name, parser, and whether the column is required - for the type which declares the field.
        /// </summary>
        public void AddDeserializableField(FieldInfo field, string name, Parser parser, IsMemberRequired isRequired)
        => AddDeserializeMember(field?.DeclaringType.GetTypeInfo(), (Setter)field, name, parser, isRequired, null);

        /// <summary>
        /// Add a field to deserialize with the given name, parser, whether the column is required, and a reset method - for the type which declares the field.
        /// </summary>
        public void AddDeserializableField(FieldInfo field, string name, Parser parser, IsMemberRequired isRequired, Reset reset)
        {
            if (reset == null)
            {
                Throw.ArgumentNullException<object>(nameof(reset));
            }

            AddDeserializeMember(field?.DeclaringType.GetTypeInfo(), (Setter)field, name, parser, isRequired, reset);
        }

        // deserialize properties

        /// <summary>
        /// Add a property to deserialize for the given type.
        /// </summary>
        public void AddDeserializableProperty(TypeInfo forType, PropertyInfo prop)
        {
            var parser = prop != null ? Parser.GetDefault(prop.PropertyType.GetTypeInfo()) : null;

            AddDeserializeMember(forType, (Setter)prop?.SetMethod, prop?.Name, parser, IsMemberRequired.No, null);
        }

        /// <summary>
        /// Add a property to deserialize for the given type with the given name.
        /// </summary>
        public void AddDeserializableProperty(TypeInfo forType, PropertyInfo prop, string name)
        {
            var parser = prop != null ? Parser.GetDefault(prop.PropertyType.GetTypeInfo()) : null;

            AddDeserializeMember(forType, (Setter)prop?.SetMethod, name, parser, IsMemberRequired.No, null);
        }

        /// <summary>
        /// Add a property to deserialize for the given type with the given name and parser.
        /// </summary>
        public void AddDeserializableProperty(TypeInfo forType, PropertyInfo prop, string name, Parser parser)
        => AddDeserializeMember(forType, (Setter)prop?.SetMethod, name, parser, IsMemberRequired.No, null);

        /// <summary>
        /// Add a property to deserialize for the given type with the given name and parser and whether the column is required.
        /// </summary>
        public void AddDeserializableProperty(TypeInfo forType, PropertyInfo prop, string name, Parser parser, IsMemberRequired isRequired)
        => AddDeserializeMember(forType, (Setter)prop?.SetMethod, name, parser, isRequired, null);

        /// <summary>
        /// Add a property to deserialize for the given type with the given name and parser, whether the column is required, and a reset method.
        /// </summary>
        public void AddDeserializableProperty(TypeInfo forType, PropertyInfo prop, string name, Parser parser, IsMemberRequired isRequired, Reset reset)
        {
            if (reset == null)
            {
                Throw.ArgumentNullException<object>(nameof(reset));
            }

            AddDeserializeMember(forType, (Setter)prop?.SetMethod, name, parser, isRequired, reset);
        }

        /// <summary>
        /// Add a property to deserialize for the type which declares the property.
        /// </summary>
        public void AddDeserializableProperty(PropertyInfo prop)
        {
            var parser = prop != null ? Parser.GetDefault(prop.PropertyType.GetTypeInfo()) : null;

            AddDeserializeMember(prop?.DeclaringType.GetTypeInfo(), (Setter)prop?.SetMethod, prop?.Name, parser, IsMemberRequired.No, null);
        }

        /// <summary>
        /// Add a property to deserialize with the given name - for the type which declares the property.
        /// </summary>
        public void AddDeserializableProperty(PropertyInfo prop, string name)
        {
            var parser = prop != null ? Parser.GetDefault(prop.PropertyType.GetTypeInfo()) : null;

            AddDeserializeMember(prop?.DeclaringType.GetTypeInfo(), (Setter)prop?.SetMethod, name, parser, IsMemberRequired.No, null);
        }

        /// <summary>
        /// Add a property to deserialize with the given name and parser - for the type which declares the property.
        /// </summary>
        public void AddDeserializableProperty(PropertyInfo prop, string name, Parser parser)
        => AddDeserializeMember(prop?.DeclaringType.GetTypeInfo(), (Setter)prop?.SetMethod, name, parser, IsMemberRequired.No, null);

        /// <summary>
        /// Add a property to deserialize with the given name, parser, and whether the column is required - for the type which declares the property.
        /// </summary>
        public void AddDeserializableProperty(PropertyInfo prop, string name, Parser parser, IsMemberRequired isRequired)
        => AddDeserializeMember(prop?.DeclaringType.GetTypeInfo(), (Setter)prop?.SetMethod, name, parser, isRequired, null);

        /// <summary>
        /// Add a property to deserialize with the given name, parser, whether the column is required, and a reset method - for the type which declares the property.
        /// </summary>
        public void AddDeserializableProperty(PropertyInfo prop, string name, Parser parser, IsMemberRequired isRequired, Reset reset)
        {
            if (reset == null)
            {
                Throw.ArgumentNullException<object>(nameof(reset));
            }

            AddDeserializeMember(prop?.DeclaringType.GetTypeInfo(), (Setter)prop?.SetMethod, name, parser, isRequired, reset);
        }

        private void AddDeserializeMember(TypeInfo forType, Setter setter, string name, Parser parser, IsMemberRequired isRequired, Reset reset)
        {
            if (forType == null)
            {
                Throw.ArgumentNullException<object>(nameof(forType));
            }

            if (setter == null)
            {
                Throw.ArgumentNullException<object>(nameof(setter));
            }

            if (name == null)
            {
                Throw.ArgumentNullException<object>(nameof(name));
            }

            if (parser == null)
            {
                Throw.ArgumentNullException<object>(nameof(parser));
            }

            var toAdd = DeserializableMember.Create(forType, name, setter, parser, isRequired, reset);

            if (!Deserializers.TryGetValue(forType, out var d))
            {
                Deserializers[forType] = d = new List<DeserializableMember>();
            }

            d.Add(toAdd);
        }

        /// <summary>
        /// Returns an InstanceBuilder that can construct the given type.
        /// 
        /// Will throw an expection if no builder is registered.
        /// </summary>
        public InstanceBuilder GetInstanceBuilder(TypeInfo forType)
        {
            if (forType == null)
            {
                return Throw.ArgumentNullException<InstanceBuilder>(nameof(forType));
            }

            if (!Builders.TryGetValue(forType, out var builder))
            {
                return Throw.InvalidOperationException<InstanceBuilder>($"No builder set for {forType}");
            }

            return builder;
        }

        /// <summary>
        /// Enumerate all the members on forType to deserialize.
        /// 
        /// If no members have been added for deserialization, will either return
        ///   an empty enumerable or throw an exception based on the value of 
        ///   ThrowsOnNoConfiguredType.
        /// </summary>
        public IEnumerable<DeserializableMember> EnumerateMembersToDeserialize(TypeInfo forType)
        {
            if (!Deserializers.TryGetValue(forType, out var ret))
            {
                if (ThrowsOnNoConfiguredType)
                {
                    return Throw.InvalidOperationException<IEnumerable<DeserializableMember>>($"No configured members to deserialize for {forType} ({nameof(ThrowsOnNoConfiguredType)} is set)");
                }

                return Enumerable.Empty<DeserializableMember>();
            }

            return ret;
        }

        /// <summary>
        /// Enumerate all the members on forType to serialize.
        /// 
        /// If no members have been added for serialization, will either return
        ///   an empty enumerable or throw an exception based on the value of 
        ///   ThrowsOnNoConfiguredType.
        /// </summary>
        public IEnumerable<SerializableMember> EnumerateMembersToSerialize(TypeInfo forType)
        {
            if (!Serializers.TryGetValue(forType, out var ret))
            {
                if (ThrowsOnNoConfiguredType)
                {
                    return Throw.InvalidOperationException<IEnumerable<SerializableMember>>($"No configured members to serialize for {forType} ({nameof(ThrowsOnNoConfiguredType)} is set)");
                }

                return Enumerable.Empty<SerializableMember>();
            }

            return ret;
        }

        /// <summary>
        /// Returns a representation of this ManualTypeDescriber object.
        /// 
        /// Only for debugging, this value is not guaranteed to be stable.
        /// </summary>
        public override string ToString()
        {
            var ret = new StringBuilder();

            ret.Append($"{nameof(ManualTypeDescriber)}");

            if (ThrowsOnNoConfiguredType)
            {
                ret.Append(" which throws on unconfigured types");
            }
            else
            {
                ret.Append(" which returns empty on unconfigured types");
            }

            if (Builders.Any())
            {
                ret.Append(" and builds (");

                var isFirst = true;
                foreach (var build in Builders)
                {
                    if (!isFirst)
                    {
                        ret.Append(", ");
                    }

                    isFirst = false;
                    ret.Append(build);
                }
                ret.Append(")");
            }

            if (Deserializers.Any())
            {
                var firstType = true;
                ret.Append(" and reads (");
                foreach (var kv in Deserializers)
                {
                    if (!firstType)
                    {
                        ret.Append(", ");
                    }

                    firstType = false;
                    ret.Append($"for type {kv.Key} (");

                    var firstMember = true;
                    foreach (var mem in kv.Value)
                    {
                        if (!firstMember)
                        {
                            ret.Append(", ");
                        }

                        firstMember = false;
                        ret.Append(mem);
                    }
                    ret.Append(")");
                }

                ret.Append(")");
            }

            if (Serializers.Any())
            {
                var firstType = true;
                ret.Append(" and writes (");
                foreach (var kv in Serializers)
                {
                    if (!firstType)
                    {
                        ret.Append(", ");
                    }

                    firstType = false;
                    ret.Append($"for type {kv.Key} (");

                    var firstMember = true;
                    foreach (var mem in kv.Value)
                    {
                        if (!firstMember)
                        {
                            ret.Append(", ");
                        }

                        firstMember = false;
                        ret.Append(mem);
                    }
                    ret.Append(")");
                }

                ret.Append(")");
            }

            return ret.ToString();
        }

        /// <summary>
        /// Delegates to DefaultTypeDescriber.
        /// </summary>
        public Parser GetDynamicCellParserFor(in ReadContext ctx, TypeInfo targetType)
        => Throw.NotSupportedException<Parser>(nameof(ManualTypeDescriber), nameof(GetDynamicCellParserFor));

        /// <summary>
        /// Delegates to DefaultTypeDescriber.
        /// </summary>
        public DynamicRowConverter GetDynamicRowConverter(in ReadContext ctx, IEnumerable<ColumnIdentifier> columns, TypeInfo targetType)
        => Throw.NotSupportedException<DynamicRowConverter>(nameof(ManualTypeDescriber), nameof(GetDynamicRowConverter));

        /// <summary>
        /// Delegates to DefaultTypeDescriber.
        /// </summary>
        public IEnumerable<DynamicCellValue> GetCellsForDynamicRow(in WriteContext ctx, object row)
        => Throw.NotSupportedException<IEnumerable<DynamicCellValue>>(nameof(ManualTypeDescriber), nameof(GetCellsForDynamicRow));
    }
}