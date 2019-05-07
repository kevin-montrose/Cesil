using System.Collections.Generic;
using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;

namespace Cesil
{
    internal sealed class DynamicRowMetaObject : DynamicMetaObject
    {
        private readonly DynamicRow Row;

        internal DynamicRowMetaObject(DynamicRow outer, Expression exp) : base(exp, BindingRestrictions.Empty, outer)
        {
            Row = outer;
        }

        public override DynamicMetaObject BindGetIndex(GetIndexBinder binder, DynamicMetaObject[] indexes)
        {
            if (indexes.Length != 1)
            {
                var msg = Expression.Constant($"Only single indexers are supported, found {indexes.Length}.");
                var call = Expression.Call(Methods.Throw.InvalidOperationException, msg);

                return new DynamicMetaObject(call, BindingRestrictions.Empty);
            }

            var indexType = indexes[0].RuntimeType.GetTypeInfo();

            if (indexType == Types.IntType)
            {
                var restrictions = BindingRestrictions.GetTypeRestriction(Expression, LimitType);
                var castToRow = Expression.Convert(Expression, Types.DynamicRow);
                var callOnSelf = Expression.Call(castToRow, Methods.DynamicRow.GetIndex, indexes[0].Expression);

                return new DynamicMetaObject(callOnSelf, restrictions);
            }

            if (indexType == Types.StringType)
            {
                var restrictions = BindingRestrictions.GetTypeRestriction(Expression, LimitType);
                var castToRow = Expression.Convert(Expression, Types.DynamicRow);
                var callOnSelf = Expression.Call(castToRow, Methods.DynamicRow.GetValue, indexes[0].Expression);

                return new DynamicMetaObject(callOnSelf, restrictions);
            }

            // no binder
            {
                var msg = Expression.Constant($"Only string and int indexers are supported, found {indexType}.");
                var call = Expression.Call(Methods.Throw.InvalidOperationException, msg);

                return new DynamicMetaObject(call, BindingRestrictions.Empty);
            }
        }

        public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
        {
            var restrictions = BindingRestrictions.GetTypeRestriction(Expression, LimitType);

            var name = Expression.Constant(binder.Name);
            var castToRow = Expression.Convert(Expression, Types.DynamicRow);
            var callOnSelf = Expression.Call(castToRow, Methods.DynamicRow.GetValue, name);

            return new DynamicMetaObject(callOnSelf, restrictions);
        }

        public override DynamicMetaObject BindConvert(ConvertBinder binder)
        {
            var converterInterface = Row.Converter;
            var index = Row.RowNumber;
            var columnNames = Row.Names;
            var converter = converterInterface.GetRowConverter(index, Row.Width, columnNames, binder.ReturnType.GetTypeInfo());

            var restrictions = BindingRestrictions.GetTypeRestriction(Expression, LimitType);

            if (converter == null)
            {
                var throwMsg = Expression.Call(Methods.Throw.InvalidOperationException, Expression.Constant($"No row converter discovered for {binder.ReturnType}"));

                return new DynamicMetaObject(throwMsg, restrictions);
            }

            if (!binder.ReturnType.IsAssignableFrom(converter.TargetType))
            {
                var throwMsg = Expression.Call(Methods.Throw.InvalidOperationException, Expression.Constant($"Row converter {converter} does not create a type assignable to {binder.ReturnType}, returns {converter.TargetType}"));

                return new DynamicMetaObject(throwMsg, restrictions);
            }

            var selfAsRow = Expression.Convert(Expression, Types.DynamicRow);
            
            var cons = converter.ConstructorForObject;
            if (cons != null)
            {
                var createType = Expression.New(cons, selfAsRow);
                var cast = Expression.Convert(createType, binder.ReturnType);

                return new DynamicMetaObject(cast, restrictions);
            }

            var mtd = converter.Method;
            if (mtd != null)
            {
                var statements = new List<Expression>();

                var callGetContext = Expression.Call(selfAsRow, Methods.DynamicRow.GetReadContext);

                var outVar = Expression.Parameter(converter.TargetType);
                var resVar = Expression.Variable(Types.BoolType);

                var callConvert = Expression.Call(mtd, selfAsRow, callGetContext, outVar);
                var assignRes = Expression.Assign(resVar, callConvert);

                statements.Add(assignRes);

                var callThrow = Expression.Call(Methods.Throw.InvalidOperationException, Expression.Constant($"{nameof(DynamicRowConverter)} returned false"));

                var ifNot = Expression.IfThen(Expression.Not(resVar), callThrow);
                statements.Add(ifNot);

                var convertOut = Expression.Convert(outVar, binder.ReturnType);
                statements.Add(convertOut);

                var block = Expression.Block(new ParameterExpression[] { outVar, resVar }, statements);

                return new DynamicMetaObject(block, restrictions);
            }

            var typedCons = converter.ConstructorTakingParams;
            if (typedCons != null)
            {
                var colsForPs = converter.ColumnsForParameters;
                var paramTypes = converter.ParameterTypes;

                var ps = new List<Expression>();
                for (var pIx = 0; pIx < colsForPs.Length; pIx++)
                {
                    var colIx = colsForPs[pIx];
                    var pType = paramTypes[pIx];
                    var getter = Methods.DynamicRow.GetIndexTyped.MakeGenericMethod(pType);

                    var call = Expression.Call(selfAsRow, getter, Expression.Constant(colIx));

                    ps.Add(call);
                }

                var createType = Expression.New(typedCons, ps);
                var cast = Expression.Convert(createType, binder.ReturnType);

                return new DynamicMetaObject(cast, restrictions);
            }

            var zeroCons = converter.EmptyConstructor;
            if(zeroCons != null)
            {
                var setters = converter.Setters;
                var setterParams = converter.SetterParameters;
                var setterCols = converter.ColumnsForSetters;

                var retVar = Expression.Variable(converter.TargetType);

                var createType = Expression.New(zeroCons);
                var assignToVar = Expression.Assign(retVar, createType);

                var statements = new List<Expression>();
                statements.Add(assignToVar);

                for(var i = 0; i < setters.Length; i++)
                {
                    var setter = setters[i];
                    var setterParam = setterParams[i];
                    var setterColumn = setterCols[i];

                    var getValueMtd = Methods.DynamicRow.GetIndexTyped.MakeGenericMethod(setterParam);
                    var getValueCall = Expression.Call(selfAsRow, getValueMtd, Expression.Constant(setterColumn));
                    Expression callSetter;
                    if (setter.IsStatic)
                    {
                        callSetter = Expression.Call(setter, getValueCall);
                    }
                    else
                    {
                        callSetter = Expression.Call(retVar, setter, getValueCall);
                    }

                    statements.Add(callSetter);
                }

                var cast = Expression.Convert(retVar, binder.ReturnType);
                statements.Add(cast);

                var block = Expression.Block(new[] { retVar }, statements);

                return new DynamicMetaObject(block, restrictions);
            }

            Throw.Exception("Converter could not be turned into an expression, this shouldn't be possible");
            return null;
        }
    }
}
