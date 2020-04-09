using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Cesil
{
    internal delegate bool ColumnWriterDelegate(object? row, in WriteContext context, IBufferWriter<char> writeTo);

    internal static class ColumnWriter
    {
        // create a delegate that will format the given value (pulled from a getter or a field) into
        //   a buffer, subject to shouldSerialize being null or returning true
        //   and return true if it was able to do so
        internal static ColumnWriterDelegate Create(TypeInfo type, Options options, Formatter formatter, NonNull<ShouldSerialize> shouldSerialize, Getter getter, bool emitDefaultValue)
        {
            var p1 = Expressions.Parameter_Object;
            var p2 = Expressions.Parameter_WriteContext_ByRef;
            var p3 = Expressions.Parameter_IBufferWriterOfChar;

            var statements = new List<Expression>();

            var p1AsType = Expression.Convert(p1, type);
            var l1 = Expression.Variable(type, "l1");

            var assignToL1 = Expression.Assign(l1, p1AsType);
            statements.Add(assignToL1);

            var end = Expression.Label(Types.Bool, "end");
            var returnTrue = Expression.Label("return-true");

            if (shouldSerialize.HasValue)
            {
                var ss = shouldSerialize.Value;

                var callShouldSerialize = ss.MakeExpression(l1, p2);

                var shouldntSerialize = Expression.Not(callShouldSerialize);
                var jumpToEnd = Expression.Goto(returnTrue);
                var jumpToEndIfShouldntSerialize = Expression.IfThen(shouldntSerialize, jumpToEnd);

                statements.Add(jumpToEndIfShouldntSerialize);
            }

            var columnType = getter.Returns;
            var l2 = Expression.Variable(columnType, "l2");

            var getExp = getter.MakeExpression(l1, p2);

            var assignToL2 = Expression.Assign(l2, getExp);
            statements.Add(assignToL2);

            if (!emitDefaultValue)
            {
                var defValue = Expression.Constant(Activator.CreateInstance(columnType));

                Expression isDefault;
                // intentionally letting GetMethod return null here
                if (!columnType.IsValueType || columnType.IsEnum || columnType.IsPrimitive || columnType.GetMethod("op_Equality") != null)
                {
                    isDefault = Expression.Equal(l2, defValue);
                }
                else
                {
                    var equatableI = Types.IEquatable.MakeGenericType(columnType).GetTypeInfo();
                    if (columnType.GetInterfaces().Any(i => i == equatableI))
                    {
                        var equals = equatableI.GetMethodNonNull(nameof(IEquatable<object>.Equals));
                        var map = columnType.GetInterfaceMap(equatableI);

                        MethodInfo? equalsTyped = null;
                        for (var j = 0; j < map.InterfaceMethods.Length; j++)
                        {
                            if (map.InterfaceMethods[j] == equals)
                            {
                                equalsTyped = map.TargetMethods[j];
                                isDefault = Expression.Call(l2, equalsTyped, defValue);
                                goto done;
                            }
                        }

                        return Throw.ImpossibleException<ColumnWriterDelegate>($"Could not find typed {nameof(IEquatable<object>.Equals)} method, which shouldn't be possible", options);
                    }
                    else
                    {
                        var eqsUntyped = columnType.GetMethodNonNull(nameof(object.Equals));
                        var defAsObject = Expression.Convert(defValue, Types.Object);
                        isDefault = Expression.Call(l2, eqsUntyped, defAsObject);
                    }
                }

done:
                var ifIsDefaultReturnTrue = Expression.IfThen(isDefault, Expression.Goto(returnTrue));

                statements.Add(ifIsDefaultReturnTrue);
            }

            var callFormatter = formatter.MakeExpression(l2, p2, p3);

            statements.Add(Expression.Goto(end, callFormatter));

            statements.Add(Expression.Label(returnTrue));
            statements.Add(Expression.Goto(end, Expressions.Constant_True));

            statements.Add(Expression.Label(end, Expressions.Constant_False));

            var block = Expression.Block(new[] { l1, l2 }, statements);

            var del = Expression.Lambda<ColumnWriterDelegate>(block, p1, p2, p3);

            var compiled = del.Compile();

            return compiled;
        }
    }
}
