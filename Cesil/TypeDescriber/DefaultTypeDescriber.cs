using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using Microsoft.CSharp.RuntimeBinder;

using static Cesil.BindingFlagsConstants;

namespace Cesil
{
    // everything in DefaultTypeDescriber is part of a public API
#pragma warning disable IDE0060

    /// <summary>
    /// The default implementation of ITypeDescriber used to
    ///   determine how to (de)serialize types and how to convert
    ///   dynamic cells and rows.
    ///   
    /// It will serialize all public properties, any fields
    ///   with a [DataMember], and will respect ShouldSerialize()
    ///   methods.
    ///   
    /// It will deserialize all public properties, any fields
    ///   with a [DataMember], and will call Reset() methods.  Expects
    ///   a public parameterless constructor for any deserialized types.
    /// 
    /// It will convert cells to most built-in types, and map rows to
    ///   POCOs, ValueTuples, Tuples, and IEnumerables.
    /// 
    /// This type is unsealed to allow for easy extension of it's behavior.
    /// </summary>
    [IntentionallyExtensible("Does 'what is expected' so minor tweaks can be handled with inheritance.")]
    public partial class DefaultTypeDescriber : ITypeDescriber
    {
        private static readonly DynamicRowConverter PassthroughEnumerable = DynamicRowConverter.ForConstructorTakingDynamic(Constructors.PassthroughRowEnumerable);
        private static readonly DynamicRowConverter Identity =
            DynamicRowConverter.ForDelegate(
                (object obj, in ReadContext _, out object res) =>
                {
                    res = obj;
                    return true;
                }
            );

        /// <summary>
        /// Construct a new DefaultTypeDesciber.
        /// 
        /// A pre-allocated instance is available on TypeDescribers.Default.
        /// </summary>
        public DefaultTypeDescriber()
        {
            // only use the caches if we're not in a subclass
            CanCache = GetType() == Types.DefaultTypeDescriber;

            DelegateCache = new ConcurrentDictionary<object, Delegate>();
            DeserializableMembers = new ConcurrentDictionary<TypeInfo, IEnumerable<DeserializableMember>>();
            SerializableMembers = new ConcurrentDictionary<TypeInfo, IEnumerable<SerializableMember>>();
            Formatters = new ConcurrentDictionary<TypeInfo, Formatter>();
        }

        /// <summary>
        /// Gets an InstanceProvider that wraps the parameterless constructor
        ///   for reference types, and the zero value for value types.
        ///   
        /// Returns null if no InstanceProvider can be found.
        /// </summary>
        [return: NullableExposed("May not be known, null is cleanest way to handle it")]
        public virtual InstanceProvider? GetInstanceProvider(TypeInfo forType)
        => InstanceProvider.GetDefault(forType);

        /// <summary>
        /// Enumerate all columns to deserialize.
        /// </summary>
        public virtual IEnumerable<DeserializableMember> EnumerateMembersToDeserialize(TypeInfo forType)
        {
            Utils.CheckArgumentNull(forType, nameof(forType));

            if (CanCache)
            {
                if (TryGetDeserializableMembers(forType, out var earlyRet))
                {
                    return earlyRet;
                }
            }

            IEnumerable<DeserializableMember> ret;

            if (CheckReadingWellKnownType(forType, out var knownMember))
            {
                // no need to create something fancy for a single member
                ret = new[] { knownMember };
            }
            else
            {
                // use something slightly fancy so we don't have to sort at the end
                // 
                // note that this will handle the "actually, order is always null"
                //      optimization case, it won't bother to track ordering
                //      until a non-null is found
                var buffer = MemberOrderHelper<DeserializableMember>.Create();

                foreach (var p in forType.GetProperties(All))
                {
                    if (!ShouldDeserialize(forType, p)) continue;

                    var name = GetDeserializationName(forType, p);
                    var setter = GetSetter(forType, p);
                    var parser = GetParser(forType, p);
                    var order = GetOrder(forType, p);
                    var isRequired = GetIsRequired(forType, p);
                    var reset = GetReset(forType, p);

                    buffer.Add(order, DeserializableMember.CreateInner(forType, name, setter, parser, isRequired, reset));
                }

                foreach (var f in forType.GetFields())
                {
                    if (!ShouldDeserialize(forType, f)) continue;

                    var name = GetDeserializationName(forType, f);
                    var setter = GetSetter(forType, f);
                    var parser = GetParser(forType, f);
                    var order = GetOrder(forType, f);
                    var isRequired = GetIsRequired(forType, f);
                    var reset = GetReset(forType, f);

                    buffer.Add(order, DeserializableMember.CreateInner(forType, name, setter, parser, isRequired, reset));
                }

                ret = buffer;
            }

            if (CanCache)
            {
                AddDeserializableMembers(forType, ret);
            }

            return ret;
        }

        // property deserialization defaults

        /// <summary>
        /// Returns true if the given property should be deserialized.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        protected virtual bool ShouldDeserialize(TypeInfo forType, PropertyInfo property)
        {
            Utils.CheckArgumentNull(forType, nameof(forType));
            Utils.CheckArgumentNull(property, nameof(property));

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
                Parser.GetDefault(property.SetMethod.GetParameters()[0].ParameterType.GetTypeInfo()) != null;
        }

