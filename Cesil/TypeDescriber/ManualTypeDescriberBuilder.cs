using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using System.Text;

namespace Cesil
{
    /// <summary>
    /// Builder for creating a ManualTypeDescriber.
    /// 
    /// Creates ITypeDescribers explicitly registered members to return
    ///   when one of the EnumerateXXX() methods are called.
    /// </summary>
    [NotEquatable("Mutable")]
    public sealed class ManualTypeDescriberBuilder
    {
        /// <summary>
        /// The configured behavior to use when a type has no registered members or providers (depending on the method invoked).
        /// </summary>
        public ManualTypeDescriberFallbackBehavior FallbackBehavior { get; private set; }

        /// <summary>
        /// ITypeDescriber that is used to discover providers or enumerate members if no registration exists
        /// and FallbackBehavior allows falling back.
        /// </summary>
        public ITypeDescriber FallbackTypeDescriber { get; private set; }

        private readonly ImmutableDictionary<TypeInfo, InstanceProvider>.Builder Builders;

        private readonly Dictionary<TypeInfo, ImmutableArray<SerializableMember>.Builder> Serializers;
        private readonly Dictionary<TypeInfo, ImmutableArray<DeserializableMember>.Builder> Deserializers;

        private ManualTypeDescriberBuilder(ManualTypeDescriberFallbackBehavior behavior, ITypeDescriber fallback)
        {
            FallbackBehavior = behavior;
            FallbackTypeDescriber = fallback;

            Builders = ImmutableDictionary.CreateBuilder<TypeInfo, InstanceProvider>();
            Serializers = new Dictionary<TypeInfo, ImmutableArray<SerializableMember>.Builder>();
            Deserializers = new Dictionary<TypeInfo, ImmutableArray<DeserializableMember>.Builder>();
        }

        private ManualTypeDescriberBuilder(ManualTypeDescriber other)
        {
            FallbackBehavior = other.ThrowsOnNoConfiguredType ? ManualTypeDescriberFallbackBehavior.Throw : ManualTypeDescriberFallbackBehavior.UseFallback;
            FallbackTypeDescriber = other.Fallback;

            Builders = ImmutableDictionary.CreateBuilder<TypeInfo, InstanceProvider>();
            foreach (var b in other.Builders)
            {
                Builders[b.Key] = b.Value;
            }

            Serializers = new Dictionary<TypeInfo, ImmutableArray<SerializableMember>.Builder>();
            foreach (var s in other.Serializers)
            {
                var arr = ImmutableArray.CreateBuilder<SerializableMember>();
                arr.AddRange(s.Value);

                Serializers[s.Key] = arr;
            }

            Deserializers = new Dictionary<TypeInfo, ImmutableArray<DeserializableMember>.Builder>();
            foreach (var s in other.Deserializers)
            {
                var arr = ImmutableArray.CreateBuilder<DeserializableMember>();
                arr.AddRange(s.Value);

                Deserializers[s.Key] = arr;
            }
        }

        // create builders

        /// <summary>
        /// Create a new ManualTypeDescriberBuilder which fallbacks to TypeDescribers.Default when a type with
        ///    no registered members is requested.
        /// </summary>
        public static ManualTypeDescriberBuilder CreateBuilder()
        => CreateBuilder(ManualTypeDescriberFallbackBehavior.UseFallback);

        /// <summary>
        /// Create a new empty ManualTypeDescriberBuilder with the given fallback behavior, and a fallback ITypeDescriber of TypeDescribers.Default.
        /// </summary>
        public static ManualTypeDescriberBuilder CreateBuilder(ManualTypeDescriberFallbackBehavior fallbackBehavior)
        {
            if (!Enum.IsDefined(Types.ManualTypeDescriberFallbackBehaviorsType, fallbackBehavior))
            {
                return Throw.ArgumentException<ManualTypeDescriberBuilder>($"Unexpected value: {fallbackBehavior}", nameof(fallbackBehavior));
            }

            return CreateBuilder(fallbackBehavior, TypeDescribers.Default);
        }

