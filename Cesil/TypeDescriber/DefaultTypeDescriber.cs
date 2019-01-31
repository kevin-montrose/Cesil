using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;

namespace Cesil
{
    /// <summary>
    /// The default implementation of ITypeDescriber used to
    ///   determine how to (de)serialize a type.
    ///   
    /// It will serialize all public properties, any fields
    ///   with a [DataMember], and will respect ShouldSerialize()
    ///   methods.
    ///   
    /// This type is unsealed to allow for easy extension of it's behavior.
    /// </summary>
    public class DefaultTypeDescriber : ITypeDescriber
    {
        // todo: Reset()?

        /// <summary>
        /// Construct a new DefaultTypeDesciber.
        /// 
        /// A pre-allocated instance is available on TypeDescribers.Default.
        /// </summary>
        public DefaultTypeDescriber() { }

        /// <summary>
        /// Enumerate all columns to deserialize.
        /// </summary>
        public virtual IEnumerable<DeserializableMember> EnumerateMembersToDeserialize(TypeInfo forType)
        {
            var buffer = new List<(DeserializableMember Member, int? Position)>();

            foreach (var p in forType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
            {
                if (!ShouldDeserialize(forType, p)) continue;

                var name = GetDeserializationName(forType, p);
                var setter = GetSetter(forType, p);
                var parser = GetParser(forType, p);
                var order = GetPosition(forType, p);
                var isRequired = GetIsRequired(forType, p);

                buffer.Add((DeserializableMember.Create(name, setter, parser, isRequired), order));
            }

            foreach (var f in forType.GetFields())
            {
                if (!ShouldDeserialize(forType, f)) continue;

                var name = GetDeserializationName(forType, f);
                var parser = GetParser(forType, f);
                var order = GetPosition(forType, f);
                var isRequired = GetIsRequired(forType, f);

                buffer.Add((DeserializableMember.Create(name, f, parser, isRequired), order));
            }

            buffer.Sort(TypeDescribers.DeserializableComparer);

            foreach (var (member, _) in buffer)
            {
                yield return member;
            }
        }

        // property deserialization defaults

        /// <summary>
        /// Returns true if the given property should be deserialized.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        protected virtual bool ShouldDeserialize(TypeInfo forType, PropertyInfo property)
        {
            if (property.SetMethod == null) return false;

            var ignoreDataMember = property.GetCustomAttribute<IgnoreDataMemberAttribute>();
            if (ignoreDataMember != null)
            {
                return false;
            }

            var dataMember = property.GetCustomAttribute<DataMemberAttribute>();
            if (dataMember != null)
            {
                return true;
            }

            return
                property.SetMethod != null &&
                property.SetMethod.IsPublic &&
                !property.SetMethod.IsStatic &&
                property.SetMethod.GetParameters().Length == 1 &&
                DeserializableMember.GetDefaultParser(property.SetMethod.GetParameters()[0].ParameterType.GetTypeInfo()) != null;
        }

        /// <summary>
        /// Returns the name of the column that should map to the given property when deserialized.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        protected virtual string GetDeserializationName(TypeInfo forType, PropertyInfo property)
        => GetDeserializationName(property);

        /// <summary>
        /// Returns the setter to use for the given property when deserialized.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        protected virtual MethodInfo GetSetter(TypeInfo forType, PropertyInfo property)
        => property.SetMethod;

        /// <summary>
        /// Returns the parser to use for the column that maps to the given property when deserialized.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        protected virtual MethodInfo GetParser(TypeInfo forType, PropertyInfo property)
        => GetParser(property.SetMethod.GetParameters()[0].ParameterType.GetTypeInfo());

        /// <summary>
        /// Returns the index of the column that should map to the given property.  Headers
        ///   can change this during deserialization.
        ///   
        /// Return null to leave order unspecified.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        protected virtual int? GetPosition(TypeInfo forType, PropertyInfo property)
        => GetPosition(property);

        /// <summary>
        /// Returns whether or not the given property is required during deserialization.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        protected virtual bool GetIsRequired(TypeInfo forType, PropertyInfo property)
        => GetIsRequired(property);

        // field deserialization defaults


        /// <summary>
        /// Returns true if the given field should be deserialized.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        protected virtual bool ShouldDeserialize(TypeInfo forType, FieldInfo field)
        {
            var dataMember = field.GetCustomAttribute<DataMemberAttribute>();
            if (dataMember != null)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns the name of the column that should map to the given field when deserialized.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        protected virtual string GetDeserializationName(TypeInfo forType, FieldInfo field)
        => GetDeserializationName(field);

        /// <summary>
        /// Returns the parser to use for the column that maps to the given property when deserialized.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        protected virtual MethodInfo GetParser(TypeInfo forType, FieldInfo field)
        => GetParser(field.FieldType.GetTypeInfo());

        /// <summary>
        /// Returns the index of the column that should map to the given field.  Headers
        ///   can change this during deserialization.
        ///   
        /// Return null to leave order unspecified.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        protected virtual int? GetPosition(TypeInfo forType, FieldInfo field)
        => GetPosition(field);

        /// <summary>
        /// Returns whether or not the given field is required during deserialization.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        protected virtual bool GetIsRequired(TypeInfo forType, FieldInfo field)
        => GetIsRequired(field);

        // common deserialization defaults

        private static string GetDeserializationName(MemberInfo member)
        {
            var dataMember = member.GetCustomAttribute<DataMemberAttribute>();
            if (!string.IsNullOrWhiteSpace(dataMember?.Name))
            {
                return dataMember.Name;
            }

            return member.Name;
        }

        private static MethodInfo GetParser(TypeInfo forType)
        => DeserializableMember.GetDefaultParser(forType);

        private static bool GetIsRequired(MemberInfo member)
        {
            var dataMember = member.GetCustomAttribute<DataMemberAttribute>();
            if (dataMember != null)
            {
                return dataMember.IsRequired;
            }

            return false;
        }

        /// <summary>
        /// Enumerate all columns to deserialize.
        /// </summary>
        public virtual IEnumerable<SerializableMember> EnumerateMembersToSerialize(TypeInfo forType)
        {
            var buffer = new List<(SerializableMember Member, int? Position)>();

            foreach (var p in forType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
            {
                if (!ShouldSerialize(forType, p)) continue;

                var name = GetSerializationName(forType, p);
                var getter = GetGetter(forType, p);
                var shouldSerialize = GetShouldSerializeMethod(forType, p);
                var formatter = GetFormatter(forType, p);
                var order = GetPosition(forType, p);
                var emitDefault = GetEmitDefaultValue(forType, p);

                buffer.Add((SerializableMember.Create(forType, name, getter, formatter, shouldSerialize, emitDefault), order));
            }

            foreach (var f in forType.GetFields())
            {
                if (!ShouldSerialize(forType, f)) continue;

                var name = GetSerializationName(forType, f);
                var shouldSerialize = GetShouldSerializeMethod(forType, f);
                var formatter = GetFormatter(forType, f);
                var order = GetPosition(forType, f);
                var emitDefault = GetEmitDefaultValue(forType, f);

                buffer.Add((SerializableMember.Create(forType, name, f, formatter, shouldSerialize, emitDefault), order));
            }

            buffer.Sort(TypeDescribers.SerializableComparer);

            foreach (var (member, _) in buffer)
            {
                yield return member;
            }
        }

        // property serialization defaults

        
        /// <summary>
        /// Returns true if the given property should be serialized.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        protected virtual bool ShouldSerialize(TypeInfo forType, PropertyInfo property)
        {
            if (property.GetMethod == null) return false;

            var ignoreDataMember = property.GetCustomAttribute<IgnoreDataMemberAttribute>();
            if (ignoreDataMember != null)
            {
                return false;
            }

            var dataMember = property.GetCustomAttribute<DataMemberAttribute>();
            if (dataMember != null)
            {
                return true;
            }

            return
                property.GetMethod.IsPublic &&
                !property.GetMethod.IsStatic &&
                property.GetMethod.GetParameters().Length == 0 &&
                property.GetMethod.ReturnType != Types.VoidType &&
                SerializableMember.GetDefaultFormatter(property.GetMethod.ReturnType.GetTypeInfo()) != null;
        }

        /// <summary>
        /// Returns the name of the column that should map to the given property when serialized.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        protected virtual string GetSerializationName(TypeInfo forType, PropertyInfo property)
        => GetDeserializationName(property);

        /// <summary>
        /// Returns the getter to use for the given property when serialized.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        protected virtual MethodInfo GetGetter(TypeInfo forType, PropertyInfo property)
        => property.GetMethod;

        /// <summary>
        /// Returns the ShouldXXX()-style method to use for the given property when serializing, if
        ///   any.
        ///  
        /// If specified, the method will be invoked for each record to determine whether to write
        ///   the property.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        protected virtual MethodInfo GetShouldSerializeMethod(TypeInfo forType, PropertyInfo property)
        {
            var mtd = forType.GetMethod("ShouldSerialize" + property.Name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (mtd == null) return null;

            if (mtd.GetParameters().Length != 0) return null;

            return mtd;
        }

        /// <summary>
        /// Returns the formatter to use for the column that maps to the given property when serialized.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        protected virtual MethodInfo GetFormatter(TypeInfo forType, PropertyInfo property)
        => GetFormatter(property.PropertyType.GetTypeInfo());

        // todo: do we have a test for EmitDefaultValue = false on user defined ValueTypes?

        /// <summary>
        /// Returns whether or not the default value should be serialized for the given property.
        /// 
        /// For reference types, the default value is `null`.  For ValueTypes the default value
        ///   is either 0 or the equivalent of initializing all of it's fields with their default
        ///   values.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        protected virtual bool GetEmitDefaultValue(TypeInfo forType, PropertyInfo property)
        => GetEmitDefaultValue(property);

        // field serialization defaults

        /// <summary>
        /// Returns true if the given field should be serialized.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        protected virtual bool ShouldSerialize(TypeInfo forType, FieldInfo field)
        {
            var dataMember = field.GetCustomAttribute<DataMemberAttribute>();
            if (dataMember != null)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns the name of the column that should map to the given field when serialized.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        protected virtual string GetSerializationName(TypeInfo forType, FieldInfo property)
        => GetDeserializationName(property);

        /// <summary>
        /// Returns the ShouldXXX()-style method to use for the given field when serializing, if
        ///   any.
        ///  
        /// If specified, the method will be invoked for each record to determine whether to write
        ///   the field.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        protected virtual MethodInfo GetShouldSerializeMethod(TypeInfo forType, FieldInfo field)
        => null;

        /// <summary>
        /// Returns the formatter to use for the column that maps to the given field when serialized.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        protected virtual MethodInfo GetFormatter(TypeInfo forType, FieldInfo field)
        => GetFormatter(field.FieldType.GetTypeInfo());

        /// <summary>
        /// Returns whether or not the default value should be serialized for the given property.
        /// 
        /// For reference types, the default value is `null`.  For ValueTypes the default value
        ///   is either 0 or the equivalent of initializing all of it's fields with their default
        ///   values.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        protected virtual bool GetEmitDefaultValue(TypeInfo forType, FieldInfo field)
        => GetEmitDefaultValue(field);

        // common serialization defaults
        private static MethodInfo GetFormatter(TypeInfo t)
        => SerializableMember.GetDefaultFormatter(t);

        private static int? GetPosition(MemberInfo member)
        {
            var dataMember = member.GetCustomAttribute<DataMemberAttribute>();
            if (dataMember != null)
            {
                return dataMember.Order;
            }

            return null;
        }

        private static bool GetEmitDefaultValue(MemberInfo member)
        {
            var dataMember = member.GetCustomAttribute<DataMemberAttribute>();
            if (dataMember != null)
            {
                return dataMember.EmitDefaultValue;
            }

            return true;
        }
    }
}