        /// <summary>
        /// Returns the name of the column that should map to the given property when deserialized.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        protected virtual string GetDeserializationName(TypeInfo forType, PropertyInfo property)
        {
            Utils.CheckArgumentNull(forType, nameof(forType));
            Utils.CheckArgumentNull(property, nameof(property));

            return GetName(property);
        }

        /// <summary>
        /// Returns the setter to use for the given property when deserialized.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        [return: NullableExposed("May not be known, null is cleanest way to handle it")]
        protected virtual Setter? GetSetter(TypeInfo forType, PropertyInfo property)
        {
            Utils.CheckArgumentNull(forType, nameof(forType));
            Utils.CheckArgumentNull(property, nameof(property));

            return (Setter?)property.SetMethod;
        }

        /// <summary>
        /// Returns the parser to use for the column that maps to the given property when deserialized.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        [return: NullableExposed("May not be known, null is cleanest way to handle it")]
        protected virtual Parser? GetParser(TypeInfo forType, PropertyInfo property)
        {
            Utils.CheckArgumentNull(forType, nameof(forType));
            Utils.CheckArgumentNull(property, nameof(property));

            return GetParser(property.PropertyType.GetTypeInfo());
        }

        /// <summary>
        /// Returns the index of the column that should map to the given property.  Headers
        ///   can change this during deserialization.
        ///   
        /// Return null to leave order unspecified.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        protected virtual int? GetOrder(TypeInfo forType, PropertyInfo property)
        {
            Utils.CheckArgumentNull(forType, nameof(forType));
            Utils.CheckArgumentNull(property, nameof(property));

            return GetOrder(property);
        }

        /// <summary>
        /// Returns whether or not the given property is required during deserialization.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        protected virtual MemberRequired GetIsRequired(TypeInfo forType, PropertyInfo property)
        {
            Utils.CheckArgumentNull(forType, nameof(forType));
            Utils.CheckArgumentNull(property, nameof(property));

            return GetIsRequired(property);
        }

        /// <summary>
        /// Returns the reset method, if any, to call prior to deserializing the given property.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        [return: NullableExposed("May not be known, null is cleanest way to handle it")]
        protected virtual Reset? GetReset(TypeInfo forType, PropertyInfo property)
        {
            Utils.CheckArgumentNull(forType, nameof(forType));
            Utils.CheckArgumentNull(property, nameof(property));

            // intentionally letting this be null
            var mtd = forType.GetMethod("Reset" + property.Name, All);
            if (mtd == null) return null;

            if (mtd.IsStatic)
            {
                if (mtd.GetParameters().Length > 1) return null;
            }
            else
            {
                if (mtd.GetParameters().Length != 0) return null;
            }

            return Reset.ForMethod(mtd);
        }

        // field deserialization defaults