        /// <summary>
        /// Create a new empty ManualTypeDescriberBuilder with the given fallback behavior.
        /// </summary>
        public static ManualTypeDescriberBuilder CreateBuilder(ManualTypeDescriberFallbackBehavior fallbackBehavior, ITypeDescriber fallbackTypeDescriber)
        {
            if (!Enum.IsDefined(Types.ManualTypeDescriberFallbackBehaviorsType, fallbackBehavior))
            {
                return Throw.ArgumentException<ManualTypeDescriberBuilder>($"Unexpected value: {fallbackBehavior}", nameof(fallbackBehavior));
            }

            Utils.CheckArgumentNull(fallbackTypeDescriber, nameof(fallbackTypeDescriber));

            return new ManualTypeDescriberBuilder(fallbackBehavior, fallbackTypeDescriber);
        }

        /// <summary>
        /// Create a new ManualTypeDescriberBuilder that copies it's
        ///   initial values from the given ManualTypeDescriber.
        /// </summary>
        public static ManualTypeDescriberBuilder CreateBuilder(ManualTypeDescriber typeDescriber)
        {
            Utils.CheckArgumentNull(typeDescriber, nameof(typeDescriber));

            return new ManualTypeDescriberBuilder(typeDescriber);
        }

        // create type describe

        /// <summary>
        /// Create a ManualTypeDescriber with the options configured with this builder.
        /// </summary>
        public ManualTypeDescriber ToManualTypeDescriber()
        {
            var builders = Builders.ToImmutable();
            var serializers = Serializers.ToImmutableDictionary(kv => kv.Key, kv => kv.Value.ToImmutable());
            var deserializers = Deserializers.ToImmutableDictionary(kv => kv.Key, kv => kv.Value.ToImmutable());

            bool throws;
            switch (FallbackBehavior)
            {
                case ManualTypeDescriberFallbackBehavior.Throw: throws = true; break;
                case ManualTypeDescriberFallbackBehavior.UseFallback: throws = false; break;
                default: return Throw.InvalidOperationException<ManualTypeDescriber>($"Unexpected {nameof(ManualTypeDescriberFallbackBehavior)}: {FallbackBehavior}");
            }

            return new ManualTypeDescriber(throws, FallbackTypeDescriber, builders, serializers, deserializers);
        }

        // set fallback behavior

        /// <summary>
        /// Sets the behavior to fallback to when a method has no registrations to return.
        /// </summary>
        public ManualTypeDescriberBuilder SetFallbackBehavior(ManualTypeDescriberFallbackBehavior fallbackBehavior)
        {
            if (!Enum.IsDefined(Types.ManualTypeDescriberFallbackBehaviorsType, fallbackBehavior))
            {
                return Throw.ArgumentException<ManualTypeDescriberBuilder>($"Unexpected value: {fallbackBehavior}", nameof(fallbackBehavior));
            }

            FallbackBehavior = fallbackBehavior;

            return this;
        }

        // fallback itypedescriber

        /// <summary>
        /// Sets the ITypeDescriber to fallback to, provided that FallbackBehavior allows falling back, 
        ///   when a method has no regisrations to return.
        /// </summary>
        public ManualTypeDescriberBuilder SetFallback(ITypeDescriber typeDescriber)
        {
            Utils.CheckArgumentNull(typeDescriber, nameof(typeDescriber));

            FallbackTypeDescriber = typeDescriber;

            return this;
        }

        // instance providers

        /// <summary>
        /// Set the delegate to use when constructing new instances of 
        ///   type T.
        /// </summary>
        public ManualTypeDescriberBuilder SetInstanceProvider(InstanceProvider instanceProvider)
        {
            Utils.CheckArgumentNull(instanceProvider, nameof(instanceProvider));

            SetInstanceProvider(instanceProvider.ConstructsType, instanceProvider);

            return this;
        }

        /// <summary>
        /// Set the delegate to use when constructing new instances of 
        ///   the given type.
        /// </summary>
        public ManualTypeDescriberBuilder SetInstanceProvider(TypeInfo forType, InstanceProvider instanceProvider)
        {
            Utils.CheckArgumentNull(forType, nameof(forType));
            Utils.CheckArgumentNull(instanceProvider, nameof(instanceProvider));

            var createdType = instanceProvider.ConstructsType;
            if (!forType.IsAssignableFrom(createdType))
            {
                return Throw.InvalidOperationException<ManualTypeDescriberBuilder>($"{forType} cannot be assigned from {createdType}, constructed by {instanceProvider}");
            }

            if (Builders.ContainsKey(forType))
            {
                return Throw.InvalidOperationException<ManualTypeDescriberBuilder>($"Instance provider already registered for {forType}");
            }

            Builders[forType] = instanceProvider;

            return this;
        }

