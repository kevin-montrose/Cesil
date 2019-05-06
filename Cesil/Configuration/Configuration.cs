using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Cesil
{
    // todo: support deserializing into dynamic objects?

    /// <summary>
    /// Used to combine a type and an Options into a BoundConfiguration(T),
    /// which can create readers and writers.
    /// </summary>
    public static class Configuration
    {
        /// <summary>
        /// Create a new BoundConfiguration(T) with default Options.
        /// </summary>
        public static BoundConfiguration<T> For<T>()
            where T : new()
        => For<T>(Options.Default);

        /// <summary>
        /// Create a new BoundConfiguration(T) with the given Options.
        /// </summary>
        public static BoundConfiguration<T> For<T>(Options options)
            where T : new()
        {
            if(options == null)
            {
                Throw.ArgumentNullException(nameof(options));
            }
            var forType = typeof(T).GetTypeInfo();

            var newCons = forType.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.CreateInstance, null, Type.EmptyTypes, Array.Empty<ParameterModifier>());
            if(newCons == null)
            {
                Throw.InvalidOperation($"Type {forType} does not have a default constructor");
            }

            var deserializeColumns = DiscoverDeserializeColumns(forType, options);
            var serializeColumns = DiscoverSerializeColumns(forType, options);

            if (deserializeColumns.Length == 0 && serializeColumns.Length == 0)
            {
                Throw.InvalidOperation($"No columns found to read or write for {typeof(T).FullName}");
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
                new BoundConfiguration<T>(
                    newCons,
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
            var p2 = Expression.Parameter(Types.ObjectType);

            var end = Expression.Label(Types.BoolType, "end");

            var statements = new List<Expression>();

            var p2AsType = Expression.Convert(p2, type);
            var l1 = Expression.Variable(type, "l1");

            var assignToL1 = Expression.Assign(l1, p2AsType);
            statements.Add(assignToL1);

            var outArg = parser.GetParameters()[1].ParameterType.GetElementType();
            var l2 = Expression.Parameter(outArg);

            var callParser = Expression.Call(parser, p1, l2);

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

            var del = Expression.Lambda<Column.SetterDelegate>(block, p1, p2);

            var compiled = del.Compile();
            return compiled;

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
        private static Func<object, IBufferWriter<char>, bool> MakeWriter(TypeInfo type, MethodInfo formatter, MethodInfo shouldSerialize, MethodInfo getter, FieldInfo field, bool emitDefaultValue)
        {
            var p1 = Expression.Parameter(Types.ObjectType);
            var p2 = Expression.Parameter(Types.IBufferWriterOfCharType);

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
                var defValue = Activator.CreateInstance(columnType);
                
                var isDefault = Expression.Equal(l2, Expression.Constant(defValue));
                var ifIsDefaultReturnTrue = Expression.IfThen(isDefault, Expression.Goto(returnTrue));

                statements.Add(ifIsDefaultReturnTrue);
            }

            var callFormatter = Expression.Call(formatter, l2, p2);
            statements.Add(Expression.Goto(end, callFormatter));

            statements.Add(Expression.Label(returnTrue));
            statements.Add(Expression.Goto(end, Expression.Constant(true)));

            statements.Add(Expression.Label(end, Expression.Constant(false)));

            var block = Expression.Block(new[] { l1, l2 }, statements);

            var del = Expression.Lambda<Func<object, IBufferWriter<char>, bool>>(block, p1, p2);

            var compiled = del.Compile();
            return compiled;
        }
    }
}