        /// <summary>
        /// Returns true if the given field should be deserialized.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        protected virtual bool ShouldDeserialize(TypeInfo forType, FieldInfo field)
        {
            Utils.CheckArgumentNull(forType, nameof(forType));
            Utils.CheckArgumentNull(field, nameof(field));

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
        {
            Utils.CheckArgumentNull(forType, nameof(forType));
            Utils.CheckArgumentNull(field, nameof(field));

            return GetName(field);
        }

        /// <summary>
        /// Returns the setter to use for the given field when deserialized.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        [return: NullableExposed("May not be known, null is cleanest way to handle it")]
        protected virtual Setter? GetSetter(TypeInfo forType, FieldInfo field)
        {
            Utils.CheckArgumentNull(forType, nameof(forType));
            Utils.CheckArgumentNull(field, nameof(field));

            return Setter.ForField(field);
        }

        /// <summary>
        /// Returns the parser to use for the column that maps to the given property when deserialized.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        [return: NullableExposed("May not be known, null is cleanest way to handle it")]
        protected virtual Parser? GetParser(TypeInfo forType, FieldInfo field)
        {
            Utils.CheckArgumentNull(forType, nameof(forType));
            Utils.CheckArgumentNull(field, nameof(field));

            return GetParser(field.FieldType.GetTypeInfo());
        }

        /// <summary>
        /// Returns the index of the column that should map to the given field.  Headers
        ///   can change this during deserialization.
        ///   
        /// Return null to leave order unspecified.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        protected virtual int? GetOrder(TypeInfo forType, FieldInfo field)
        {
            Utils.CheckArgumentNull(forType, nameof(forType));
            Utils.CheckArgumentNull(field, nameof(field));

            return GetOrder(field);
        }

        /// <summary>
        /// Returns whether or not the given field is required during deserialization.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        protected virtual MemberRequired GetIsRequired(TypeInfo forType, FieldInfo field)
        {
            Utils.CheckArgumentNull(forType, nameof(forType));
            Utils.CheckArgumentNull(field, nameof(field));

            return GetIsRequired(field);
        }

        /// <summary>
        /// Returns the reset method, if any, to call prior to deserializing the given field.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        [return: NullableExposed("May not be known, null is cleanest way to handle it")]
        protected virtual Reset? GetReset(TypeInfo forType, FieldInfo field)
        {
            Utils.CheckArgumentNull(forType, nameof(forType));
            Utils.CheckArgumentNull(field, nameof(field));

            return null;
        }

        // common deserialization defaults

        /// <summary>
        /// Returns the parser to use for the given type.
        /// 
        /// If you do not care about the member being parsed, override just this method
        ///   as the other GetParser(...) methods delegate to it.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        [return: NullableExposed("May not be known, null is cleanest way to handle it")]
        public virtual Parser? GetParser(TypeInfo forType)
        => Parser.GetDefault(forType);

        private static string GetName(MemberInfo member)
        {
            var dataMember = member.GetCustomAttribute<DataMemberAttribute>();
            if (!string.IsNullOrWhiteSpace(dataMember?.Name))
            {
                return dataMember.Name;
            }

            return member.Name;
        }



        private static MemberRequired GetIsRequired(MemberInfo member)
        {
            var dataMember = member.GetCustomAttribute<DataMemberAttribute>();
            if (dataMember != null)
            {
                return dataMember.IsRequired ? MemberRequired.Yes : MemberRequired.No;
            }

            return MemberRequired.No;
        }

        /// <summary>
        /// Enumerate all columns to deserialize.
        /// </summary>
        public virtual IEnumerable<SerializableMember> EnumerateMembersToSerialize(TypeInfo forType)
        {
            if (forType.IsBigTuple() || forType.IsBigValueTuple())
            {
                return Throw.InvalidOperationException<IEnumerable<SerializableMember>>($"{forType.Name} is a tuple with a Rest property, the {nameof(DefaultTypeDescriber)} cannot serialize this unambiguously.");
            }

            if (CanCache)
            {
                // we _know_ this isn't a subclass, so we know what our behavior will be
                //    and therefore we can optimize this to avoid pointless repeated lookups
                if (TryGetSerializableMembers(forType, out var earlyRet))
                {
                    return earlyRet;
                }
            }

            IEnumerable<SerializableMember> ret;

            if (CheckWritingWellKnownType(forType, out var knownMember))
            {
                // no need to spin up something big for a single member
                ret = new[] { knownMember };
            }
            else
            {
                var buffer = MemberOrderHelper<SerializableMember>.Create();

                foreach (var p in forType.GetProperties(All))
                {
                    if (!ShouldSerialize(forType, p)) continue;

                    var name = GetSerializationName(forType, p);
                    var getter = GetGetter(forType, p);
                    var shouldSerialize = GetShouldSerialize(forType, p);
                    var formatter = GetFormatter(forType, p);
                    var order = GetOrder(forType, p);
                    var emitDefault = GetEmitDefaultValue(forType, p);

                    buffer.Add(order, SerializableMember.CreateInner(forType, name, getter, formatter, shouldSerialize, emitDefault));
                }

                foreach (var f in forType.GetFields(All))
                {
                    if (!ShouldSerialize(forType, f)) continue;

                    var name = GetSerializationName(forType, f);
                    var getter = GetGetter(forType, f);
                    var shouldSerialize = GetShouldSerialize(forType, f);
                    var formatter = GetFormatter(forType, f);
                    var order = GetOrder(forType, f);
                    var emitDefault = GetEmitDefaultValue(forType, f);

                    buffer.Add(order, SerializableMember.CreateInner(forType, name, getter, formatter, shouldSerialize, emitDefault));
                }

                ret = buffer;
            }

            if (CanCache)
            {
                AddSerializableMembers(forType, ret);
            }

            return ret;
        }

        // property serialization defaults


        /// <summary>
        /// Returns true if the given property should be serialized.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        protected virtual bool ShouldSerialize(TypeInfo forType, PropertyInfo property)
        {
            Utils.CheckArgumentNull(forType, nameof(forType));
            Utils.CheckArgumentNull(property, nameof(property));

            var getMtd = property.GetMethod;
            if (getMtd == null) return false;

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

            if (!getMtd.IsPublic || getMtd.IsStatic) return false;
            if (getMtd.GetParameters().Length != 0) return false;

            if (GetFormatter(forType, property) == null) return false;

            return true;
        }

        /// <summary>
        /// Returns the name of the column that should map to the given property when serialized.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        protected virtual string GetSerializationName(TypeInfo forType, PropertyInfo property)
        {
            Utils.CheckArgumentNull(forType, nameof(forType));
            Utils.CheckArgumentNull(property, nameof(property));

            return GetName(property);
        }

        /// <summary>
        /// Returns the getter to use for the given property when serialized.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        [return: NullableExposed("May not be known, null is cleanest way to handle it")]
        protected virtual Getter? GetGetter(TypeInfo forType, PropertyInfo property)
        {
            Utils.CheckArgumentNull(forType, nameof(forType));
            Utils.CheckArgumentNull(property, nameof(property));

            return (Getter?)property.GetMethod;
        }

        /// <summary>
        /// Returns the ShouldSerializeXXX()-style method to use for the given property when serializing, if
        ///   any.
        ///  
        /// If specified, the method will be invoked for each record to determine whether to write
        ///   the property.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        [return: NullableExposed("May not be known, null is cleanest way to handle it")]
        protected virtual ShouldSerialize? GetShouldSerialize(TypeInfo forType, PropertyInfo property)
        {
            Utils.CheckArgumentNull(forType, nameof(forType));
            Utils.CheckArgumentNull(property, nameof(property));

            // intentionally letting this be null
            var mtd = forType.GetMethod("ShouldSerialize" + property.Name, All);
            if (mtd == null) return null;

            if (mtd.ReturnType != Types.Bool) return null;

            if (mtd.IsStatic)
            {
                if (mtd.GetParameters().Length > 1) return null;
            }
            else
            {
                if (mtd.GetParameters().Length != 0) return null;
            }

            return (ShouldSerialize?)mtd;
        }

        /// <summary>
        /// Returns the formatter to use for the column that maps to the given property when serialized.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        [return: NullableExposed("May not be known, null is cleanest way to handle it")]
        protected virtual Formatter? GetFormatter(TypeInfo forType, PropertyInfo property)
        {
            Utils.CheckArgumentNull(forType, nameof(forType));
            Utils.CheckArgumentNull(property, nameof(property));

            return GetFormatter(property.PropertyType.GetTypeInfo());
        }

        /// <summary>
        /// Returns whether or not the default value should be serialized for the given property.
        /// 
        /// For reference types, the default value is `null`.  For ValueTypes the default value
        ///   is either 0 or the equivalent of initializing all of it's fields with their default
        ///   values.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        protected virtual EmitDefaultValue GetEmitDefaultValue(TypeInfo forType, PropertyInfo property)
        {
            Utils.CheckArgumentNull(forType, nameof(forType));
            Utils.CheckArgumentNull(property, nameof(property));

            return GetEmitDefaultValue(property);
        }

        // field serialization defaults

        /// <summary>
        /// Returns true if the given field should be serialized.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        protected virtual bool ShouldSerialize(TypeInfo forType, FieldInfo field)
        {
            Utils.CheckArgumentNull(forType, nameof(forType));
            Utils.CheckArgumentNull(field, nameof(field));

            if (field.IsPublic && !field.IsStatic)
            {
                var dataMember = field.GetCustomAttribute<DataMemberAttribute>();
                if (dataMember != null)
                {
                    return true;
                }
            }

            if (forType.IsValueTuple())
            {
                var fieldType = field.FieldType.GetTypeInfo();

                return
                    fieldType != Types.Void &&
                    Formatter.GetDefault(fieldType) != null;
            }

            return false;
        }

        /// <summary>
        /// Returns the getter to use for the given field when serialized.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        [return: NullableExposed("May not be known, null is cleanest way to handle it")]
        protected virtual Getter? GetGetter(TypeInfo forType, FieldInfo field)
        {
            Utils.CheckArgumentNull(forType, nameof(forType));
            Utils.CheckArgumentNull(field, nameof(field));

            return Getter.ForField(field);
        }

        /// <summary>
        /// Returns the name of the column that should map to the given field when serialized.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        protected virtual string GetSerializationName(TypeInfo forType, FieldInfo field)
        {
            Utils.CheckArgumentNull(forType, nameof(forType));
            Utils.CheckArgumentNull(field, nameof(field));

            return GetName(field);
        }

        /// <summary>
        /// Returns the ShouldSerializeXXX()-style method to use for the given field when serializing, if
        ///   any. By default, always returns null.
        ///  
        /// If specified, the method will be invoked for each record to determine whether to write
        ///   the field.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        [return: NullableExposed("May not be known, null is cleanest way to handle it")]
        protected virtual ShouldSerialize? GetShouldSerialize(TypeInfo forType, FieldInfo field)
        {
            Utils.CheckArgumentNull(forType, nameof(forType));
            Utils.CheckArgumentNull(field, nameof(field));

            return null;
        }

        /// <summary>
        /// Returns the formatter to use for the column that maps to the given field when serialized.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        [return: NullableExposed("May not be known, null is cleanest way to handle it")]
        protected virtual Formatter? GetFormatter(TypeInfo forType, FieldInfo field)
        {
            Utils.CheckArgumentNull(forType, nameof(forType));
            Utils.CheckArgumentNull(field, nameof(field));

            return GetFormatter(field.FieldType.GetTypeInfo());
        }

        /// <summary>
        /// Returns whether or not the default value should be serialized for the given property.
        /// 
        /// For reference types, the default value is `null`.  For ValueTypes the default value
        ///   is either 0 or the equivalent of initializing all of it's fields with their default
        ///   values.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        protected virtual EmitDefaultValue GetEmitDefaultValue(TypeInfo forType, FieldInfo field)
        {
            Utils.CheckArgumentNull(forType, nameof(forType));
            Utils.CheckArgumentNull(field, nameof(field));

            return GetEmitDefaultValue(field);
        }

        // common serialization defaults

        /// <summary>
        /// Returns the formatter to use for the given type.
        /// 
        /// If you do not care about the member being parsed, override just this method
        ///   as the other GetFormatter(...) methods delegate to it.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        [return: NullableExposed("May not be known, null is cleanest way to handle it")]
        protected virtual Formatter? GetFormatter(TypeInfo t)
        => Formatter.GetDefault(t);

        private static int? GetOrder(MemberInfo member)
        {
            var dataMember = member.GetCustomAttribute<DataMemberAttribute>();
            if (dataMember != null)
            {
                var ret = dataMember.Order;

                if (ret < 0)
                {
                    // can't actually find this in the documentation, _but_ 
                    //   the setter throws on values < 0 and the default
                    //   value is -1... so < 0 means not set
                    // weird behavior from predating nullable value types
                    return null;
                }

                return ret;
            }

            return null;
        }

        private static EmitDefaultValue GetEmitDefaultValue(MemberInfo member)
        {
            var dataMember = member.GetCustomAttribute<DataMemberAttribute>();
            if (dataMember != null)
            {
                if (dataMember.EmitDefaultValue)
                {
                    return EmitDefaultValue.Yes;
                }
                else
                {
                    return EmitDefaultValue.No;
                }
            }

            return EmitDefaultValue.Yes;
        }

        /// <summary>
        /// Discovers cells for the given dynamic row.
        /// 
        /// If the span is too small, the needed size is returned and the span
        /// is left in an indeterminate state.
        /// 
        /// Null rows have no cells, but are legal.
        /// 
        /// Rows created by Cesil have their cells enumerated as strings.
        /// 
        /// Other dynamic types will have each member enumerated as either their
        /// actual type (if a formatter is available) or as a string.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        [return: IntentionallyExposedPrimitive("Count, int is the best option")]
        public virtual int GetCellsForDynamicRow(in WriteContext context, dynamic row, Span<DynamicCellValue> cells)
        {
            // handle no value
            if (row is null)
            {
                return 0;
            }

            var rowObj = row as object;

            // handle serializing our own dynamic types
            if (rowObj is DynamicRow asOwnRow)
            {
                var cols = asOwnRow.Columns;

                // can we fit?
                if (cols.Count > cells.Length)
                {
                    return cols.Count;
                }

                var nextRetIx = 0;

                var ix = 0;
                foreach (var col in cols)
                {
                    var name = col.Name;
                    if (!CanCache && !ShouldIncludeCell(name, in context, rowObj))
                    {
                        goto endLoop;
                    }

                    string? value;
                    if (asOwnRow.TryGetDataSpan(ix, out var valueRaw))
                    {
                        value = new string(valueRaw);
                    }
                    else
                    {
                        value = null;
                    }

                    Formatter? formatter;
                    if (CanCache)
                    {
                        var cache = this;

                        if (!cache.TryGetFormatter(Types.String, out formatter))
                        {
                            formatter = GetFormatter(Types.String, name, in context, rowObj);
                            if (formatter != null)
                            {
                                cache.AddFormatter(Types.String, formatter);
                            }
                        }
                    }
                    else
                    {
                        formatter = GetFormatter(Types.String, name, in context, rowObj);
                    }

                    if (formatter == null)
                    {
                        return Throw.InvalidOperationException<int>($"No formatter returned by {nameof(GetFormatter)}");
                    }

                    cells[nextRetIx] = DynamicCellValue.Create(name, value, formatter);

                    nextRetIx++;

endLoop:
                    ix++;
                }

                return nextRetIx;
            }

            if (rowObj is DynamicRowRange asOwnRowRange)
            {
                var cols = asOwnRowRange.Columns;

                // can we fit?
                if (cols.Count > cells.Length)
                {
                    return cols.Count;
                }

                var realRow = asOwnRowRange.Parent;

                var nextRetIx = 0;

                var ix = 0;
                foreach (var col in cols)
                {
                    var name = col.Name;
                    if (!CanCache && !ShouldIncludeCell(name, in context, rowObj))
                    {
                        goto endLoop;
                    }

                    var actualIndex = ix + (asOwnRowRange.Offset ?? 0);

                    string? value;
                    if (realRow.TryGetDataSpan(actualIndex, out var valueRaw))
                    {
                        value = new string(valueRaw);
                    }
                    else
                    {
                        value = null;
                    }

                    Formatter? formatter;
                    if (CanCache)
                    {
                        var cache = this;

                        if (!cache.TryGetFormatter(Types.String, out formatter))
                        {
                            formatter = GetFormatter(Types.String, name, in context, rowObj);
                            if (formatter != null)
                            {
                                cache.AddFormatter(Types.String, formatter);
                            }
                        }
                    }
                    else
                    {
                        formatter = GetFormatter(Types.String, name, in context, rowObj);
                    }

                    if (formatter == null)
                    {
                        return Throw.InvalidOperationException<int>($"No formatter returned by {nameof(GetFormatter)}");
                    }

                    cells[nextRetIx] = DynamicCellValue.Create(name, value, formatter);

                    nextRetIx++;

endLoop:
                    ix++;
                }

                return nextRetIx;
            }

            // special case the most convenient dynamic type
            if (rowObj is ExpandoObject asExpando)
            {
                var asCollection = (ICollection<KeyValuePair<string, object>>)asExpando;
                if (asCollection.Count > cells.Length)
                {
                    return asCollection.Count;
                }

                var nextRetIx = 0;

                foreach (var kv in asExpando)
                {
                    var name = kv.Key;
                    var value = kv.Value;
                    Formatter? formatter;

                    if (!CanCache && !ShouldIncludeCell(name, in context, rowObj)) continue;

                    if (value == null)
                    {
                        formatter = GetFormatter(Types.String, name, in context, rowObj);
                    }
                    else
                    {
                        var valueType = value.GetType().GetTypeInfo();

                        if (CanCache)
                        {
                            if (!TryGetFormatter(valueType, out formatter))
                            {
                                formatter = GetFormatter(valueType, name, in context, rowObj);
                                if (formatter != null)
                                {
                                    AddFormatter(valueType, formatter);
                                }
                            }
                        }
                        else
                        {
                            formatter = GetFormatter(valueType, name, in context, rowObj);
                        }

                        if (formatter == null)
                        {
                            // try and coerce into a string?
                            var convert = Microsoft.CSharp.RuntimeBinder.Binder.Convert(0, Types.String, valueType);
                            var convertCallSite = CallSite<Func<CallSite, object, object>>.Create(convert);
                            try
                            {
                                value = convertCallSite.Target.Invoke(convertCallSite, value);
                                formatter = Formatter.GetDefault(Types.String);
                            }
                            catch
                            {
                                /* intentionally left blank */
                            }
                        }
                    }

                    // skip anything that isn't formattable
                    if (formatter == null) continue;

                    cells[nextRetIx] = DynamicCellValue.Create(name, value, formatter);
                    nextRetIx++;
                }

                return nextRetIx;
            }

            var rowObjType = rowObj.GetType().GetTypeInfo();

            // now the least convenient dynamic type
            if (rowObj is IDynamicMetaObjectProvider asDynamic)
            {
                var arg = Expressions.Parameter_Object;
                var metaObj = asDynamic.GetMetaObject(arg);

                var names = metaObj.GetDynamicMemberNames();
                var namesCount = names.Count();
                if (namesCount > cells.Length)
                {
                    return namesCount;
                }

                var nextRetIx = 0;
                foreach (var name in names)
                {
                    var args = new[] { CSharpArgumentInfo.Create(default, null) };
                    var getMember = Microsoft.CSharp.RuntimeBinder.Binder.GetMember(default, name, rowObjType, args);
                    var getMemberCallSite = CallSite<Func<CallSite, object, object>>.Create(getMember);

                    var skip = false;
                    object? value;
                    Formatter? formatter;
                    try
                    {
                        value = getMemberCallSite.Target.Invoke(getMemberCallSite, rowObj);
                    }
                    catch
                    {
                        value = null;
                        skip = true;
                    }

                    // skip it, access failed
                    if (skip) continue;

                    if (!CanCache && !ShouldIncludeCell(name, in context, rowObj)) continue;

                    if (value == null)
                    {
                        formatter = GetFormatter(Types.String, name, in context, rowObj);
                    }
                    else
                    {
                        var valueType = value.GetType().GetTypeInfo();

                        if (CanCache)
                        {
                            if (!TryGetFormatter(valueType, out formatter))
                            {
                                formatter = GetFormatter(valueType, name, in context, rowObj);
                                if (formatter != null)
                                {
                                    AddFormatter(valueType, formatter);
                                }
                            }
                        }
                        else
                        {
                            formatter = GetFormatter(valueType, name, in context, rowObj);
                        }

                        if (formatter == null)
                        {
                            // try and coerce into a string?
                            var convert = Microsoft.CSharp.RuntimeBinder.Binder.Convert(0, Types.String, valueType);
                            var convertCallSite = CallSite<Func<CallSite, object, object>>.Create(convert);
                            try
                            {
                                value = convertCallSite.Target.Invoke(convertCallSite, value);
                                formatter = GetFormatter(Types.String, name, in context, rowObj);
                            }
                            catch
                            {
                                /* intentionally left blank */
                            }
                        }
                    }

                    // skip it, can't serialize it
                    if (formatter == null) continue;

                    cells[nextRetIx] = DynamicCellValue.Create(name, value, formatter);
                    nextRetIx++;
                }

                return nextRetIx;
            }

            // now just plain old types
            {
                var toSerialize = EnumerateMembersToSerialize(rowObjType);
                var toSerializeCount = toSerialize.Count();
                if (toSerializeCount > cells.Length)
                {
                    return toSerializeCount;
                }

                var nextRetIx = 0;

                foreach (var mem in toSerialize)
                {
                    // todo: probably refactor a bit to make this workable?
                    if (mem.IsBackedByGeneratedMethod)
                    {
                        return Throw.InvalidOperationException<int>($"{nameof(GetCellsForDynamicRow)} cannot dynamically access members backed by source generated serializers");
                    }

                    var name = mem.Name;
                    if (!CanCache && !ShouldIncludeCell(name, in context, rowObj)) continue;

                    var getter = mem.Getter;

                    Formatter? formatter;
                    if (CanCache)
                    {
                        if (!TryGetFormatter(getter.Returns, out formatter))
                        {
                            formatter = GetFormatter(getter.Returns, name, in context, rowObj);
                            if (formatter != null)
                            {
                                AddFormatter(getter.Returns, formatter);
                            }
                        }
                    }
                    else
                    {
                        formatter = GetFormatter(getter.Returns, name, in context, rowObj);
                    }

                    if (formatter == null)
                    {
                        return Throw.InvalidOperationException<int>($"No formatter returned by {nameof(GetFormatter)}");
                    }

                    var delProvider = ((ICreatesCacheableDelegate<Getter.DynamicGetterDelegate>)getter);
                    var del = delProvider.Guarantee(this);
                    var value = del(rowObj, in context);

                    cells[nextRetIx] = DynamicCellValue.Create(name, value, formatter);
                    nextRetIx++;
                }

                return nextRetIx;
            }
        }

        /// <summary>
        /// Called in GetCellsForDynamicRow to determine whether a cell should be included.
        /// 
        /// Override to customize behavior.
        /// </summary>
        protected virtual bool ShouldIncludeCell(string name, in WriteContext context, dynamic row)
        => true;

        /// <summary>
        /// Called in GetCellsForDynamicRow to determine the formatter that should be used for a cell.
        /// 
        /// Override to customize behavior.
        /// </summary>
        [return: NullableExposed("May not be known, null is cleanest way to handle it")]
        protected virtual Formatter? GetFormatter(TypeInfo forType, string name, in WriteContext context, dynamic row)
        => Formatter.GetDefault(forType);

        /// <summary>
        /// Returns a Parser that can be used to parse the targetType.
        /// 
        /// Override to customize behavior.
        /// </summary>
        [return: NullableExposed("May not be known, null is cleanest way to handle it")]
        public virtual Parser? GetDynamicCellParserFor(in ReadContext context, TypeInfo targetType)
        {
            var onePCons = targetType.GetConstructor(PublicInstance, null, Types.ParserConstructorOneParameter_Array, null);
            var twoPCons = targetType.GetConstructor(PublicInstance, null, Types.ParserConstructorTwoParameter_Array, null);
            var cons = onePCons ?? twoPCons;
            if (cons != null)
            {
                return Parser.ForConstructor(cons);
            }

            var parser = Parser.GetDefault(targetType);
            if (parser != null)
            {
                return parser;
            }

            return null;
        }

        /// <summary>
        /// Returns a DynamicRowConverter that can be used to parse the targetType,
        ///    if a default parser for the type exists or a constructor accepting
        ///    the appropriate number of objects (can be dynamic in source) is on 
        ///    the type.
        /// </summary>
        [return: NullableExposed("May not be known, null is cleanest way to handle it")]
        public virtual DynamicRowConverter? GetDynamicRowConverter(in ReadContext context, IEnumerable<ColumnIdentifier> columns, TypeInfo targetType)
        {
            // handle tuples
            if (IsValueTuple(targetType))
            {
                var mtd = Types.TupleDynamicParsers.MakeGenericType(targetType).GetTypeInfo();
                var genMtd = mtd.GetMethodNonNull(nameof(TupleDynamicParsers<object>.TryConvertValueTuple), InternalStatic);
                return DynamicRowConverter.ForMethod(genMtd);
            }
            else if (IsTuple(targetType))
            {
                var mtd = Types.TupleDynamicParsers.MakeGenericType(targetType).GetTypeInfo();
                var genMtd = mtd.GetMethodNonNull(nameof(TupleDynamicParsers<object>.TryConvertTuple), InternalStatic);
                return DynamicRowConverter.ForMethod(genMtd);
            }

            // handle IEnumerables
            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition().GetTypeInfo() == Types.IEnumerableOfT)
            {
                var elementType = targetType.GetGenericArguments()[0].GetTypeInfo();
                if (elementType != Types.Object)
                {
                    var genEnum = Types.DynamicRowEnumerable.MakeGenericType(elementType).GetTypeInfo();
                    var cons = genEnum.GetConstructorNonNull(InternalInstance, null, new[] { Types.Object }, null);
                    return DynamicRowConverter.ForConstructorTakingDynamic(cons);
                }
                else
                {
                    // in this case, we're basically casting to `dynamic`, so we don't want ANY conversion to happen.
                    return PassthroughEnumerable;
                }
            }
            else if (targetType == Types.IEnumerable)
            {
                var cons = Types.DynamicRowEnumerableNonGeneric.GetConstructorNonNull(InternalInstance, null, new[] { Types.Object }, null);
                return DynamicRowConverter.ForConstructorTakingDynamic(cons);
            }

            // a plain object cast is also allowed
            if (targetType == Types.Object)
            {
                return Identity;
            }

            int width;
            if (columns is ICollection<ColumnIdentifier> c)
            {
                width = c.Count;
            }
            else
            {
                width = 0;
                foreach (var _ in columns)
                {
                    width++;
                }
            }

            var isConsPOCO = IsConstructorPOCO(width, targetType);
            if (isConsPOCO.HasValue)
            {
                return DynamicRowConverter.ForConstructorTakingTypedParameters(isConsPOCO.Constructor.Value, isConsPOCO.Columns.Value);
            }

            var isPropPOCO = IsPropertyPOCO(targetType, columns);
            if (isPropPOCO.HasValue)
            {
                return DynamicRowConverter.ForEmptyConstructorAndSetters(isPropPOCO.Constructor.Value, isPropPOCO.Setters.Value, isPropPOCO.Columns.Value);
            }

            return null;
        }