        // explicit getter

        /// <summary>
        /// Add a getter for the given type, with the given name, using the given getter.
        /// </summary>
        public ManualTypeDescriberBuilder AddExplicitGetter(TypeInfo forType, string name, Getter getter)
        {
            var formatter = getter != null ? Formatter.GetDefault(getter.Returns) : null;
            return AddSerializableMember(forType, getter, name, formatter, null, EmitDefaultValue.Yes);
        }

        /// <summary>
        /// Add a getter for the given type, with the given name, using the given getter, and formatter.
        /// </summary>
        public ManualTypeDescriberBuilder AddExplicitGetter(TypeInfo forType, string name, Getter getter, Formatter formatter)
        => AddSerializableMember(forType, getter, name, formatter, null, EmitDefaultValue.Yes);

        /// <summary>
        /// Add a getter for the given type, with the given name, using the given getter, formatter, and ShouldSerialize method.
        /// </summary>
        public ManualTypeDescriberBuilder AddExplicitGetter(TypeInfo forType, string name, Getter getter, Formatter formatter, ShouldSerialize shouldSerialize)
        {
            Utils.CheckArgumentNull(shouldSerialize, nameof(shouldSerialize));

            return AddSerializableMember(forType, getter, name, formatter, shouldSerialize, EmitDefaultValue.Yes);
        }

        /// <summary>
        /// Add a getter for the given type, with the given name, using the given getter, formatter, ShouldSerialize method, and whether to emit a default value.
        /// </summary>
        public ManualTypeDescriberBuilder AddExplicitGetter(TypeInfo forType, string name, Getter getter, Formatter formatter, ShouldSerialize shouldSerialize, EmitDefaultValue emitDefault)
        {
            Utils.CheckArgumentNull(shouldSerialize, nameof(shouldSerialize));

            return AddSerializableMember(forType, getter, name, formatter, shouldSerialize, emitDefault);
        }

        // serializing fields

        /// <summary>
        /// Add a field to serialize for the given type.
        /// </summary>
        public ManualTypeDescriberBuilder AddSerializableField(TypeInfo forType, FieldInfo field)
        {
            var formatter = field != null ? Formatter.GetDefault(field.FieldType.GetTypeInfo()) : null;
            return AddSerializableMember(forType, (Getter?)field, field?.Name, formatter, null, EmitDefaultValue.Yes);
        }

        /// <summary>
        /// Add a field to serialize for the given type, using the given name.
        /// </summary>
        public ManualTypeDescriberBuilder AddSerializableField(TypeInfo forType, FieldInfo field, string name)
        {
            var formatter = field != null ? Formatter.GetDefault(field.FieldType.GetTypeInfo()) : null;
            return AddSerializableMember(forType, (Getter?)field, name, formatter, null, EmitDefaultValue.Yes);
        }

        /// <summary>
        /// Add a field to serialize for the given type, using the given name and formatter.
        /// </summary>
        public ManualTypeDescriberBuilder AddSerializableField(TypeInfo forType, FieldInfo field, string name, Formatter formatter)
        => AddSerializableMember(forType, (Getter?)field, name, formatter, null, EmitDefaultValue.Yes);

        /// <summary>
        /// Add a field to serialize for the given type, using the given name, formatter, and ShouldSerialize method.
        /// </summary>
        public ManualTypeDescriberBuilder AddSerializableField(TypeInfo forType, FieldInfo field, string name, Formatter formatter, ShouldSerialize shouldSerialize)
        {
            Utils.CheckArgumentNull(shouldSerialize, nameof(shouldSerialize));

            return AddSerializableMember(forType, (Getter?)field, name, formatter, shouldSerialize, EmitDefaultValue.Yes);
        }

        /// <summary>
        /// Add a field to serialize for the given type, using the given name, formatter, and ShouldSerialize method.
        /// </summary>
        public ManualTypeDescriberBuilder AddSerializableField(TypeInfo forType, FieldInfo field, string name, Formatter formatter, ShouldSerialize shouldSerialize, EmitDefaultValue emitDefault)
        {
            Utils.CheckArgumentNull(shouldSerialize, nameof(shouldSerialize));

            return AddSerializableMember(forType, (Getter?)field, name, formatter, shouldSerialize, emitDefault);
        }

