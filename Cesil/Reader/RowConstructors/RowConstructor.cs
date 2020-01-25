using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;

namespace Cesil
{
    internal delegate void ParseAndSetOnDelegate<TRow>(TRow row, in ReadContext ctx, ReadOnlySpan<char> data);
    internal delegate void MoveFromHoldToRowDelegate<TRow, THold>(TRow row, THold hold, in ReadContext setterCtx);
    internal delegate TRow GetInstanceGivenHoldDelegate<TRow, THold>(THold hold);
    internal delegate void ClearHoldDelegate<THold>(THold hold);

    /// <summary>
    /// Handles gluing InstanceProviders and Setters together for constructing an object.
    /// 
    /// This is complicated by some providers being backed by a constructor that takes parameters,
    ///    which means we may need to do considerable work before we could actually get a hold of
    ///    a row instance.
    ///    
    /// For extra fun, we allow mixing different kinds of setters and instance providers so we might 
    ///    need to "hold" some values before an instance is available even if those values have simple
    ///    setters.
    ///    
    /// And then on top of that, the column order can vary per reader which means what needs to be "held"
    ///    varies even then.
    /// </summary>
    internal static class RowConstructor
    {
        private static readonly AssemblyBuilder AssemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(nameof(Cesil) + "_" + nameof(RowConstructor) + "_" + nameof(AssemblyBuilder)), AssemblyBuilderAccess.RunAndCollect);
        private static readonly ModuleBuilder ModuleBuilder = AssemblyBuilder.DefineDynamicModule(nameof(Cesil) + "_" + nameof(RowConstructor) + "_" + nameof(ModuleBuilder));

        public static IRowConstructor<TRow> Create<TRow>(
            MemoryPool<char> pool,
            InstanceProvider instanceProvider,
            IEnumerable<DeserializableMember> setters
        )
        {
            var settersWithIndex = setters.Select((s, ix) => (Index: ix, Member: s));

            var needHolding = settersWithIndex.Where(s => s.Member.Setter.Mode == BackingMode.ConstructorParameter).ToList();
            var noHolding = settersWithIndex.Where(s => s.Member.Setter.Mode != BackingMode.ConstructorParameter).ToList();

            if (!needHolding.Any())
            {
                return CreateSimpleRowConstructor<TRow>(pool, instanceProvider, setters);
            }

            foreach (var nh in needHolding)
            {
                var member = nh.Member;

                if (member.Reset.HasValue)
                {
                    return Throw.InvalidOperationException<IRowConstructor<TRow>>($"Cannot attach a {nameof(Reset)} to a member whose {nameof(Setter)} is backed by a constructor parameter; found {member.Reset} associated with {member.Setter}");
                }
            }

            return CreateNeedsHoldRowConstructor<TRow>(pool, instanceProvider, needHolding, noHolding);
        }

        private static IRowConstructor<TRow> CreateSimpleRowConstructor<TRow>(
            MemoryPool<char> pool,
            InstanceProvider instanceProvider,
            IEnumerable<DeserializableMember> settersAndParsers
        )
        {
            var rowType = typeof(TRow).GetTypeInfo();

            var instanceDel = MakeInstanceProviderDelegate(instanceProvider);

            var arr = ImmutableArray.CreateBuilder<(string Name, MemberRequired Required, ParseAndSetOnDelegate<TRow> Setter)>();
            foreach (var sp in settersAndParsers)
            {
                var name = sp.Name;
                var required = sp.IsRequired ? MemberRequired.Yes : MemberRequired.No;
                var parseAndSet = (ParseAndSetOnDelegate<TRow>)MakeParseAndSetDelegate(rowType, sp.Parser, sp.Setter, sp.Reset);


                arr.Add((name, required, parseAndSet));
            }

            return new SimpleRowConstructor<TRow>(pool, instanceDel, arr.ToImmutable());

            // bind the instance provider
            static InstanceProviderDelegate<TRow> MakeInstanceProviderDelegate(InstanceProvider instanceProvider)
            {
                var rowType = typeof(TRow).GetTypeInfo();

                var ctxParam = Expressions.Parameter_ReadContext_ByRef;
                var outParam = Expression.Parameter(rowType.MakeByRefType().GetTypeInfo());

                var body = instanceProvider.MakeExpression(rowType, ctxParam, outParam);

                var lambda = Expression.Lambda<InstanceProviderDelegate<TRow>>(body, ctxParam, outParam);
                var del = lambda.Compile();

                return del;
            }
        }

