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
            var callParser = parser.MakeExpression(p1, p2, l2);

            var assignToL3 = Expression.Assign(l3, callParser);
            statements.Add(assignToL3);

            var ifNotParsedReturnFalse = Expression.IfThen(Expression.Not(l3), Expression.Return(end, Expressions.Constant_False));
            statements.Add(ifNotParsedReturnFalse);

            // call the reset method, if there is one
            if (reset.HasValue)
            {
                var r = reset.Value;

                var callReset = r.MakeExpression(l1, p2);

                statements.Add(callReset);
            }

            var assignResult = setter.MakeExpression(l1, l2, p2);
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