        /// <summary>
        /// Add a field to serialize for the type which declares the field.
        /// </summary>
        public ManualTypeDescriberBuilder AddSerializableField(FieldInfo field)
        {
            var formatter = field != null ? Formatter.GetDefault(field.FieldType.GetTypeInfo()) : null;

            return AddSerializableMember(field?.DeclaringType?.GetTypeInfo(), (Getter?)field, field?.Name, formatter, null, EmitDefaultValue.Yes);
        }

        /// <summary>
        /// Add a field to serialize with the given name - for the type which declares the field.
        /// </summary>
        public ManualTypeDescriberBuilder AddSerializableField(FieldInfo field, string name)
        {
            var formatter = field != null ? Formatter.GetDefault(field.FieldType.GetTypeInfo()) : null;

            return AddSerializableMember(field?.DeclaringType?.GetTypeInfo(), (Getter?)field, name, formatter, null, EmitDefaultValue.Yes);
        }

        /// <summary>
        /// Add a field to serialize with the given name and formatter - for the type which declares the field.
        /// </summary>
        public ManualTypeDescriberBuilder AddSerializableField(FieldInfo field, string name, Formatter formatter)
        => AddSerializableMember(field?.DeclaringType?.GetTypeInfo(), (Getter?)field, name, formatter, null, EmitDefaultValue.Yes);

        /// <summary>
        /// Add a field to serialize with the given name, formatter, and ShouldSerialize method - for the type which declares the field.
        /// </summary>
        public ManualTypeDescriberBuilder AddSerializableField(FieldInfo field, string name, Formatter formatter, ShouldSerialize shouldSerialize)
        {
            Utils.CheckArgumentNull(shouldSerialize, nameof(shouldSerialize));

            return AddSerializableMember(field?.DeclaringType?.GetTypeInfo(), (Getter?)field, name, formatter, shouldSerialize, EmitDefaultValue.Yes);
        }

        /// <summary>
        /// Add a field to serialize with the given name, formatter, ShouldSerialize method, and whether to emit a default value - for the type which declares the field.
        /// </summary>
        public ManualTypeDescriberBuilder AddSerializableField(FieldInfo field, string name, Formatter formatter, ShouldSerialize shouldSerialize, EmitDefaultValue emitDefault)
        {
            Utils.CheckArgumentNull(shouldSerialize, nameof(shouldSerialize));

            return AddSerializableMember(field?.DeclaringType?.GetTypeInfo(), (Getter?)field, name, formatter, shouldSerialize, emitDefault);
        }

        // serializing properties

        /// <summary>
        /// Add a property to serialize for the given type.
        /// </summary>
        public ManualTypeDescriberBuilder AddSerializableProperty(TypeInfo forType, PropertyInfo property)
        {
            var formatter = property != null ? Formatter.GetDefault(property.PropertyType.GetTypeInfo()) : null;

            return AddSerializableMember(forType, (Getter?)property?.GetMethod, property?.Name, formatter, null, EmitDefaultValue.Yes);
        }

        /// <summary>
        /// Add a property to serialize for the given type with the given name.
        /// </summary>
        public ManualTypeDescriberBuilder AddSerializableProperty(TypeInfo forType, PropertyInfo property, string name)
        {
            var formatter = property != null ? Formatter.GetDefault(property.PropertyType.GetTypeInfo()) : null;

            return AddSerializableMember(forType, (Getter?)property?.GetMethod, name, formatter, null, EmitDefaultValue.Yes);
        }

        /// <summary>
        /// Add a property to serialize for the given type with the given name and formatter.
        /// </summary>
        public ManualTypeDescriberBuilder AddSerializableProperty(TypeInfo forType, PropertyInfo property, string name, Formatter formatter)
        => AddSerializableMember(forType, (Getter?)property?.GetMethod, name, formatter, null, EmitDefaultValue.Yes);

        /// <summary>
        /// Add a property to serialize for the given type with the given name, formatter, and ShouldSerialize method.
        /// </summary>
        public ManualTypeDescriberBuilder AddSerializableProperty(TypeInfo forType, PropertyInfo property, string name, Formatter formatter, ShouldSerialize shouldSerialize)
        {
            Utils.CheckArgumentNull(shouldSerialize, nameof(shouldSerialize));

            return AddSerializableMember(forType, (Getter?)property?.GetMethod, name, formatter, shouldSerialize, EmitDefaultValue.Yes);
        }

