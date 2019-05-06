using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Cesil
{
    // todo: test

    /// <summary>
    /// An ITypeDescriber that takes lets you register explicit members to return
    ///   when one of the EnumerateXXX() methods are called.
    /// </summary>
    public sealed class ManualTypeDescriber: ITypeDescriber
    {
        /// <summary>
        /// Whether to throw an exception if a type has no configured members
        ///   for a given EnumerateXXX() method call.
        /// </summary>
        public bool ThrowsOnNoConfiguredType { get; }

        private readonly Dictionary<TypeInfo, List<SerializableMember>> Serializers;
        private readonly Dictionary<TypeInfo, List<DeserializableMember>> Deserializers;

        /// <summary>
        /// Creates a new ManualTypeDescriber.
        /// </summary>
        public ManualTypeDescriber(bool throwOnNoConfiguredType)
        {
            ThrowsOnNoConfiguredType = throwOnNoConfiguredType;
            Serializers = new Dictionary<TypeInfo, List<SerializableMember>>();
            Deserializers = new Dictionary<TypeInfo, List<DeserializableMember>>();
        }

        /// <summary>
        /// Creates a new ManualTypeDescriber.
        /// 
        /// Does not throw if no type is configured for a given enumeration.
        /// </summary>
        public ManualTypeDescriber() : this(false) { }

        // explicit getter

        /// <summary>
        /// Add a getter for the given type, with the given name, using the given getter.
        /// </summary>
        public void AddExplicitGetter(TypeInfo forType, string name, MethodInfo getter)
        => AddSerializableMember(forType, null, getter, name, SerializableMember.GetDefaultFormatter(getter?.ReturnType?.GetTypeInfo()), null, true);

        /// <summary>
        /// Add a getter for the given type, with the given name, using the given getter, and formatter.
        /// </summary>
        public void AddExplicitGetter(TypeInfo forType, string name, MethodInfo getter, MethodInfo formatter)
        => AddSerializableMember(forType, null, getter, name, formatter, null, true);

        /// <summary>
        /// Add a getter for the given type, with the given name, using the given getter, formatter, and ShouldSerialize method.
        /// </summary>
        public void AddExplicitGetter(TypeInfo forType, string name, MethodInfo getter, MethodInfo formatter, MethodInfo shouldSerialize)
        => AddSerializableMember(forType, null, getter, name, formatter, shouldSerialize, true);

        /// <summary>
        /// Add a getter for the given type, with the given name, using the given getter, formatter, ShouldSerialize method, and whether to emit a default value.
        /// </summary>
        public void AddExplicitGetter(TypeInfo forType, string name, MethodInfo getter, MethodInfo formatter, MethodInfo shouldSerialize, bool emitDefaultValue)
        => AddSerializableMember(forType, null, getter, name, formatter, shouldSerialize, emitDefaultValue);

        // serializing fields

        /// <summary>
        /// Add a field to serialize for the given type.
        /// </summary>
        public void AddSerializableField(TypeInfo forType, FieldInfo field)
        => AddSerializableMember(forType, field, null, field.Name, SerializableMember.GetDefaultFormatter(field?.FieldType?.GetTypeInfo()), null, true);

        /// <summary>
        /// Add a field to serialize for the given type, using the given name.
        /// </summary>
        public void AddSerializableField(TypeInfo forType, FieldInfo field, string name)
        => AddSerializableMember(forType, field, null, name, SerializableMember.GetDefaultFormatter(field?.FieldType?.GetTypeInfo()), null, true);

        /// <summary>
        /// Add a field to serialize for the given type, using the given name and formatter.
        /// </summary>
        public void AddSerializableField(TypeInfo forType, FieldInfo field, string name, MethodInfo formatter)
        => AddSerializableMember(forType, field, null, name, formatter, null, true);

        /// <summary>
        /// Add a field to serialize for the given type, using the given name, formatter, and ShouldSerialize method.
        /// </summary>
        public void AddSerializableField(TypeInfo forType, FieldInfo field, string name, MethodInfo formatter, MethodInfo shouldSerialize)
        => AddSerializableMember(forType, field, null, name, formatter, shouldSerialize, true);

        /// <summary>
        /// Add a field to serialize for the type which declares the field.
        /// </summary>
        public void AddSerializableField(FieldInfo field)
        => AddSerializableMember(field?.DeclaringType?.GetTypeInfo(), field, null, field.Name, SerializableMember.GetDefaultFormatter(field?.FieldType?.GetTypeInfo()), null, true);

        /// <summary>
        /// Add a field to serialize with the given name - for the type which declares the field.
        /// </summary>
        public void AddSerializableField(FieldInfo field, string name)
        => AddSerializableMember(field?.DeclaringType?.GetTypeInfo(), field, null, name, SerializableMember.GetDefaultFormatter(field?.FieldType?.GetTypeInfo()), null, true);

        /// <summary>
        /// Add a field to serialize with the given name and formatter - for the type which declares the field.
        /// </summary>
        public void AddSerializableField(FieldInfo field, string name, MethodInfo formatter)
        => AddSerializableMember(field?.DeclaringType?.GetTypeInfo(), field, null, name, formatter, null, true);

        /// <summary>
        /// Add a field to serialize with the given name, formatter, and ShouldSerialize method - for the type which declares the field.
        /// </summary>
        public void AddSerializableField(FieldInfo field, string name, MethodInfo formatter, MethodInfo shouldSerialize)
        => AddSerializableMember(field?.DeclaringType?.GetTypeInfo(), field, null, name, formatter, shouldSerialize, true);

        /// <summary>
        /// Add a field to serialize with the given name, formatter, ShouldSerialize method, and whether to emit a default value - for the type which declares the field.
        /// </summary>
        public void AddSerializableField(FieldInfo field, string name, MethodInfo formatter, MethodInfo shouldSerialize, bool emitDefaultValue)
        => AddSerializableMember(field?.DeclaringType?.GetTypeInfo(), field, null, name, formatter, shouldSerialize, emitDefaultValue);

        // serializing properties

        /// <summary>
        /// Add a property to serialize for the given type.
        /// </summary>
        public void AddSerializableProperty(TypeInfo forType, PropertyInfo prop)
        => AddSerializableMember(forType, null, prop?.GetMethod, prop.Name, SerializableMember.GetDefaultFormatter(prop?.PropertyType?.GetTypeInfo()), null, true);

        /// <summary>
        /// Add a property to serialize for the given type with the given name.
        /// </summary>
        public void AddSerializableProperty(TypeInfo forType, PropertyInfo prop, string name)
        => AddSerializableMember(forType, null, prop?.GetMethod, name, SerializableMember.GetDefaultFormatter(prop?.PropertyType?.GetTypeInfo()), null, true);

        /// <summary>
        /// Add a property to serialize for the given type with the given name and formatter.
        /// </summary>
        public void AddSerializableProperty(TypeInfo forType, PropertyInfo prop, string name, MethodInfo formatter)
        => AddSerializableMember(forType, null, prop?.GetMethod, name, formatter, null, true);

        /// <summary>
        /// Add a property to serialize for the given type with the given name, formatter, and ShouldSerialize method.
        /// </summary>
        public void AddSerializableProperty(TypeInfo forType, PropertyInfo prop, string name, MethodInfo formatter, MethodInfo shouldSerialize)
        => AddSerializableMember(forType, null, prop?.GetMethod, name, formatter, shouldSerialize, true);

        /// <summary>
        /// Add a property to serialize for the given type with the given name, formatter, ShouldSerialize method, and whether to emit a default value.
        /// </summary>
        public void AddSerializableProperty(TypeInfo forType, PropertyInfo prop, string name, MethodInfo formatter, MethodInfo shouldSerialize, bool emitDefaultValue)
        => AddSerializableMember(forType, null, prop?.GetMethod, name, formatter, shouldSerialize, emitDefaultValue);

        /// <summary>
        /// Add a property to serialize for the type which declares the property.
        /// </summary>
        public void AddSerializableProperty(PropertyInfo prop)
        => AddSerializableMember(prop?.DeclaringType?.GetTypeInfo(), null, prop.GetMethod, prop.Name, SerializableMember.GetDefaultFormatter(prop?.PropertyType?.GetTypeInfo()), null, true);

        /// <summary>
        /// Add a property to serialize with the given name - for the type which declares the property.
        /// </summary>
        public void AddSerializableProperty(PropertyInfo prop, string name)
        => AddSerializableMember(prop?.DeclaringType?.GetTypeInfo(), null, prop.GetMethod, name, SerializableMember.GetDefaultFormatter(prop?.PropertyType?.GetTypeInfo()), null, true);

        /// <summary>
        /// Add a property to serialize with the given name and formatter - for the type which declares the property.
        /// </summary>
        public void AddSerializableProperty(PropertyInfo prop, string name, MethodInfo formatter)
        => AddSerializableMember(prop?.DeclaringType?.GetTypeInfo(), null, prop.GetMethod, name, formatter, null, true);

        /// <summary>
        /// Add a property to serialize with the given name, formatter, and ShouldSerialize method - for the type which declares the property.
        /// </summary>
        public void AddSerializableProperty(PropertyInfo prop, string name, MethodInfo formatter, MethodInfo shouldSerialize)
        => AddSerializableMember(prop?.DeclaringType?.GetTypeInfo(), null, prop.GetMethod, name, formatter, shouldSerialize, true);

        /// <summary>
        /// Add a property to serialize with the given name, formatter, ShouldSerialize method, and whether to emit a default value - for the type which declares the property.
        /// </summary>
        public void AddSerializableProperty(PropertyInfo prop, string name, MethodInfo formatter, MethodInfo shouldSerialize, bool emitDefaultValue)
        => AddSerializableMember(prop?.DeclaringType?.GetTypeInfo(), null, prop.GetMethod, name, formatter, shouldSerialize, emitDefaultValue);

        private void AddSerializableMember(TypeInfo forType, FieldInfo field, MethodInfo getter, string name, MethodInfo formatter, MethodInfo shouldSerialize, bool emitDefaultValue)
        {
            if(forType == null)
            {
                Throw.ArgumentNullException(nameof(forType));
            }

            if(field == null && getter == null)
            {
                Throw.InvalidOperation($"One of {nameof(field)} and {nameof(getter)} must be set");
            }

            if(name == null)
            {
                Throw.ArgumentNullException(nameof(name));
            }

            if(formatter == null)
            {
                Throw.ArgumentNullException(nameof(name));
            }

            // shouldSerialize can be null

            if (field != null)
            {
                // the field must be either on the given type,
                //     or on a base class of the given type
                var fieldOnType = field.DeclaringType.GetTypeInfo();
                var isLegal = false;
                var cur = fieldOnType;

                while (cur != null)
                {
                    if (cur == forType)
                    {
                        isLegal = true;
                        break;
                    }

                    cur = cur.BaseType?.GetTypeInfo();
                }

                if (!isLegal)
                {
                    Throw.ArgumentException($"Provided field ({field}) is not on the given type or one of it's base types.", nameof(field));
                }
            }
            else
            {
                if(!getter.IsStatic)
                {
                    var getterOnType = getter.DeclaringType.GetTypeInfo();
                    var isLegal = false;
                    var cur = getterOnType;

                    while(cur != null)
                    {
                        if(cur == forType)
                        {
                            isLegal = true;
                            break;
                        }

                        cur = cur.BaseType?.GetTypeInfo();
                    }

                    if (!isLegal)
                    {
                        Throw.ArgumentException($"Provided getter ({getter}) is not on the given type or one of it's base types.", nameof(getter));
                    }
                }
            }

            var toAdd =
                field != null ?
                    SerializableMember.Create(forType, name, field, formatter, shouldSerialize, emitDefaultValue) :
                    SerializableMember.Create(forType, name, getter, formatter, shouldSerialize, emitDefaultValue);

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
        public void AddExplicitSetter(TypeInfo forType, string name, MethodInfo setter)
        {
            MethodInfo defaultParser;

            var setterPs = setter?.GetParameters();
            if(setterPs != null)
            {
                if(setterPs.Length == 1)
                {
                    defaultParser = DeserializableMember.GetDefaultParser(setterPs[0].ParameterType.GetTypeInfo());
                }else if(setterPs.Length == 2)
                {
                    defaultParser = DeserializableMember.GetDefaultParser(setterPs[1].ParameterType.GetTypeInfo());
                }
                else
                {
                    defaultParser = null;
                }
            }
            else
            {
                defaultParser = null;
            }

            AddDeserializeMember(forType, null, setter, name, defaultParser, false, null);
        }

        /// <summary>
        /// Add a setter for the given type, with the given name, using the given setter and parser.
        /// </summary>
        public void AddExplicitSetter(TypeInfo forType, string name, MethodInfo setter, MethodInfo parser)
        => AddDeserializeMember(forType, null, setter, name, parser, false, null);

        /// <summary>
        /// Add a setter for the given type, with the given name, using the given setter, parser, and whether the column is required.
        /// </summary>
        public void AddExplicitSetter(TypeInfo forType, string name, MethodInfo setter, MethodInfo parser, bool isRequired)
        => AddDeserializeMember(forType, null, setter, name, parser, isRequired, null);

        /// <summary>
        /// Add a setter for the given type, with the given name, using the given setter, parser, whether the column is required, and a reset method.
        /// </summary>
        public void AddExplicitSetter(TypeInfo forType, string name, MethodInfo setter, MethodInfo parser, bool isRequired, MethodInfo reset)
        => AddDeserializeMember(forType, null, setter, name, parser, isRequired, reset);

        // deserialize fields

        /// <summary>
        /// Add a field to deserialize for the given type.
        /// </summary>
        public void AddDeserializableField(TypeInfo forType, FieldInfo field)
        => AddDeserializeMember(forType, field, null, field?.Name, DeserializableMember.GetDefaultParser(field?.DeclaringType?.GetTypeInfo()), false, null);

        /// <summary>
        /// Add a field to deserialize for the given type with the given name.
        /// </summary>
        public void AddDeserializableField(TypeInfo forType, FieldInfo field, string name)
        => AddDeserializeMember(forType, field, null, name, DeserializableMember.GetDefaultParser(field?.DeclaringType?.GetTypeInfo()), false, null);

        /// <summary>
        /// Add a field to deserialize for the given type with the given name and parser.
        /// </summary>
        public void AddDeserializableField(TypeInfo forType, FieldInfo field, string name, MethodInfo parser)
        => AddDeserializeMember(forType, field, null, name, parser, false, null);

        /// <summary>
        /// Add a field to deserialize for the given type with the given name, parser, and whether the column is required.
        /// </summary>
        public void AddDeserializableField(TypeInfo forType, FieldInfo field, string name, MethodInfo parser, bool isRequired)
        => AddDeserializeMember(forType, field, null, name, parser, isRequired, null);

        /// <summary>
        /// Add a field to deserialize for the given type with the given name, parser, whether the column is required, and a reset method.
        /// </summary>
        public void AddDeserializableField(TypeInfo forType, FieldInfo field, string name, MethodInfo parser, bool isRequired, MethodInfo reset)
        => AddDeserializeMember(forType, field, null, name, parser, isRequired, reset);

        /// <summary>
        /// Add a field to deserialize for the type which declares the field.
        /// </summary>
        public void AddDeserializableField(FieldInfo field)
        => AddDeserializeMember(field?.DeclaringType?.GetTypeInfo(), field, null, field?.Name, DeserializableMember.GetDefaultParser(field?.DeclaringType?.GetTypeInfo()), false, null);

        /// <summary>
        /// Add a field to deserialize with the given name - for the type which declares the field.
        /// </summary>
        public void AddDeserializableField(FieldInfo field, string name)
        => AddDeserializeMember(field?.DeclaringType?.GetTypeInfo(), field, null, name, DeserializableMember.GetDefaultParser(field?.DeclaringType?.GetTypeInfo()), false, null);

        /// <summary>
        /// Add a field to deserialize with the given name and parser - for the type which declares the field.
        /// </summary>
        public void AddDeserializableField(FieldInfo field, string name, MethodInfo parser)
        => AddDeserializeMember(field?.DeclaringType?.GetTypeInfo(), field, null, name, parser, false, null);

        /// <summary>
        /// Add a field to deserialize with the given name, parser, and whether the column is required - for the type which declares the field.
        /// </summary>
        public void AddDeserializableField(FieldInfo field, string name, MethodInfo parser, bool isRequired)
        => AddDeserializeMember(field?.DeclaringType?.GetTypeInfo(), field, null, name, parser, isRequired, null);

        /// <summary>
        /// Add a field to deserialize with the given name, parser, whether the column is required, and a reset method - for the type which declares the field.
        /// </summary>
        public void AddDeserializableField(FieldInfo field, string name, MethodInfo parser, bool isRequired, MethodInfo reset)
        => AddDeserializeMember(field?.DeclaringType?.GetTypeInfo(), field, null, name, parser, isRequired, reset);

        // deserialize properties

        /// <summary>
        /// Add a property to deserialize for the given type.
        /// </summary>
        public void AddDeserializableProperty(TypeInfo forType, PropertyInfo prop)
        => AddDeserializeMember(forType, null, prop?.SetMethod, prop?.Name, DeserializableMember.GetDefaultParser(prop?.DeclaringType?.GetTypeInfo()), false, null);

        /// <summary>
        /// Add a property to deserialize for the given type with the given name.
        /// </summary>
        public void AddDeserializableProperty(TypeInfo forType, PropertyInfo prop, string name)
        => AddDeserializeMember(forType, null, prop?.SetMethod, name, DeserializableMember.GetDefaultParser(prop?.DeclaringType?.GetTypeInfo()), false, null);

        /// <summary>
        /// Add a property to deserialize for the given type with the given name and parser.
        /// </summary>
        public void AddDeserializableProperty(TypeInfo forType, PropertyInfo prop, string name, MethodInfo parser)
        => AddDeserializeMember(forType, null, prop?.SetMethod, name, parser, false, null);

        /// <summary>
        /// Add a property to deserialize for the given type with the given name and parser and whether the column is required.
        /// </summary>
        public void AddDeserializableProperty(TypeInfo forType, PropertyInfo prop, string name, MethodInfo parser, bool isRequired)
        => AddDeserializeMember(forType, null, prop?.SetMethod, name, parser, isRequired, null);

        /// <summary>
        /// Add a property to deserialize for the given type with the given name and parser, whether the column is required, and a reset method.
        /// </summary>
        public void AddDeserializableProperty(TypeInfo forType, PropertyInfo prop, string name, MethodInfo parser, bool isRequired, MethodInfo reset)
        => AddDeserializeMember(forType, null, prop?.SetMethod, name, parser, isRequired, reset);

        /// <summary>
        /// Add a property to deserialize for the type which declares the property.
        /// </summary>
        public void AddDeserializableProperty(PropertyInfo prop)
        => AddDeserializeMember(prop?.DeclaringType?.GetTypeInfo(), null, prop?.SetMethod, prop?.Name, DeserializableMember.GetDefaultParser(prop?.DeclaringType?.GetTypeInfo()), false, null);

        /// <summary>
        /// Add a property to deserialize with the given name - for the type which declares the property.
        /// </summary>
        public void AddDeserializableProperty(PropertyInfo prop, string name)
        => AddDeserializeMember(prop?.DeclaringType?.GetTypeInfo(), null, prop?.SetMethod, name, DeserializableMember.GetDefaultParser(prop?.DeclaringType?.GetTypeInfo()), false, null);

        /// <summary>
        /// Add a property to deserialize with the given name and parser - for the type which declares the property.
        /// </summary>
        public void AddDeserializableProperty(PropertyInfo prop, string name, MethodInfo parser)
        => AddDeserializeMember(prop?.DeclaringType?.GetTypeInfo(), null, prop?.SetMethod, name, parser, false, null);

        /// <summary>
        /// Add a property to deserialize with the given name, parser, and whether the column is required - for the type which declares the property.
        /// </summary>
        public void AddDeserializableProperty(PropertyInfo prop, string name, MethodInfo parser, bool isRequired)
        => AddDeserializeMember(prop?.DeclaringType?.GetTypeInfo(), null, prop?.SetMethod, name, parser, isRequired, null);

        /// <summary>
        /// Add a property to deserialize with the given name, parser, whether the column is required, and a reset method - for the type which declares the property.
        /// </summary>
        public void AddDeserializableProperty(PropertyInfo prop, string name, MethodInfo parser, bool isRequired, MethodInfo reset)
        => AddDeserializeMember(prop?.DeclaringType?.GetTypeInfo(), null, prop?.SetMethod, name, parser, isRequired, reset);

        private void AddDeserializeMember(TypeInfo forType, FieldInfo field, MethodInfo setter, string name, MethodInfo parser, bool isRequired, MethodInfo reset)
        {
            if (forType == null)
            {
                Throw.ArgumentNullException(nameof(forType));
            }

            if (field == null && setter == null)
            {
                Throw.InvalidOperation($"One of {nameof(field)} and {nameof(setter)} must be set");
            }

            if (name == null)
            {
                Throw.ArgumentNullException(nameof(name));
            }

            if (parser == null)
            {
                Throw.ArgumentNullException(nameof(parser));
            }

            var toAdd =
                field != null ?
                    DeserializableMember.Create(forType, name, field, parser, isRequired, reset) :
                    DeserializableMember.Create(forType, name, setter, parser, isRequired, reset);

            if (!Deserializers.TryGetValue(forType, out var d))
            {
                Deserializers[forType] = d = new List<DeserializableMember>();
            }

            d.Add(toAdd);
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
            if(!Deserializers.TryGetValue(forType, out var ret))
            {
                if (ThrowsOnNoConfiguredType)
                {
                    Throw.InvalidOperation($"No configured members to deserialize for {forType} ({nameof(ThrowsOnNoConfiguredType)} is set)");
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
                    Throw.InvalidOperation($"No configured members to serialize for {forType} ({nameof(ThrowsOnNoConfiguredType)} is set)");
                }

                return Enumerable.Empty<SerializableMember>();
            }

            return ret;
        }
    }
}
