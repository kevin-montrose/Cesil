using System;
using System.Collections.Generic;
using System.Linq.Expressions;
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

            if (forType == Types.ObjectType)
            {
                return Throw.InvalidOperationException<IBoundConfiguration<TRow>>($"Use {nameof(ForDynamic)} when creating configurations for dynamic types");
            }

            var deserializeColumns = DiscoverDeserializeColumns(forType, options);
            var serializeColumns = DiscoverSerializeColumns(forType, options);
            var cons = DiscoverInstanceProvider<TRow>(forType, options);

            if (deserializeColumns.Length == 0 && serializeColumns.Length == 0)
            {
                return Throw.InvalidOperationException<IBoundConfiguration<TRow>>($"No columns found to read or write for {typeof(TRow).FullName}");
            }

            // this is entirely knowable now, so go ahead and calculate
            //   and save for future use
            var needsEscape = new bool[serializeColumns.Length];
            for (var i = 0; i < serializeColumns.Length; i++)
            {
                var name = serializeColumns[i].Name.Value;
                var escape = false;
                for (var j = 0; j < name.Length; j++)
                {
                    var c = name[j];
                    if (c == '\r' || c == '\n' || c == options.ValueSeparator || c == options.EscapedValueStartAndEnd)
                    {
                        escape = true;
                        break;
                    }
                }

                needsEscape[i] = escape;
            }

            return
                new ConcreteBoundConfiguration<TRow>(
                    cons,
                    deserializeColumns,
                    serializeColumns,
                    needsEscape,
                    options
                );
        }

        private static Column[] DiscoverDeserializeColumns(TypeInfo t, Options options)
        {
            var ret = new List<Column>();

            var cols = options.TypeDescriber.EnumerateMembersToDeserialize(t.GetTypeInfo());

            foreach (var col in cols)
            {
                var setter = ColumnSetter.Create(t, col.Parser, col.Setter, col.Reset);

                ret.Add(new Column(col.Name, setter, null, col.IsRequired));
            }

            return ret.ToArray();
        }

        private static InstanceProviderDelegate<T> DiscoverInstanceProvider<T>(TypeInfo forType, Options opts)
        {
            var neededType = typeof(T).GetTypeInfo();

            var builder = opts.TypeDescriber.GetInstanceProvider(forType);
            if (builder == null)
            {
                return Throw.InvalidOperationException<InstanceProviderDelegate<T>>($"No {nameof(InstanceProvider)} returned for {typeof(T)}");
            }

            var constructedType = builder.ConstructsType;
            if (!neededType.IsAssignableFrom(constructedType))
            {
                return Throw.InvalidOperationException<InstanceProviderDelegate<T>>($"Returned {nameof(InstanceProvider)} ({builder}) cannot create instances assignable to {typeof(T)}");
            }

            // fast paths
            if (!builder.HasFallbacks)
            {
                switch (builder.Mode)
                {
                    case BackingMode.Delegate:
                        {
                            var del = builder.Delegate.Value;
                            if (del is InstanceProviderDelegate<T> exactMatch)
                            {
                                return exactMatch;
                            }
                        }
                        break;
                    case BackingMode.Method:
                        {
                            var mtd = builder.Method.Value;

                            var delType = typeof(InstanceProviderDelegate<T>);
                            var del = (InstanceProviderDelegate<T>)Delegate.CreateDelegate(delType, mtd);

                            return del;
                        }
                        // intentionally non-exhaustive
                }
            }


            var resultType = typeof(T).GetTypeInfo();
            var outVar = Expression.Parameter(resultType.MakeByRefType());
            var ctxVar = Expressions.Parameter_ReadContext_ByRef;

            var exp = builder.MakeExpression(resultType, ctxVar, outVar);

            var wrappingDel = Expression.Lambda<InstanceProviderDelegate<T>>(exp, ctxVar, outVar);
            var ret = wrappingDel.Compile();

            return ret;
        }

        private static Column[] DiscoverSerializeColumns(TypeInfo t, Options options)
        {
            var ret = new List<Column>();

            var cols = options.TypeDescriber.EnumerateMembersToSerialize(t.GetTypeInfo());

            foreach (var col in cols)
            {
                var writer = ColumnWriter.Create(t, col.Formatter, col.ShouldSerialize, col.Getter, col.EmitDefaultValue);

                ret.Add(new Column(col.Name, null, writer, false));
            }

            return ret.ToArray();
        }
    }
}