        private static bool IsTuple(TypeInfo t)
        {
            if (!t.IsGenericType || t.IsGenericTypeDefinition) return false;

            var genType = t.GetGenericTypeDefinition();
            return Array.IndexOf(Types.Tuple_Array, genType) != -1;
        }

        private static bool IsValueTuple(TypeInfo t)
        {
            if (!t.IsGenericType || t.IsGenericTypeDefinition) return false;

            var genType = t.GetGenericTypeDefinition();
            return Array.IndexOf(Types.ValueTuple_Array, genType) != -1;
        }

        private static ConstructorPOCOResult IsConstructorPOCO(int width, TypeInfo type)
        {
            foreach (var cons in type.GetConstructors(AllInstance))
            {
                var consPs = cons.GetParameters();
                if (consPs.Length != width) continue;

                var columnIndexes = new ColumnIdentifier[consPs.Length];
                for (var i = 0; i < columnIndexes.Length; i++)
                {
                    columnIndexes[i] = ColumnIdentifier.Create(i);
                }

                return new ConstructorPOCOResult(cons, columnIndexes);
            }

            return ConstructorPOCOResult.Empty;
        }

        private static PropertyPOCOResult IsPropertyPOCO(TypeInfo type, IEnumerable<ColumnIdentifier> columns)
        {
            var emptyCons = type.GetConstructor(AllInstance, null, Type.EmptyTypes, null);
            if (emptyCons == null)
            {
                return PropertyPOCOResult.Empty;
            }

            var allProperties = type.GetProperties(All);

            var setters = new Setter[allProperties.Length];
            var columnIndexes = new ColumnIdentifier[allProperties.Length];

            var ix = 0;
            var i = 0;
            foreach (var col in columns)
            {
                if (!col.HasName)
                {
                    return PropertyPOCOResult.Empty;
                }

                var colName = col.Name;

                PropertyInfo? prop = null;
                for (var j = 0; j < allProperties.Length; j++)
                {
                    var p = allProperties[j];
                    if (p.Name == colName)
                    {
                        prop = p;
                    }
                }

                if (prop == null)
                {
                    goto loopEnd;
                }

                var setterMtd = prop.SetMethod;
                if (setterMtd == null)
                {
                    goto loopEnd;
                }

                if (setterMtd.ReturnType.GetTypeInfo() != Types.Void)
                {
                    goto loopEnd;
                }

                if (setterMtd.GetParameters().Length != 1) continue;

                setters[ix] = Setter.ForMethod(setterMtd);
                columnIndexes[ix] = ColumnIdentifier.Create(i);

                ix++;

loopEnd:
                i++;
            }

            if (ix != setters.Length)
            {
                Array.Resize(ref setters, ix);
                Array.Resize(ref columnIndexes, ix);
            }

            return new PropertyPOCOResult(emptyCons, setters, columnIndexes);
        }

