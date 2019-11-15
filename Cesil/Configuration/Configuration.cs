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
                return Throw.ArgumentException<IBoundConfiguration<dynamic>>($"Dynamic deserialization cannot detect the presense of headers, you must specify a {nameof(ReadHeader)} of {nameof(ReadHeader.Always)} or {nameof(ReadHeader.Never)}", nameof(options));
            }

            return
                new DynamicBoundConfiguration(
                    options.TypeDescriber,
                    options.ValueSeparator,
                    options.EscapedValueStartAndEnd,
                    options.EscapedValueEscapeCharacter,
                    options.RowEnding,
                    options.ReadHeader,
                    options.WriteHeader,
                    options.WriteTrailingNewLine,
                    options.MemoryPool,
                    options.CommentCharacter,
                    options.WriteBufferSizeHint,
                    options.ReadBufferSizeHint,
                    options.DynamicRowDisposal,
                    options.WhitespaceTreatment
                );
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
                    options.ValueSeparator,
                    options.EscapedValueStartAndEnd,
                    options.EscapedValueEscapeCharacter,
                    options.RowEnding,
                    options.ReadHeader,
                    options.WriteHeader,
                    options.WriteTrailingNewLine,
                    options.MemoryPool,
                    options.CommentCharacter,
                    options.WriteBufferSizeHint,
                    options.ReadBufferSizeHint,
                    options.WhitespaceTreatment
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

            var resultType = typeof(T).GetTypeInfo();

            var retTrue = Expressions.Constant_True;
            var outVar = Expression.Parameter(resultType.MakeByRefType());

            switch (builder.Mode)
            {
                case BackingMode.Delegate:
                    {
                        var del = builder.Delegate.Value;
                        if (del is InstanceProviderDelegate<T> exactMatch)
                        {
                            return exactMatch;
                        }

                        // handle the case where we have to _bridge_ between two assignable types
                        //   we basically construct a delegate that takes an object that wraps up
                        //   the original build delegate, which we then capture and use to construct
                        //   the type to be bridged

                        var innerDelType = Types.InstanceProviderDelegateType.MakeGenericType(constructedType).GetTypeInfo();

                        var p1 = Expressions.Parameter_Object;
                        var l1 = Expression.Variable(constructedType);
                        var innerInstanceBuilderType = Expression.Convert(p1, innerDelType);

                        var endLabel = Expression.Label(Types.BoolType, "end");

                        var invokeInstanceBuilder = Expression.Invoke(innerInstanceBuilderType, l1);

                        var convertAndAssign = Expression.Assign(outVar, Expression.Convert(l1, resultType));

                        var ifTrueBlock = Expression.Block(convertAndAssign, Expression.Goto(endLabel, retTrue));

                        var assignDefault = Expression.Assign(outVar, Expression.Default(resultType));
                        var retFalse = Expressions.Constant_False;

                        var ifFalseBlock = Expression.Block(assignDefault, Expression.Goto(endLabel, retFalse));

                        var ifThenElse = Expression.IfThenElse(invokeInstanceBuilder, ifTrueBlock, ifFalseBlock);

                        var markEndLabel = Expression.Label(endLabel, Expressions.Default_Bool);

                        var delBody = Expression.Block(Types.BoolType, new[] { l1 }, ifThenElse, markEndLabel);

                        var lambda = Expression.Lambda<Types.InstanceBuildThunkDelegate<T>>(delBody, p1, outVar);
                        var compiled = lambda.Compile();

                        return (out T res) => compiled(del, out res);
                    }
                case BackingMode.Constructor:
                    {
                        var cons = builder.Constructor.Value;

                        var assignTo = Expression.Assign(outVar, Expression.New(cons));

                        var block = Expression.Block(new Expression[] { assignTo, retTrue });

                        var wrappingDel = Expression.Lambda<InstanceProviderDelegate<T>>(block, outVar);
                        var ret = wrappingDel.Compile();

                        return ret;
                    }
                case BackingMode.Method:
                    {
                        var mtd = builder.Method.Value;

                        var delType = typeof(InstanceProviderDelegate<T>);
                        var del = (InstanceProviderDelegate<T>)Delegate.CreateDelegate(delType, mtd);

                        return del;
                    }
                default:
                    return Throw.InvalidOperationException<InstanceProviderDelegate<T>>($"Unexpected {nameof(BackingMode)}: {builder.Mode}");
            }
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
