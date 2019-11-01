using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Cesil
{
    internal delegate bool ColumnSetterDelegate(ReadOnlySpan<char> text, in ReadContext context, object? row);

    internal static class ColumnSetter
    {
        public static ColumnSetterDelegate CreateDynamic(string? name, int ix)
        {
            return
                (ReadOnlySpan<char> text, in ReadContext _, object? row) =>
                {
                    ((DynamicRow)row!).SetValue(ix, text);
                    return true;
                };
        }

        // create a delegate that will parse the given characters,
        //   and store them using either the given setter or
        //   the given field
        public static ColumnSetterDelegate Create(TypeInfo type, Parser parser, Setter setter, NonNull<Reset> reset)
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
                        var parserMtd = parser.Method.Value;

                        callParser = Expression.Call(parserMtd, p1, p2, l2);
                    }
                    break;
                case BackingMode.Delegate:
                    {
                        var parserDel = parser.Delegate.Value;
                        var delRef = Expression.Constant(parserDel);
                        callParser = Expression.Invoke(delRef, p1, p2, l2);
                    }
                    break;
                case BackingMode.Constructor:
                    {
                        var cons = parser.Constructor.Value;
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
                    return Throw.InvalidOperationException<ColumnSetterDelegate>($"Unexpected {nameof(BackingMode)}: {parser.Mode}");
            }

            var assignToL3 = Expression.Assign(l3, callParser);
            statements.Add(assignToL3);

            var ifNotParsedReturnFalse = Expression.IfThen(Expression.Not(l3), Expression.Return(end, Expressions.Constant_False));
            statements.Add(ifNotParsedReturnFalse);

            // call the reset method, if there is one
            if (reset.HasValue)
            {
                var r = reset.Value;

                Expression callReset;
                switch (r.Mode)
                {
                    case BackingMode.Method:
                        {
                            var resetMtd = r.Method.Value;
                            if (r.IsStatic)
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
                            var resetDel = r.Delegate.Value;
                            var delRef = Expression.Constant(resetDel);

                            if (r.IsStatic)
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
                        return Throw.InvalidOperationException<ColumnSetterDelegate>($"Unexpected {nameof(BackingMode)}: {r.Mode}");
                }

                statements.Add(callReset);
            }

            // call the setter (or set the field)

            Expression assignResult;
            switch (setter.Mode)
            {
                case BackingMode.Method:
                    {
                        var setterMtd = setter.Method.Value;

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
                        var fieldExp = Expression.Field(l1, setter.Field.Value);
                        assignResult = Expression.Assign(fieldExp, l2);
                    }
                    break;
                case BackingMode.Delegate:
                    {
                        var setterDel = setter.Delegate.Value;
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
                    return Throw.InvalidOperationException<ColumnSetterDelegate>($"Unexpected {nameof(BackingMode)}: {setter.Mode}");
            }

            statements.Add(assignResult);

            var returnTrue = Expression.Return(end, Expressions.Constant_True);
            statements.Add(returnTrue);

            statements.Add(Expression.Label(end, Expressions.Constant_False));

            var block = Expression.Block(new[] { l1, l2, l3 }, statements);

            var del = Expression.Lambda<ColumnSetterDelegate>(block, p1, p2, p3);

            var compiled = del.Compile();
            return compiled;
        }
    }
}
