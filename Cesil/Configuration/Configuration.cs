using System;
using System.Collections.Generic;
using System.Linq;
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
            if (options == null)
            {
                return Throw.ArgumentNullException<IBoundConfiguration<dynamic>>(nameof(options));
            }

            if (options.ReadHeader == ReadHeaders.Detect)
            {
                return Throw.ArgumentException<IBoundConfiguration<dynamic>>($"Dynamic deserialization cannot detect the presense of headers, you must specify a {nameof(ReadHeaders)} of {nameof(ReadHeaders.Always)} or {nameof(ReadHeaders.Never)}", nameof(options));
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
                    options.DynamicRowDisposal
                );
        }

        /// <summary>
        /// Create a new IBoundConfiguration(T) with Options.Default, for
        ///   use with the given type.
        /// </summary>
        public static IBoundConfiguration<T> For<T>()
        => For<T>(Options.Default);

        /// <summary>
        /// Create a new IBoundConfiguration(T) with the given Options, for
        ///   use with the given type.
        /// </summary>
        public static IBoundConfiguration<T> For<T>(Options options)
        {
            if (options == null)
            {
                return Throw.ArgumentNullException<IBoundConfiguration<T>>(nameof(options));
            }

            var forType = typeof(T).GetTypeInfo();

            if (forType == Types.ObjectType)
            {
                return Throw.InvalidOperationException<IBoundConfiguration<T>>($"Use {nameof(ForDynamic)} when creating configurations for dynamic types");
            }

            var deserializeColumns = DiscoverDeserializeColumns(forType, options);
            var serializeColumns = DiscoverSerializeColumns(forType, options);
            var cons = DiscoverInstanceBuilder<T>(forType, options);

            if (deserializeColumns.Length == 0 && serializeColumns.Length == 0)
            {
                return Throw.InvalidOperationException<IBoundConfiguration<T>>($"No columns found to read or write for {typeof(T).FullName}");
            }

            // this is entirely knowable now, so go ahead and calculate
            //   and save for future use
            var needsEscape = new bool[serializeColumns.Length];
            for (var i = 0; i < serializeColumns.Length; i++)
            {
                var name = serializeColumns[i].Name;
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
                new ConcreteBoundConfiguration<T>(
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
                    options.ReadBufferSizeHint
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

        private static InstanceBuilderDelegate<T> DiscoverInstanceBuilder<T>(TypeInfo forType, Options opts)
        {
            var neededType = typeof(T).GetTypeInfo();

            var builder = opts.TypeDescriber.GetInstanceBuilder(forType);

            var constructedType = builder.ConstructsType;
            if (!neededType.IsAssignableFrom(constructedType))
            {
                return Throw.InvalidOperationException<InstanceBuilderDelegate<T>>($"Returned {nameof(InstanceBuilder)} ({builder}) cannot create instances assignable to {typeof(T)}");
            }

            var resultType = typeof(T).GetTypeInfo();

            var retTrue = Expressions.Constant_True;
            var outVar = Expression.Parameter(resultType.MakeByRefType());

            switch (builder.Mode)
            {
                case BackingMode.Delegate:
                    {
                        var del = builder.Delegate;
                        if (del is InstanceBuilderDelegate<T> exactMatch)
                        {
                            return exactMatch;
                        }

                        // handle the case where we have to _bridge_ between two assignable types
                        //   we basically construct a delegate that takes an object that wraps up
                        //   the original build delegate, which we then capture and use to construct
                        //   the type to be bridged

                        var innerDelType = Types.InstanceBuilderDelegateType.MakeGenericType(constructedType).GetTypeInfo();

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
                        var cons = builder.Constructor;

                        var assignTo = Expression.Assign(outVar, Expression.New(cons));

                        var block = Expression.Block(new Expression[] { assignTo, retTrue });

                        var wrappingDel = Expression.Lambda<InstanceBuilderDelegate<T>>(block, outVar);
                        var ret = wrappingDel.Compile();

                        return ret;
                    }
                case BackingMode.Method:
                    {
                        var mtd = builder.Method;

                        var delType = typeof(InstanceBuilderDelegate<T>);
                        var del = (InstanceBuilderDelegate<T>)Delegate.CreateDelegate(delType, mtd);

                        return del;
                    }
                default:
                    return Throw.InvalidOperationException<InstanceBuilderDelegate<T>>($"Unexpected {nameof(BackingMode)}: {builder.Mode}");
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
