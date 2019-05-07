using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Cesil
{
    /// <summary>
    /// Used to combine a type and an Options into a BoundConfiguration(T),
    /// which can create readers and writers.
    /// </summary>
    public static class Configuration
    {
        /// <summary>
        /// Create a new IBoundConfiguration(T) with default Options, for use 
        ///   with dynamic types.
        /// </summary>
        public static IBoundConfiguration<dynamic> ForDynamic()
        => ForDynamic(Options.Default);

        /// <summary>
        /// Create a new IBoundConfiguration(T) with given Options, for use 
        ///   with dynamic types.
        /// </summary>
        public static IBoundConfiguration<dynamic> ForDynamic(Options options)
        {
            if (options == null)
            {
                Throw.ArgumentNullException(nameof(options));
            }

            if (options.ReadHeader == ReadHeaders.Detect)
            {
                Throw.ArgumentException($"Dynamic deserialization cannot detect the presense of headers, you must specify {nameof(ReadHeaders.Always)} or {nameof(ReadHeaders.Never)}", nameof(options));
            }

            return
                new DynamicBoundConfiguration(
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
                    options.DynamicTypeConverter,
                    options.DynamicRowDisposal
                );
        }

        /// <summary>
        /// Create a new IBoundConfiguration(T) with default Options, for
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
            if(options == null)
            {
                Throw.ArgumentNullException(nameof(options));
            }

            var forType = typeof(T).GetTypeInfo();

            if (forType == Types.ObjectType)
            {
                Throw.InvalidOperationException($"Use {nameof(ForDynamic)} when creating configurations for dynamic types");
            }

            var deserializeColumns = DiscoverDeserializeColumns(forType, options);
            var serializeColumns = DiscoverSerializeColumns(forType, options);
            var cons = DiscoverInstanceBuilder<T>(forType, options);

            if (deserializeColumns.Length == 0 && serializeColumns.Length == 0)
            {
                Throw.InvalidOperationException($"No columns found to read or write for {typeof(T).FullName}");
            }

            // this is entirely knowable now, so go ahead and calculate
            //   and save for future use
            var needsEscape = new bool[serializeColumns.Length];
            for(var i = 0; i < serializeColumns.Length; i++)
            {
                var name = serializeColumns[i].Name;
                var escape = false;
                for(var j = 0; j < name.Length; j++)
                {
                    var c = name[j];
                    if(c == '\r' || c == '\n' || c == options.ValueSeparator || c == options.EscapedValueStartAndEnd)
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
                var setter = MakeSetter(t, col.Parser, col.Setter, col.Field, col.Reset);

                ret.Add(new Column(col.Name, setter, null, col.IsRequired));
            }

            return ret.ToArray();
        }

        // create a delegate that will parse the given characters,
        //   and store them using either the given setter or
        //   the given field
        private static Column.SetterDelegate MakeSetter(TypeInfo type, MethodInfo parser, MethodInfo setter, FieldInfo field, MethodInfo reset)
        {
            var p1 = Expression.Parameter(Types.ReadOnlySpanOfCharType);
            var p2 = Expression.Parameter(Types.ReadContextType.MakeByRefType());
            var p3 = Expression.Parameter(Types.ObjectType);

            var end = Expression.Label(Types.BoolType, "end");

            var statements = new List<Expression>();

            var p3AsType = Expression.Convert(p3, type);
            var l1 = Expression.Variable(type, "l1");

            var assignToL1 = Expression.Assign(l1, p3AsType);
            statements.Add(assignToL1);

            var outArg = parser.GetParameters()[2].ParameterType.GetElementType();
            var l2 = Expression.Parameter(outArg);

            var callParser = Expression.Call(parser, p1, p2, l2);

            var l3 = Expression.Variable(Types.BoolType, "l3");
            var assignToL3 = Expression.Assign(l3, callParser);

            statements.Add(assignToL3);

            var ifNotParsedReturnFalse = Expression.IfThen(Expression.Not(l3), Expression.Return(end, Expression.Constant(false)));
            statements.Add(ifNotParsedReturnFalse);

            if(reset != null)
            {
                MethodCallExpression callReset;
                if (reset.IsStatic)
                {
                    var resetPs = reset.GetParameters();
                    if(resetPs.Length == 1)
                    {
                        callReset = Expression.Call(reset, l1);
                    }
                    else
                    {
                        callReset = Expression.Call(reset);
                    }
                }
                else
                {
                    callReset = Expression.Call(l1, reset);
                }

                statements.Add(callReset);
            }

            if(setter != null)
            {
                MethodCallExpression assignToSetter;

                if (setter.IsStatic)
                {
                    assignToSetter = Expression.Call(setter, l2);
                }
                else
                {
                    assignToSetter = Expression.Call(l1, setter, l2);
                }

                statements.Add(assignToSetter);
            }
            else
            {
                var fieldExp = Expression.Field(l1, field);
                var assignToField = Expression.Assign(fieldExp, l2);

                statements.Add(assignToField);
            }

            var returnTrue = Expression.Return(end, Expression.Constant(true));
            statements.Add(returnTrue);

            statements.Add(Expression.Label(end, Expression.Constant(false)));

            var block = Expression.Block(new[] { l1, l2, l3 }, statements);

            var del = Expression.Lambda<Column.SetterDelegate>(block, p1, p2, p3);

            var compiled = del.Compile();
            return compiled;
        }

        private static InstanceBuilderDelegate<T> DiscoverInstanceBuilder<T>(TypeInfo forType, Options opts)
        {
            var neededType = typeof(T).GetTypeInfo();

            var builder = opts.TypeDescriber.GetInstanceBuilder(forType);

            var constructedType = builder.ConstructsType;
            if (!neededType.IsAssignableFrom(constructedType))
            {
                Throw.InvalidOperationException($"Returned {nameof(InstanceBuilder)} ({builder}) cannot create instances assignable to {typeof(T)}");
            }

            var resultType = typeof(T).GetTypeInfo();

            var retTrue = Expression.Constant(true);
            var outVar = Expression.Parameter(resultType.MakeByRefType());

            var del = builder.Delegate;
            if (del != null)
            {
                if (del is InstanceBuilderDelegate<T> exactMatch)
                {
                    return exactMatch;
                }

                // handle the case where we have to _bridge_ between two assignable types
                //   we basically construct a delegate that takes an object that wraps up
                //   the original build delegate, which we then capture and use to construct
                //   the type to be bridged

                var innerDelType = Types.InstanceBuilderDelegateType.MakeGenericType(constructedType).GetTypeInfo();

                var p1 = Expression.Parameter(Types.ObjectType);
                var l1 = Expression.Variable(constructedType);
                var innerInstanceBuilderType = Expression.Convert(p1, innerDelType);

                var endLabel = Expression.Label(Types.BoolType, "end");

                var invokeInstanceBuilder = Expression.Invoke(innerInstanceBuilderType, l1);

                var convertAndAssign = Expression.Assign(outVar, Expression.Convert(l1, resultType));

                var ifTrueBlock = Expression.Block(convertAndAssign, Expression.Goto(endLabel, retTrue));

                var assignDefault = Expression.Assign(outVar, Expression.Default(resultType));
                var retFalse = Expression.Constant(false);

                var ifFalseBlock = Expression.Block(assignDefault, Expression.Goto(endLabel, retFalse));

                var ifThenElse = Expression.IfThenElse(invokeInstanceBuilder, ifTrueBlock, ifFalseBlock);

                var markEndLabel = Expression.Label(endLabel, Expression.Default(Types.BoolType));

                var delBody = Expression.Block(Types.BoolType, new[] { l1 }, ifThenElse, markEndLabel);

                var lambda = Expression.Lambda<Types.InstanceBuildThunkDelegate<T>>(delBody, p1, outVar);
                var compiled = lambda.Compile();

                return (out T res) => compiled(del, out res);
            }

            var cons = builder.Constructor;

            var assignTo = Expression.Assign(outVar, Expression.New(cons));

            var block = Expression.Block(new Expression[] { assignTo, retTrue });

            var wrappingDel = Expression.Lambda<InstanceBuilderDelegate<T>>(block, outVar);
            var ret = wrappingDel.Compile();

            return ret;
        }

        private static Column[] DiscoverSerializeColumns(TypeInfo t, Options options)
        {
            var ret = new List<Column>();

            var cols = options.TypeDescriber.EnumerateMembersToSerialize(t.GetTypeInfo());

            foreach (var col in cols)
            {
                var writer = MakeWriter(t, col.Formatter, col.ShouldSerialize, col.Getter, col.Field, col.EmitDefaultValue);

                ret.Add(new Column(col.Name, null, writer, false));
            }

            return ret.ToArray();
        }

        // create a delegate that will format the given value (pulled from a getter or a field) into
        //   a buffer, subject to shouldSerialize being null or returning true
        //   and return true if it was able to do so
        private static Column.WriterDelegate MakeWriter(TypeInfo type, MethodInfo formatter, MethodInfo shouldSerialize, MethodInfo getter, FieldInfo field, bool emitDefaultValue)
        {
            var p1 = Expression.Parameter(Types.ObjectType);
            var p2 = Expression.Parameter(Types.WriteContext.MakeByRefType());
            var p3 = Expression.Parameter(Types.IBufferWriterOfCharType);

            var statements = new List<Expression>();

            var p1AsType = Expression.Convert(p1, type);
            var l1 = Expression.Variable(type, "l1");

            var assignToL1 = Expression.Assign(l1, p1AsType);
            statements.Add(assignToL1);

            var end = Expression.Label(Types.BoolType, "end");
            var returnTrue = Expression.Label("return-true");

            if (shouldSerialize != null)
            {
                MethodCallExpression callShouldSerialize;
                if (shouldSerialize.IsStatic)
                {
                    callShouldSerialize = Expression.Call(shouldSerialize);
                }
                else
                {
                    callShouldSerialize = Expression.Call(l1, shouldSerialize);
                }

                var shouldntSerialize = Expression.Not(callShouldSerialize);
                var jumpToEnd = Expression.Goto(returnTrue);
                var jumpToEndIfShouldntSerialize = Expression.IfThen(shouldntSerialize, jumpToEnd);

                statements.Add(jumpToEndIfShouldntSerialize);
            }

            var columnType = (getter?.ReturnType ?? field.FieldType).GetTypeInfo();
            var l2 = Expression.Variable(columnType, "l2");
            
            if(getter != null)
            {
                MethodCallExpression callGetter;
                if (getter.IsStatic)
                {
                    if (getter.GetParameters().Length == 0)
                    {
                        callGetter = Expression.Call(getter);
                    }
                    else
                    {
                        callGetter = Expression.Call(getter, l1);
                    }
                }
                else
                {
                    callGetter = Expression.Call(l1, getter);
                }

                var assignToL2 = Expression.Assign(l2, callGetter);
                statements.Add(assignToL2);
            }
            else
            {
                var assignToL2 = Expression.Assign(l2, Expression.Field(l1, field));
                statements.Add(assignToL2);
            }

            if (!emitDefaultValue)
            {
                var defValue = Expression.Constant(Activator.CreateInstance(columnType));

                Expression isDefault;
                if (!columnType.IsValueType || columnType.IsEnum || columnType.IsPrimitive || columnType.GetMethod("op_Equality") != null)
                {
                    isDefault = Expression.Equal(l2, defValue);
                }
                else
                {
                    var equatableI = Types.IEquatableType.MakeGenericType(columnType);
                    if (columnType.GetInterfaces().Any(i => i == equatableI))
                    {
                        var equals = equatableI.GetMethod(nameof(IEquatable<object>.Equals));
                        var map = columnType.GetInterfaceMap(equatableI);

                        MethodInfo equalsTyped = null;
                        for(var j = 0; j < map.InterfaceMethods.Length; j++)
                        {
                            if(map.InterfaceMethods[j] == equals)
                            {
                                equalsTyped = map.TargetMethods[j];
                                break;
                            }
                        }

                        isDefault = Expression.Call(l2, equalsTyped, defValue);
                    }
                    else
                    {
                        var eqsUntyped = columnType.GetMethod(nameof(object.Equals));
                        var defAsObject = Expression.Convert(defValue, Types.ObjectType);
                        isDefault = Expression.Call(l2, eqsUntyped, defAsObject);
                    }
                }

                var ifIsDefaultReturnTrue = Expression.IfThen(isDefault, Expression.Goto(returnTrue));

                statements.Add(ifIsDefaultReturnTrue);
            }

            var callFormatter = Expression.Call(formatter, l2, p2, p3);
            statements.Add(Expression.Goto(end, callFormatter));

            statements.Add(Expression.Label(returnTrue));
            statements.Add(Expression.Goto(end, Expression.Constant(true)));

            statements.Add(Expression.Label(end, Expression.Constant(false)));

            var block = Expression.Block(new[] { l1, l2 }, statements);

            var del = Expression.Lambda<Column.WriterDelegate>(block, p1, p2, p3);

            var compiled = del.Compile();
            return compiled;
        }
    }
}