        /// <summary>
        /// Add a property to serialize for the given type with the given name, formatter, ShouldSerialize method, and whether to emit a default value.
        /// </summary>
        public ManualTypeDescriberBuilder AddSerializableProperty(TypeInfo forType, PropertyInfo property, string name, Formatter formatter, ShouldSerialize shouldSerialize, EmitDefaultValue emitDefault)
        {
            Utils.CheckArgumentNull(shouldSerialize, nameof(shouldSerialize));

            return AddSerializableMember(forType, (Getter?)property?.GetMethod, name, formatter, shouldSerialize, emitDefault);
        }

        /// <summary>
        /// Add a property to serialize for the type which declares the property.
        /// </summary>
        public ManualTypeDescriberBuilder AddSerializableProperty(PropertyInfo property)
        {
            var formatter = property != null ? Formatter.GetDefault(property.PropertyType.GetTypeInfo()) : null;

            return AddSerializableMember(property?.DeclaringType?.GetTypeInfo(), (Getter?)property?.GetMethod, property?.Name, formatter, null, EmitDefaultValue.Yes);
        }

        /// <summary>
        /// Add a property to serialize with the given name - for the type which declares the property.
        /// </summary>
        public ManualTypeDescriberBuilder AddSerializableProperty(PropertyInfo property, string name)
        {
            var formatter = property != null ? Formatter.GetDefault(property.PropertyType.GetTypeInfo()) : null;

            return AddSerializableMember(property?.DeclaringType?.GetTypeInfo(), (Getter?)property?.GetMethod, name, formatter, null, EmitDefaultValue.Yes);
        }

        /// <summary>
        /// Add a property to serialize with the given name and formatter - for the type which declares the property.
        /// </summary>
        public ManualTypeDescriberBuilder AddSerializableProperty(PropertyInfo property, string name, Formatter formatter)
        => AddSerializableMember(property?.DeclaringType?.GetTypeInfo(), (Getter?)property?.GetMethod, name, formatter, null, EmitDefaultValue.Yes);

        /// <summary>
        /// Add a property to serialize with the given name, formatter, and ShouldSerialize method - for the type which declares the property.
        /// </summary>
        public ManualTypeDescriberBuilder AddSerializableProperty(PropertyInfo property, string name, Formatter formatter, ShouldSerialize shouldSerialize)
        {
            Utils.CheckArgumentNull(shouldSerialize, nameof(shouldSerialize));

            return AddSerializableMember(property?.DeclaringType?.GetTypeInfo(), (Getter?)property?.GetMethod, name, formatter, shouldSerialize, EmitDefaultValue.Yes);
        }

        /// <summary>
        /// Add a property to serialize with the given name, formatter, ShouldSerialize method, and whether to emit a default value - for the type which declares the property.
        /// </summary>
        public ManualTypeDescriberBuilder AddSerializableProperty(PropertyInfo property, string name, Formatter formatter, ShouldSerialize shouldSerialize, EmitDefaultValue emitDefault)
        {
            Utils.CheckArgumentNull(shouldSerialize, nameof(shouldSerialize));

            return AddSerializableMember(property?.DeclaringType?.GetTypeInfo(), (Getter?)property?.GetMethod, name, formatter, shouldSerialize, emitDefault);
        }