        private bool CheckReadingWellKnownType(TypeInfo forType, [MaybeNullWhen(returnValue: false)] out DeserializableMember member)
        {
            if (!WellKnownRowTypes.TryGetSetter(forType, out var name, out var setter))
            {
                member = null;
                return false;
            }

            var parser = GetParser(forType);
            if (parser == null)
            {
                member = null;
                return false;
            }

            member = DeserializableMember.CreateInner(forType, name, setter, parser, MemberRequired.No, null);
            return true;
        }

        private bool CheckWritingWellKnownType(TypeInfo forType, [MaybeNullWhen(returnValue: false)] out SerializableMember member)
        {
            if (!WellKnownRowTypes.TryGetGetter(forType, out var name, out var getter))
            {
                member = null;
                return false;
            }

            var formatter = GetFormatter(forType);
            if (formatter == null)
            {
                member = null;
                return false;
            }

            member = SerializableMember.CreateInner(forType, name, getter, formatter, null, EmitDefaultValue.Yes);
            return true;
        }

        /// <summary>
        /// Returns a representation of this DefaultTypeDescriber object.
        /// 
        /// Only for debugging, this value is not guaranteed to be stable.
        /// </summary>
        public override string ToString()
        {
            var isCommon = ReferenceEquals(this, TypeDescribers.Default);
            if (isCommon)
            {
                return $"{nameof(DefaultTypeDescriber)} Shared Instance";
            }

            var t = GetType().GetTypeInfo();

            if (t == Types.DefaultTypeDescriber)
            {
                return $"{nameof(DefaultTypeDescriber)} Unique Instance";
            }

            return $"{nameof(DefaultTypeDescriber)} Subclass {t.Name}";
        }
    }
#pragma warning restore IDE0060
}
