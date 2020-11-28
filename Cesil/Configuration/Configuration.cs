﻿using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Cesil
{
    /// <summary>
    /// Used to combine a type and an Options into an IBoundConfiguration(T),
    /// which can create readers and writers.
    /// </summary>
    public static class Configuration
    {
        /// <summary>
        /// Create a new IBoundConfiguration(T) with Options.DynamicDefault, for use 
        ///   with dynamic types.
        /// </summary>
        public static IBoundConfiguration<dynamic> ForDynamic()
        => ForDynamic(Options.DynamicDefault);

        /// <summary>
        /// Create a new IBoundConfiguration(T) with given Options, for use 
        ///   with dynamic types.
        /// </summary>
        public static IBoundConfiguration<dynamic> ForDynamic(Options options)
        {
            Utils.CheckArgumentNull(options, nameof(options));

            if (options.ReadHeader == ReadHeader.Detect)
            {
                return Throw.ArgumentException<IBoundConfiguration<dynamic>>($"Dynamic deserialization cannot detect the presence of headers, you must specify a {nameof(ReadHeader)} of {nameof(ReadHeader.Always)} or {nameof(ReadHeader.Never)}", nameof(options));
            }

            return new DynamicBoundConfiguration(options);
        }

        /// <summary>
        /// Create a new IBoundConfiguration(TRow) with Options.Default, for
        ///   use with the given type.
        /// </summary>
        public static IBoundConfiguration<TRow> For<TRow>()
        => For<TRow>(Options.Default);

        /// <summary>
        /// Create a new IBoundConfiguration(T) with the given Options, for
        ///   use with the given type.
        /// </summary>
        public static IBoundConfiguration<TRow> For<TRow>(Options options)
        {
            Utils.CheckArgumentNull(options, nameof(options));

            var forType = typeof(TRow).GetTypeInfo();

            if (forType == Types.Object)
            {
                return Throw.InvalidOperationException<IBoundConfiguration<TRow>>($"Use {nameof(ForDynamic)} when creating configurations for dynamic types");
            }

            var deserializeMembers = options.TypeDescriber.EnumerateMembersToDeserialize(forType);
            var serializeMembers = options.TypeDescriber.EnumerateMembersToSerialize(forType);
            var provider = options.TypeDescriber.GetInstanceProvider(forType);

            ValidateTypeDescription(forType, deserializeMembers, serializeMembers, provider);

            var serializeColumns = CreateSerializeColumns(forType, options, serializeMembers);

            char? escapeStartEnd = options.EscapedValueStartAndEnd;
            var valueSep = options.ValueSeparator;


            // this is entirely knowable now, so go ahead and calculate
            //   and save for future use
            var needsEscape = DetermineNeedsEscape(serializeColumns, escapeStartEnd, valueSep);

            return
                new ConcreteBoundConfiguration<TRow>(
                    provider,
                    deserializeMembers,
                    serializeColumns,
                    needsEscape,
                    options
                );
        }

        // internal for testing purposes
        internal static bool[] DetermineNeedsEscape(Column[] serializeColumns, char? escapeStartEnd, string valueSep)
        {
            var startOfValSep = valueSep[0];

            var needsEscape = new bool[serializeColumns.Length];
            for (var i = 0; i < serializeColumns.Length; i++)
            {
                var name = serializeColumns[i].Name;
                var escape = false;
                for (var j = 0; j < name.Length; j++)
                {
                    var c = name[j];
                    if (c == '\r' || c == '\n' || c == escapeStartEnd)
                    {
                        escape = true;
                        break;
                    }

                    if (c == startOfValSep)
                    {
                        var matchesSep = true;
                        for (var k = 1; k < valueSep.Length; k++)
                        {
                            var nameCharIx = j + k;
                            if (nameCharIx >= name.Length)
                            {
                                matchesSep = false;
                                break;
                            }

                            var valSepChar = valueSep[k];
                            if (valSepChar != name[nameCharIx])
                            {
                                matchesSep = false;
                                break;
                            }
                        }

                        if (matchesSep)
                        {
                            escape = true;
                            break;
                        }
                    }
                }

                needsEscape[i] = escape;
            }

            return needsEscape;
        }

        private static void ValidateTypeDescription(TypeInfo t, IEnumerable<DeserializableMember>? deserializeColumns, IEnumerable<SerializableMember>? serializeColumns, InstanceProvider? provider)
        {
            if (serializeColumns == null)
            {
                Throw.InvalidOperationException<object>($"Registered {nameof(ITypeDescriber)} returned null for {nameof(ITypeDescriber.EnumerateMembersToSerialize)}");
                return;
            }

            if (deserializeColumns == null)
            {
                Throw.InvalidOperationException<object>($"Registered {nameof(ITypeDescriber)} returned null for {nameof(ITypeDescriber.EnumerateMembersToDeserialize)}");
                return;
            }

            if (!deserializeColumns.Any() && !serializeColumns.Any())
            {
                Throw.InvalidOperationException<object>($"No columns found to read or write for {t.FullName}");
                return;
            }

            if (provider != null)
            {
                var rowNullability = provider.ConstructsNullability;

                // deal with setter mismatches
                if (provider.ConstructorTakesParameters)
                {
                    var cons = provider.Constructor.Value;

                    foreach (var p in cons.GetParameters())
                    {
                        var found = false;
                        foreach (var d in deserializeColumns)
                        {
                            var setter = d.Setter;

                            if (setter.Mode != BackingMode.ConstructorParameter) continue;

                            if (setter.ConstructorParameter.Value == p)
                            {
                                found = true;
                                break;
                            }
                        }

                        if (!found)
                        {
                            Throw.InvalidOperationException<object>($"No {nameof(Setter)} found for constructor parameter {p}");
                            return;
                        }
                    }

                    foreach (var d in deserializeColumns)
                    {
                        var setter = d.Setter;
                        if (setter.Mode != BackingMode.ConstructorParameter) continue;

                        var cp = setter.ConstructorParameter.Value;
                        if (cp.Member != cons)
                        {
                            Throw.InvalidOperationException<object>($"{setter} is backed by a parameter not on the constructor {cons}");
                            return;
                        }

                        // no need to check for nullability, since we're using a constructor we'll never have a null row
                        //    any null handling on the part of the setter is acceptable
                    }
                }
                else
                {
                    foreach (var d in deserializeColumns)
                    {
                        var setter = d.Setter;
                        if (setter.Mode == BackingMode.ConstructorParameter)
                        {
                            Throw.InvalidOperationException<object>($"{setter} bound to constructor parameter when {nameof(InstanceProvider)} is not backed by a parameter taking constructor");
                            return;
                        }
                    }
                }
            }
            else
            {
                if (deserializeColumns.Any())
                {
                    Throw.InvalidOperationException<object>($"Registered {nameof(ITypeDescriber)} returned null for {nameof(ITypeDescriber.GetInstanceProvider)} while return non-null for {nameof(ITypeDescriber.EnumerateMembersToDeserialize)}");
                    return;
                }
            }
        }

        private static Column[] CreateSerializeColumns(TypeInfo t, Options opts, IEnumerable<SerializableMember> cols)
        {
            var ret = new List<Column>();

            foreach (var col in cols)
            {
                ColumnWriterDelegate writer;

                if (col.IsBackedByGeneratedMethod)
                {
                    writer = (ColumnWriterDelegate)System.Delegate.CreateDelegate(Types.ColumnWriterDelegate, col.GeneratedMethod.Value);
                }
                else
                {
                    writer = ColumnWriter.Create(t, opts, col.Formatter, col.ShouldSerialize, col.Getter, col.EmitDefaultValue);
                }

                ret.Add(new Column(col.Name, writer));
            }

            return ret.ToArray();
        }
    }
}