        // internal for testing purposes
        internal ManualTypeDescriberBuilder AddSerializableMember(TypeInfo? forType, Getter? getter, string? name, Formatter? formatter, ShouldSerialize? shouldSerialize, EmitDefaultValue emitDefault)
        {
            if (forType == null)
            {
                return Throw.ArgumentNullException<ManualTypeDescriberBuilder>(nameof(forType));
            }

            if (getter == null)
            {
                return Throw.ArgumentNullException<ManualTypeDescriberBuilder>(nameof(getter));
            }

            if (name == null)
            {
                return Throw.ArgumentNullException<ManualTypeDescriberBuilder>(nameof(name));
            }

            if (formatter == null)
            {
                return Throw.ArgumentNullException<ManualTypeDescriberBuilder>(nameof(name));
            }

            // shouldSerialize can be null

            if (getter.RowType.HasValue)
            {
                var getterOnType = getter.RowType.Value;
                var isLegal = false;
                TypeInfo? cur = forType;

                while (cur != null)
                {
                    if (cur == getterOnType)
                    {
                        isLegal = true;
                        break;
                    }

                    cur = cur?.BaseType?.GetTypeInfo();
                }

                if (!isLegal)
                {
                    return Throw.InvalidOperationException<ManualTypeDescriberBuilder>($"Provided getter ({getter}) is not on {forType} or one of it's base types.");
                }
            }

            var toAdd = SerializableMember.Create(forType, name, getter, formatter, shouldSerialize, emitDefault);

            if (!Serializers.TryGetValue(forType, out var s))
            {
                Serializers[forType] = s = ImmutableArray.CreateBuilder<SerializableMember>();
            }

            s.Add(toAdd);

            return this;
        }

        // explicit setter

        /// <summary>
        /// Add a setter for the given type, with the given name, using the given setter.
        /// </summary>
        public ManualTypeDescriberBuilder AddExplicitSetter(TypeInfo forType, string name, Setter setter)
        {
            // tricky null stuff here to defer validation until the AddXXX call
            var defaultParser = setter != null ? Parser.GetDefault(setter.Takes) : null;

            return AddDeserializeMember(forType, setter, name, defaultParser, MemberRequired.No, null);
        }

        /// <summary>
        /// Add a setter for the given type, with the given name, using the given setter and parser.
        /// </summary>
        public ManualTypeDescriberBuilder AddExplicitSetter(TypeInfo forType, string name, Setter setter, Parser parser)
        => AddDeserializeMember(forType, setter, name, parser, MemberRequired.No, null);

        /// <summary>
        /// Add a setter for the given type, with the given name, using the given setter, parser, and whether the column is required.
        /// </summary>
        public ManualTypeDescriberBuilder AddExplicitSetter(TypeInfo forType, string name, Setter setter, Parser parser, MemberRequired required)
        => AddDeserializeMember(forType, setter, name, parser, required, null);

        /// <summary>
        /// Add a setter for the given type, with the given name, using the given setter, parser, whether the column is required, and a reset method.
        /// </summary>
        public ManualTypeDescriberBuilder AddExplicitSetter(TypeInfo forType, string name, Setter setter, Parser parser, MemberRequired required, Reset reset)
        {
            Utils.CheckArgumentNull(reset, nameof(reset));

            return AddDeserializeMember(forType, setter, name, parser, required, reset);
        }

        // deserialize fields

        /// <summary>
        /// Add a field to deserialize for the given type.
        /// </summary>
        public ManualTypeDescriberBuilder AddDeserializableField(TypeInfo forType, FieldInfo field)
        {
            var parser = field != null ? Parser.GetDefault(field.FieldType.GetTypeInfo()) : null;

            return AddDeserializeMember(forType, (Setter?)field, field?.Name, parser, MemberRequired.No, null);
        }

        /// <summary>
        /// Add a field to deserialize for the given type with the given name.
        /// </summary>
        public ManualTypeDescriberBuilder AddDeserializableField(TypeInfo forType, FieldInfo field, string name)
        {
            var parser = field != null ? Parser.GetDefault(field.FieldType.GetTypeInfo()) : null;

            return AddDeserializeMember(forType, (Setter?)field, name, parser, MemberRequired.No, null);
        }

        /// <summary>
        /// Add a field to deserialize for the given type with the given name and parser.
        /// </summary>
        public ManualTypeDescriberBuilder AddDeserializableField(TypeInfo forType, FieldInfo field, string name, Parser parser)
        => AddDeserializeMember(forType, (Setter?)field, name, parser, MemberRequired.No, null);

        /// <summary>
        /// Add a field to deserialize for the given type with the given name, parser, and whether the column is required.
        /// </summary>
        public ManualTypeDescriberBuilder AddDeserializableField(TypeInfo forType, FieldInfo field, string name, Parser parser, MemberRequired required)
        => AddDeserializeMember(forType, (Setter?)field, name, parser, required, null);

