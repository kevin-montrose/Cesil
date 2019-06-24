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
                Throw.ArgumentNullException(nameof(options));
            }

            if (options.ReadHeader == ReadHeaders.Detect)
            {
                Throw.ArgumentException($"Dynamic deserialization cannot detect the presense of headers, you must specify a {nameof(ReadHeaders)} of {nameof(ReadHeaders.Always)} or {nameof(ReadHeaders.Never)}", nameof(options));
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
                var setter = MakeSetter(t, col.Parser, col.Setter, col.Reset);

                ret.Add(new Column(col.Name, setter, null, col.IsRequired));
            }

            return ret.ToArray();
        }

        // create a delegate that will parse the given characters,
        //   and store them using either the given setter or
        //   the given field
        private static Column.SetterDelegate MakeSetter(TypeInfo type, Parser parser, Setter setter, Reset reset)
        {
            var p1 = Expressions.Parameter_ReadOnlySpanOfChar;
            var p2 = Expressions.Parameter_ReadContext_ByRef;
            var p3 = Expressions.Parameter_Object;

            var end = Expression.Label(Types.BoolType, "end");

            var statements = new List<Expression>();

            var p3AsType = Expression.Convert(p3, type);
            var l1 = Expression.Variable(type, "l1");

            var assignToL1 = Expression.Assign(l1, p3AsType);
            statements.Add(assignToL1);

            var outArg = parser.Creates;
            var l2 = Expression.Parameter(outArg);
            var l3 = Expressions.Variable_Bool;

            // call the parser and set l3 = success, and l2 = value
            Expression callParser;
            switch (parser.Mode)
            {
                case BackingMode.Method:
                    {
                        var parserMtd = parser.Method;

                        callParser = Expression.Call(parserMtd, p1, p2, l2);
                    }
                    break;
                case BackingMode.Delegate:
                    {
                        var parserDel = parser.Delegate;
                        var delRef = Expression.Constant(parserDel);
                        callParser = Expression.Invoke(delRef, p1, p2, l2);
                    }
                    break;
                case BackingMode.Constructor:
                    {
                        var cons = parser.Constructor;
                        var psCount = cons.GetParameters().Length;
                        NewExpression callCons;

                        if (psCount == 1)
                        {
                            callCons = Expression.New(cons, p1);
                        }
                        else
                        {
                            callCons = Expression.New(cons, p1, p2);
                        }

                        var assignToL2 = Expression.Assign(l2, callCons);

                        callParser = Expression.Block(assignToL2, Expressions.Constant_True);
                    }
                    break;
                default:
                    Throw.InvalidOperationException($"Unexpected {nameof(BackingMode)}: {parser.Mode}");
                    return default;
            }

            var assignToL3 = Expression.Assign(l3, callParser);
            statements.Add(assignToL3);

            var ifNotParsedReturnFalse = Expression.IfThen(Expression.Not(l3), Expression.Return(end, Expressions.Constant_False));
            statements.Add(ifNotParsedReturnFalse);

            // call the reset method, if there is one
            if (reset != null)
            {
                Expression callReset;
                switch (reset.Mode)
                {
                    case BackingMode.Method:
                        {
                            var resetMtd = reset.Method;
                            if (reset.IsStatic)
                            {
                                var resetPs = resetMtd.GetParameters();
                                if (resetPs.Length == 1)
                                {
                                    callReset = Expression.Call(resetMtd, l1);
                                }
                                else
                                {
                                    callReset = Expression.Call(resetMtd);
                                }
                            }
                            else
                            {
                                callReset = Expression.Call(l1, resetMtd);
                            }
                        }
                        break;
                    case BackingMode.Delegate:
                        {
                            var resetDel = reset.Delegate;
                            var delRef = Expression.Constant(resetDel);

                            if (reset.IsStatic)
                            {
                                callReset = Expression.Invoke(delRef);
                            }
                            else
                            {
                                callReset = Expression.Invoke(delRef, l1);
                            }
                        }
                        break;
                    default:
                        Throw.InvalidOperationException($"Unexpected {nameof(BackingMode)}: {reset.Mode}");
                        return default;
                }

                statements.Add(callReset);
            }

            // call the setter (or set the field)

            Expression assignResult;
            switch (setter.Mode)
            {
                case BackingMode.Method:
                    {
                        var setterMtd = setter.Method;

                        if (setter.IsStatic)
                        {
                            assignResult = Expression.Call(setterMtd, l2);
                        }
                        else
                        {
                            assignResult = Expression.Call(l1, setterMtd, l2);
                        }
                    }
                    break;
                case BackingMode.Field:
                    {
                        var fieldExp = Expression.Field(l1, setter.Field);
                        assignResult = Expression.Assign(fieldExp, l2);
                    }
                    break;
                case BackingMode.Delegate:
                    {
                        var setterDel = setter.Delegate;
                        var delRef = Expression.Constant(setterDel);

                        if (setter.IsStatic)
                        {
                            assignResult = Expression.Invoke(delRef, l2);
                        }
                        else
                        {
                            assignResult = Expression.Invoke(delRef, l1, l2);
                        }
                    }
                    break;
                default:
                    Throw.InvalidOperationException($"Unexpected {nameof(BackingMode)}: {setter.Mode}");
                    // just for control flow
                    return default;
            }

            statements.Add(assignResult);

            var returnTrue = Expression.Return(end, Expressions.Constant_True);
            statements.Add(returnTrue);

            statements.Add(Expression.Label(end, Expressions.Constant_False));

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
                    Throw.InvalidOperationException($"Unexpected {nameof(BackingMode)}: {builder.Mode}");
                    // for control flow
                    return default;
            }
        }

        private static Column[] DiscoverSerializeColumns(TypeInfo t, Options options)
        {
            var ret = new List<Column>();

            var cols = options.TypeDescriber.EnumerateMembersToSerialize(t.GetTypeInfo());

            foreach (var col in cols)
            {
                var writer = MakeWriter(t, col.Formatter, col.ShouldSerialize, col.Getter, col.EmitDefaultValue);

                ret.Add(new Column(col.Name, null, writer, false));
            }

            return ret.ToArray();
        }

        // create a delegate that will format the given value (pulled from a getter or a field) into
        //   a buffer, subject to shouldSerialize being null or returning true
        //   and return true if it was able to do so
        private static Column.WriterDelegate MakeWriter(TypeInfo type, Formatter formatter, ShouldSerialize shouldSerialize, Getter getter, bool emitDefaultValue)
        {
            var p1 = Expressions.Parameter_Object;
            var p2 = Expressions.Parameter_WriteContext_ByRef;
            var p3 = Expressions.Parameter_IBufferWriterOfChar;

            var statements = new List<Expression>();

            var p1AsType = Expression.Convert(p1, type);
            var l1 = Expression.Variable(type, "l1");

            var assignToL1 = Expression.Assign(l1, p1AsType);
            statements.Add(assignToL1);

            var end = Expression.Label(Types.BoolType, "end");
            var returnTrue = Expression.Label("return-true");

            if (shouldSerialize != null)
            {
                Expression callShouldSerialize;
                switch (shouldSerialize.Mode)
                {
                    case BackingMode.Method:
                        {
                            if (shouldSerialize.IsStatic)
                            {
                                var mtd = shouldSerialize.Method;
                                callShouldSerialize = Expression.Call(mtd);
                            }
                            else
                            {
                                var mtd = shouldSerialize.Method;
                                callShouldSerialize = Expression.Call(l1, mtd);
                            }
                        }
                        break;
                    case BackingMode.Delegate:
                        {
                            var shouldSerializeDel = shouldSerialize.Delegate;
                            var delRef = Expression.Constant(shouldSerializeDel);

                            if (shouldSerialize.IsStatic)
                            {
                                callShouldSerialize = Expression.Invoke(delRef);
                            }
                            else
                            {
                                callShouldSerialize = Expression.Invoke(delRef, l1);
                            }
                        }
                        break;
                    default:
                        Throw.InvalidOperationException($"Unexpected {nameof(BackingMode)}: {shouldSerialize.Mode}");
                        // just for control flow
                        return default;

                }

                var shouldntSerialize = Expression.Not(callShouldSerialize);
                var jumpToEnd = Expression.Goto(returnTrue);
                var jumpToEndIfShouldntSerialize = Expression.IfThen(shouldntSerialize, jumpToEnd);

                statements.Add(jumpToEndIfShouldntSerialize);
            }

            var columnType = getter.Returns;
            var l2 = Expression.Variable(columnType, "l2");

            Expression getExp;
            switch (getter.Mode)
            {
                case BackingMode.Method:
                    {
                        var mtd = getter.Method;
                        if (mtd.IsStatic)
                        {
                            if (mtd.GetParameters().Length == 0)
                            {
                                getExp = Expression.Call(mtd);
                            }
                            else
                            {
                                getExp = Expression.Call(mtd, l1);
                            }
                        }
                        else
                        {
                            getExp = Expression.Call(l1, mtd);
                        }
                    };
                    break;
                case BackingMode.Field:
                    {
                        var field = getter.Field;
                        if (field.IsStatic)
                        {
                            getExp = Expression.Field(null, field);
                        }
                        else
                        {
                            getExp = Expression.Field(l1, field);
                        }
                    }
                    break;
                case BackingMode.Delegate:
                    {
                        var getterDel = getter.Delegate;
                        var delRef = Expression.Constant(getterDel);

                        if (getter.IsStatic)
                        {
                            getExp = Expression.Invoke(delRef);
                        }
                        else
                        {
                            getExp = Expression.Invoke(delRef, l1);
                        }
                    }
                    break;
                default:
                    Throw.InvalidOperationException($"Unexpected {nameof(BackingMode)}: {getter.Mode}");
                    // just for control flow
                    return default;
            }
            var assignToL2 = Expression.Assign(l2, getExp);
            statements.Add(assignToL2);

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
                        for (var j = 0; j < map.InterfaceMethods.Length; j++)
                        {
                            if (map.InterfaceMethods[j] == equals)
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

            Expression callFormatter;
            switch (formatter.Mode)
            {
                case BackingMode.Method:
                    {
                        callFormatter = Expression.Call(formatter.Method, l2, p2, p3);
                    }
                    break;
                case BackingMode.Delegate:
                    {
                        var formatterDel = formatter.Delegate;
                        var delRef = Expression.Constant(formatterDel);
                        callFormatter = Expression.Invoke(delRef, l2, p2, p3);
                    }
                    break;
                default:
                    Throw.InvalidOperationException($"Unexpected {nameof(BackingMode)}: {formatter.Mode}");
                    // just for control flow
                    return default;
            }

            statements.Add(Expression.Goto(end, callFormatter));

            statements.Add(Expression.Label(returnTrue));
            statements.Add(Expression.Goto(end, Expressions.Constant_True));

            statements.Add(Expression.Label(end, Expressions.Constant_False));

            var block = Expression.Block(new[] { l1, l2 }, statements);

            var del = Expression.Lambda<Column.WriterDelegate>(block, p1, p2, p3);

            var compiled = del.Compile();

            return compiled;
        }
    }
}