        internal static IRowConstructor<TRow> CreateNeedsHoldRowConstructor<TRow>(
            MemoryPool<char> pool,
            InstanceProvider instanceProvider,
            IEnumerable<(int Index, DeserializableMember Member)> needHoldSettersAndParsers,
            IEnumerable<(int Index, DeserializableMember Member)> simpleSettersAndParsers
        )
        {
            var rowType = typeof(TRow).GetTypeInfo();

            var holdTypeBuilder = ModuleBuilder.DefineType(nameof(Cesil) + "_Hold_" + Guid.NewGuid());
            foreach (var csp in needHoldSettersAndParsers)
            {
                holdTypeBuilder.DefineField("Hold_" + csp.Member.Name, csp.Member.Parser.Creates, FieldAttributes.Private);
            }
            foreach (var csp in simpleSettersAndParsers)
            {
                holdTypeBuilder.DefineField("Simple_" + csp.Member.Name, csp.Member.Parser.Creates, FieldAttributes.Private);
            }
            var holdType = holdTypeBuilder.CreateTypeNonNull();

            var (constructFromHold, hold) = MakeHoldSettersAndInstanceBuilder(instanceProvider, holdType, needHoldSettersAndParsers);

            var simpleBuilder = ImmutableDictionary.CreateBuilder<int, (string? Name, MemberRequired Required, Delegate ParseAndHold, Delegate MoveToRow, Delegate SetOnRow)>();

            foreach (var csp in simpleSettersAndParsers)
            {
                var member = csp.Member;

                var name = member.Name;
                var required = member.IsRequired ? MemberRequired.Yes : MemberRequired.No;

                var field = holdType.GetFieldNonNull("Simple_" + name, BindingFlags.Instance | BindingFlags.NonPublic);
                var parseAndHold = RowConstructor.MakeParseAndSetDelegate(holdType, member.Parser, Setter.ForField(field), default);
                var moveToRow = MakeMoveFromHoldToRowDelegate(rowType, holdType, member.Setter, Getter.ForField(field), member.Reset);
                var setOnRow = RowConstructor.MakeParseAndSetDelegate(rowType, member.Parser, member.Setter, member.Reset);

                simpleBuilder.Add(csp.Index, (name, required, parseAndHold, moveToRow, setOnRow));
            }
            var simple = simpleBuilder.ToImmutable();

            var clearHold = MakeClearHold(holdType);

            var constructorType = Types.NeedsHoldRowConstructorType.MakeGenericType(rowType, holdType).GetTypeInfo();
            var createMtd = constructorType.GetMethodNonNull(nameof(NeedsHoldRowConstructor<object, object>.Create));

            var ret = createMtd.Invoke(null, new object[] { pool, clearHold, constructFromHold, simple, hold });
            if (ret == null)
            {
                return Throw.Exception<IRowConstructor<TRow>>($"Couldn't build an {nameof(NeedsHoldRowConstructor<object, object>)}, shouldn't be possible");
            }

            return (IRowConstructor<TRow>)ret;

            // make a delegate that clears all the fields on the Hold type
            static Delegate MakeClearHold(TypeInfo holdType)
            {
                var statements = new List<Expression>();

                var holdParam = Expression.Parameter(holdType);

                foreach (var f in holdType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
                {
                    var clear = Expression.Assign(Expression.Field(holdParam, f), Expression.Default(f.FieldType.GetTypeInfo()));
                    statements.Add(clear);
                }

                var delType = Types.ClearHoldDelegateType.MakeGenericType(holdType);

                var block = Expression.Block(statements);

                var lambda = Expression.Lambda(delType, block, holdParam);

                var del = lambda.Compile();

                return del;
            }

            // make setters for values on the Hold type, and an delegate that will build an instance of TRow given the Hold type
            static (Delegate ConstructFromHold, ImmutableDictionary<int, (string? Name, MemberRequired Required, Delegate ParseAndHold)>) MakeHoldSettersAndInstanceBuilder(
                InstanceProvider instanceProvider,
                TypeInfo holdType,
                IEnumerable<(int Index, DeserializableMember Member)> needHoldSettersAndParsers
            )
            {
                var parseAndHoldBuilder = ImmutableDictionary.CreateBuilder<int, (string? Name, MemberRequired Required, Delegate ParseAndHold)>();

                var holdParam = Expression.Parameter(holdType);

                var cons = instanceProvider.Constructor.Value;

                var ps = cons.GetParameters();

                var access = new MemberExpression[ps.Length];

                foreach (var csp in needHoldSettersAndParsers)
                {
                    var member = csp.Member;

                    var p = member.Setter.ConstructorParameter.Value;
                    var name = member.Name;
                    var required = member.IsRequired ? MemberRequired.Yes : MemberRequired.No;

                    var field = holdType.GetFieldNonNull("Hold_" + name, BindingFlags.Instance | BindingFlags.NonPublic);
                    var parseAndHold = RowConstructor.MakeParseAndSetDelegate(holdType, csp.Member.Parser, Setter.ForField(field), default);

                    parseAndHoldBuilder.Add(csp.Index, (name, required, parseAndHold));

                    access[p.Position] = Expression.Field(holdParam, field);
                }

                var make = Expression.New(cons, access);

                var delType = Types.GetInstanceGivenHoldDelegateType.MakeGenericType(instanceProvider.ConstructsType, holdType);

                var lambda = Expression.Lambda(delType, make, holdParam);

                var compile = lambda.Compile();

                return (compile, parseAndHoldBuilder.ToImmutable());
            }

            // make a delegate to take something off of Hold and put it on TRow
            static Delegate MakeMoveFromHoldToRowDelegate(TypeInfo rowType, TypeInfo holdType, Setter setter, Getter getter, NonNull<Reset> reset)
            {
                var statements = new List<Expression>();

                var rowParam = Expression.Parameter(rowType);
                var holdParam = Expression.Parameter(holdType);
                var setterCtxParam = Expressions.Parameter_ReadContext_ByRef;

                var getterVar = Expression.Variable(getter.Returns);
                var getterExp = getter.MakeExpression(holdParam, setterCtxParam);           // technically setterCtx doesn't matter
                var assignGetterVar = Expression.Assign(getterVar, getterExp);

                statements.Add(assignGetterVar);

                if (reset.HasValue)
                {
                    var resetBody = reset.Value.MakeExpression(rowParam, setterCtxParam);
                    statements.Add(resetBody);
                }

                var setterExp = setter.MakeExpression(rowParam, getterVar, setterCtxParam); // this setterCtx does matter though
                statements.Add(setterExp);

                var body = Expression.Block(new[] { getterVar }, statements);

                var delType = Types.MoveFromHoldToRowDelegateType.MakeGenericType(rowType, holdType);

                var lambda = Expression.Lambda(delType, body, rowParam, holdParam, setterCtxParam);

                var del = lambda.Compile();

                return del;
            }
        }

        private static Delegate MakeParseAndSetDelegate(TypeInfo rowType, Parser parser, Setter setter, NonNull<Reset> reset)
        {
            var statements = new List<Expression>();

            var rowParam = Expression.Parameter(rowType);
            var ctxParam = Expressions.Parameter_ReadContext_ByRef;
            var dataSpanParam = Expressions.Variable_ReadOnlySpanOfChar;

            var parsedType = parser.Creates;

            var outDestVar = Expression.Variable(parsedType);

            var resVar = Expressions.Variable_Bool;

            var parseBody = parser.MakeExpression(dataSpanParam, ctxParam, outDestVar);
            var assignParseRes = Expression.Assign(resVar, parseBody);

            statements.Add(assignParseRes);

            var parserConst = Expression.Constant(parser);

            var throwNoParse = Expression.Call(Methods.Throw.ParseFailed, parserConst, ctxParam, dataSpanParam);
            var throwIfFailedParse = Expression.IfThen(Expression.Not(resVar), throwNoParse);

            statements.Add(throwIfFailedParse);

            if (reset.HasValue)
            {
                var resetBody = reset.Value.MakeExpression(rowParam, ctxParam);
                statements.Add(resetBody);
            }

            var setterBody = setter.MakeExpression(rowParam, outDestVar, ctxParam);

            statements.Add(setterBody);

            var body = Expression.Block(new[] { resVar, outDestVar }, statements);

            var delType = Types.ParseAndSetOnDelegateType.MakeGenericType(rowType);

            var lambda = Expression.Lambda(delType, body, rowParam, ctxParam, dataSpanParam);

            var del = lambda.Compile();

            return del;
        }
    }
}