        /// <summary>
        /// Add a field to deserialize for the given type with the given name, parser, whether the column is required, and a reset method.
        /// </summary>
        public ManualTypeDescriberBuilder AddDeserializableField(TypeInfo forType, FieldInfo field, string name, Parser parser, MemberRequired required, Reset reset)
        {
            Utils.CheckArgumentNull(reset, nameof(reset));

            return AddDeserializeMember(forType, (Setter?)field, name, parser, required, reset);
        }

        /// <summary>
        /// Add a field to deserialize for the type which declares the field.
        /// </summary>
        public ManualTypeDescriberBuilder AddDeserializableField(FieldInfo field)
        {
            var parser = field != null ? Parser.GetDefault(field.FieldType.GetTypeInfo()) : null;

            return AddDeserializeMember(field?.DeclaringType?.GetTypeInfo(), (Setter?)field, field?.Name, parser, MemberRequired.No, null);
        }

        /// <summary>
        /// Add a field to deserialize with the given name - for the type which declares the field.
        /// </summary>
        public ManualTypeDescriberBuilder AddDeserializableField(FieldInfo field, string name)
        {
            var parser = field != null ? Parser.GetDefault(field.FieldType.GetTypeInfo()) : null;

            return AddDeserializeMember(field?.DeclaringType?.GetTypeInfo(), (Setter?)field, name, parser, MemberRequired.No, null);
        }

        /// <summary>
        /// Add a field to deserialize with the given name and parser - for the type which declares the field.
        /// </summary>
        public ManualTypeDescriberBuilder AddDeserializableField(FieldInfo field, string name, Parser parser)
        => AddDeserializeMember(field?.DeclaringType?.GetTypeInfo(), (Setter?)field, name, parser, MemberRequired.No, null);

        /// <summary>
        /// Add a field to deserialize with the given name, parser, and whether the column is required - for the type which declares the field.
        /// </summary>
        public ManualTypeDescriberBuilder AddDeserializableField(FieldInfo field, string name, Parser parser, MemberRequired required)
        => AddDeserializeMember(field?.DeclaringType?.GetTypeInfo(), (Setter?)field, name, parser, required, null);

        /// <summary>
        /// Add a field to deserialize with the given name, parser, whether the column is required, and a reset method - for the type which declares the field.
        /// </summary>
        public ManualTypeDescriberBuilder AddDeserializableField(FieldInfo field, string name, Parser parser, MemberRequired required, Reset reset)
        {
            Utils.CheckArgumentNull(reset, nameof(reset));

            return AddDeserializeMember(field?.DeclaringType?.GetTypeInfo(), (Setter?)field, name, parser, required, reset);
        }

        // deserialize properties

        /// <summary>
        /// Add a property to deserialize for the given type.
        /// </summary>
        public ManualTypeDescriberBuilder AddDeserializableProperty(TypeInfo forType, PropertyInfo property)
        {
            var parser = property != null ? Parser.GetDefault(property.PropertyType.GetTypeInfo()) : null;

            return AddDeserializeMember(forType, (Setter?)property?.SetMethod, property?.Name, parser, MemberRequired.No, null);
        }

        /// <summary>
        /// Add a property to deserialize for the given type with the given name.
        /// </summary>
        public ManualTypeDescriberBuilder AddDeserializableProperty(TypeInfo forType, PropertyInfo property, string name)
        {
            var parser = property != null ? Parser.GetDefault(property.PropertyType.GetTypeInfo()) : null;

            return AddDeserializeMember(forType, (Setter?)property?.SetMethod, name, parser, MemberRequired.No, null);
        }

        /// <summary>
        /// Add a property to deserialize for the given type with the given name and parser.
        /// </summary>
        public ManualTypeDescriberBuilder AddDeserializableProperty(TypeInfo forType, PropertyInfo property, string name, Parser parser)
        => AddDeserializeMember(forType, (Setter?)property?.SetMethod, name, parser, MemberRequired.No, null);

        /// <summary>
        /// Add a property to deserialize for the given type with the given name and parser and whether the column is required.
        /// </summary>
        public ManualTypeDescriberBuilder AddDeserializableProperty(TypeInfo forType, PropertyInfo property, string name, Parser parser, MemberRequired required)
        => AddDeserializeMember(forType, (Setter?)property?.SetMethod, name, parser, required, null);

