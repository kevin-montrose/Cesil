using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Cesil
{
    internal delegate bool ColumnWriterDelegate(object row, in WriteContext context, IBufferWriter<char> writeTo);

    internal static class ColumnWriter
    {
        // create a delegate that will format the given value (pulled from a getter or a field) into
        //   a buffer, subject to shouldSerialize being null or returning true
        //   and return true if it was able to do so
        public static ColumnWriterDelegate Create(TypeInfo type, Formatter formatter, ShouldSerialize shouldSerialize, Getter getter, bool emitDefaultValue)
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
                        return Throw.InvalidOperationException<ColumnWriterDelegate>($"Unexpected {nameof(BackingMode)}: {shouldSerialize.Mode}");

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
                    return Throw.InvalidOperationException<ColumnWriterDelegate>($"Unexpected {nameof(BackingMode)}: {getter.Mode}");
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
                    return Throw.InvalidOperationException<ColumnWriterDelegate>($"Unexpected {nameof(BackingMode)}: {formatter.Mode}");
            }

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