        /// <summary>
        /// Add a property to deserialize for the given type with the given name and parser, whether the column is required, and a reset method.
        /// </summary>
        public ManualTypeDescriberBuilder AddDeserializableProperty(TypeInfo forType, PropertyInfo property, string name, Parser parser, MemberRequired required, Reset reset)
        {
            Utils.CheckArgumentNull(reset, nameof(reset));

            return AddDeserializeMember(forType, (Setter?)property?.SetMethod, name, parser, required, reset);
        }

        /// <summary>
        /// Add a property to deserialize for the type which declares the property.
        /// </summary>
        public ManualTypeDescriberBuilder AddDeserializableProperty(PropertyInfo property)
        {
            var parser = property != null ? Parser.GetDefault(property.PropertyType.GetTypeInfo()) : null;

            return AddDeserializeMember(property?.DeclaringType?.GetTypeInfo(), (Setter?)property?.SetMethod, property?.Name, parser, MemberRequired.No, null);
        }

        /// <summary>
        /// Add a property to deserialize with the given name - for the type which declares the property.
        /// </summary>
        public ManualTypeDescriberBuilder AddDeserializableProperty(PropertyInfo property, string name)
        {
            var parser = property != null ? Parser.GetDefault(property.PropertyType.GetTypeInfo()) : null;

            return AddDeserializeMember(property?.DeclaringType?.GetTypeInfo(), (Setter?)property?.SetMethod, name, parser, MemberRequired.No, null);
        }

        /// <summary>
        /// Add a property to deserialize with the given name and parser - for the type which declares the property.
        /// </summary>
        public ManualTypeDescriberBuilder AddDeserializableProperty(PropertyInfo property, string name, Parser parser)
        => AddDeserializeMember(property?.DeclaringType?.GetTypeInfo(), (Setter?)property?.SetMethod, name, parser, MemberRequired.No, null);

        /// <summary>
        /// Add a property to deserialize with the given name, parser, and whether the column is required - for the type which declares the property.
        /// </summary>
        public ManualTypeDescriberBuilder AddDeserializableProperty(PropertyInfo property, string name, Parser parser, MemberRequired required)
        => AddDeserializeMember(property?.DeclaringType?.GetTypeInfo(), (Setter?)property?.SetMethod, name, parser, required, null);

        /// <summary>
        /// Add a property to deserialize with the given name, parser, whether the column is required, and a reset method - for the type which declares the property.
        /// </summary>
        public ManualTypeDescriberBuilder AddDeserializableProperty(PropertyInfo property, string name, Parser parser, MemberRequired required, Reset reset)
        {
            Utils.CheckArgumentNull(reset, nameof(reset));

            return AddDeserializeMember(property?.DeclaringType?.GetTypeInfo(), (Setter?)property?.SetMethod, name, parser, required, reset);
        }

        private ManualTypeDescriberBuilder AddDeserializeMember(TypeInfo? forType, Setter? setter, string? name, Parser? parser, MemberRequired required, Reset? reset)
        {
            if (forType == null)
            {
                return Throw.ArgumentNullException<ManualTypeDescriberBuilder>(nameof(forType));
            }

            if (setter == null)
            {
                return Throw.ArgumentNullException<ManualTypeDescriberBuilder>(nameof(setter));
            }

            if (name == null)
            {
                return Throw.ArgumentNullException<ManualTypeDescriberBuilder>(nameof(name));
            }

            if (parser == null)
            {
                return Throw.ArgumentNullException<ManualTypeDescriberBuilder>(nameof(parser));
            }

            var toAdd = DeserializableMember.Create(forType, name, setter, parser, required, reset);

            if (!Deserializers.TryGetValue(forType, out var d))
            {
                Deserializers[forType] = d = ImmutableArray.CreateBuilder<DeserializableMember>();
            }

            d.Add(toAdd);

            return this;
        }

        /// <summary>
        /// Returns a representation of this ManualTypeDescriber object.
        /// 
        /// Only for debugging, this value is not guaranteed to be stable.
        /// </summary>
        public override string ToString()
        {
            var ret = new StringBuilder();

            ret.Append($"{nameof(ManualTypeDescriberBuilder)}");

            ret.Append($" with {nameof(FallbackBehavior)} {FallbackBehavior}");
            ret.Append($" and {nameof(FallbackTypeDescriber)} ({FallbackTypeDescriber})");

            if (Builders.Count > 0)
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

            if (Deserializers.Count > 0)
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

            if (Serializers.Count > 0)
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
    }
}
