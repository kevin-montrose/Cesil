using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq.Expressions;
using System.Reflection;

using static Cesil.BindingFlagsConstants;

namespace Cesil
{
    /// <summary>
    /// Delegate type for parsers.
    /// </summary>
    public delegate bool ParserDelegate<TOutput>(ReadOnlySpan<char> data, in ReadContext context, out TOutput result);

    /// <summary>
    /// Represents code used to parse values into concrete types.
    /// 
    /// Wraps a static method, a constructor taking a single
    ///   ReadOnlySpan(char), a constuctor taking a ReadOnlySpan(char)
    ///   and a ReadContext, or a delegate.
    /// </summary>
    public sealed class Parser :
        IElseSupporting<Parser>,
        IEquatable<Parser>,
        ICreatesCacheableDelegate<Parser.DynamicParserDelegate>
    {
        internal delegate bool DynamicParserDelegate(ReadOnlySpan<char> data, in ReadContext context, out object result);

        // internal for testing purposes
        internal static readonly IReadOnlyDictionary<TypeInfo, Parser> TypeParsers = CreateTypeParsers();

        private static IReadOnlyDictionary<TypeInfo, Parser> CreateTypeParsers()
        {
            var ret = new Dictionary<TypeInfo, Parser>();
            foreach (var mtd in Types.DefaultTypeParsers.GetMethods(InternalStatic))
            {
                var thirdArg = mtd.GetParameters()[2];
                var forType = thirdArg.ParameterType.GetTypeInfo().GetElementTypeNonNull();

                var parser = ForMethod(mtd);

                ret.Add(forType, parser);
            }

            return ret;
        }

        internal BackingMode Mode
        {
            get
            {
                if (Method.HasValue) return BackingMode.Method;
                if (Delegate.HasValue) return BackingMode.Delegate;
                if (Constructor.HasValue) return BackingMode.Constructor;

                return BackingMode.None;
            }
        }

        internal readonly NonNull<MethodInfo> Method;

        internal readonly NonNull<Delegate> Delegate;

        internal readonly NonNull<ConstructorInfo> Constructor;

        internal readonly TypeInfo Creates;
        internal readonly NullHandling CreatesNullability;

        private readonly ImmutableArray<Parser> _Fallbacks;
        ImmutableArray<Parser> IElseSupporting<Parser>.Fallbacks => _Fallbacks;

        DynamicParserDelegate? ICreatesCacheableDelegate<DynamicParserDelegate>.CachedDelegate { get; set; }

        private Parser(MethodInfo method, TypeInfo creates, ImmutableArray<Parser> fallbacks, NullHandling createsNullability)
        {
            Method.Value = method;
            Delegate.Clear();
            Constructor.Clear();
            Creates = creates;
            CreatesNullability = createsNullability;
            _Fallbacks = fallbacks;
        }

        private Parser(Delegate del, TypeInfo creates, ImmutableArray<Parser> fallbacks, NullHandling createsNullability)
        {
            Delegate.Value = del;
            Method.Clear();
            Constructor.Clear();
            Creates = creates;
            CreatesNullability = createsNullability;
            _Fallbacks = fallbacks;
        }

        private Parser(ConstructorInfo cons, ImmutableArray<Parser> fallbacks)
        {
            Delegate.Clear();
            Method.Clear();
            Constructor.Value = cons;
            Creates = cons.DeclaringTypeNonNull();
            CreatesNullability = NullHandling.ForbidNull;
            _Fallbacks = fallbacks;
        }

        DynamicParserDelegate ICreatesCacheableDelegate<DynamicParserDelegate>.CreateDelegate()
        {
            var spanVar = Expressions.Parameter_ReadOnlySpanOfChar;
            var ctxVar = Expressions.Parameter_ReadContext_ByRef;
            var outObjVar = Expressions.Parameter_Object_ByRef;
            var outCreatesVar = Expression.Variable(Creates);
            var resVar = Expressions.Variable_Bool;

            var exp = MakeExpression(spanVar, ctxVar, outCreatesVar);
            var assignToRes = Expression.Assign(resVar, exp);

            var boxCreates = Expression.Convert(outCreatesVar, Types.Object);
            var assignOutObj = Expression.Assign(outObjVar, boxCreates);

            var setOutDefault = Expression.Assign(outObjVar, Expressions.Constant_Null);

            var ifSetOrClear = Expression.IfThenElse(resVar, assignOutObj, setOutDefault);

            var body = Expression.Block(new[] { resVar, outCreatesVar }, assignToRes, ifSetOrClear, resVar);

            var lambda = Expression.Lambda<DynamicParserDelegate>(body, spanVar, ctxVar, outObjVar);
            var del = lambda.Compile();

            return del;
        }

        DynamicParserDelegate ICreatesCacheableDelegate<DynamicParserDelegate>.Guarantee(IDelegateCache cache)
        => IDelegateCacheHelpers.GuaranteeImpl<Parser, DynamicParserDelegate>(this, cache);

        Parser IElseSupporting<Parser>.Clone(ImmutableArray<Parser> newFallbacks)
        {
            return
                Mode switch
                {
                    BackingMode.Method => new Parser(Method.Value, Creates, newFallbacks, CreatesNullability),
                    BackingMode.Delegate => new Parser(Delegate.Value, Creates, newFallbacks, CreatesNullability),
                    BackingMode.Constructor => new Parser(Constructor.Value, newFallbacks),
                    _ => Throw.ImpossibleException<Parser>($"Unexpected {nameof(BackingMode)}: {Mode}"),
                };
        }

        /// <summary>
        /// Create a new parser that will try this parser, but if it returns false
        ///   it will then try the given fallback Parser.
        /// </summary>
        public Parser Else(Parser fallbackParser)
        {
            Utils.CheckArgumentNull(fallbackParser, nameof(fallbackParser));

            if (!Creates.IsAssignableFrom(fallbackParser.Creates))
            {
                return Throw.ArgumentException<Parser>($"{fallbackParser} does not provide a value assignable to {Creates}, and cannot be used as a fallback for this {nameof(Parser)}", nameof(fallbackParser));
            }

            // todo: does nullability need logic here?

            return this.DoElse(fallbackParser);
        }

        internal Expression MakeExpression(ParameterExpression dataVar, ParameterExpression contextVar, ParameterExpression outVar)
        {
            Expression selfExp;

            switch (Mode)
            {
                case BackingMode.Method:
                    {
                        var parserMtd = Method.Value;

                        selfExp = Expression.Call(parserMtd, dataVar, contextVar, outVar);
                    }
                    break;
                case BackingMode.Delegate:
                    {
                        var parserDel = Delegate.Value;
                        var delRef = Expression.Constant(parserDel);
                        selfExp = Expression.Invoke(delRef, dataVar, contextVar, outVar);
                    }
                    break;
                case BackingMode.Constructor:
                    {
                        var cons = Constructor.Value;
                        var psCount = cons.GetParameters().Length;
                        NewExpression callCons;

                        if (psCount == 1)
                        {
                            callCons = Expression.New(cons, dataVar);
                        }
                        else
                        {
                            callCons = Expression.New(cons, dataVar, contextVar);
                        }

                        var assignToL2 = Expression.Assign(outVar, callCons);

                        selfExp = Expression.Block(assignToL2, Expressions.Constant_True);
                    }
                    break;
                default:
                    return Throw.ImpossibleException<Expression>($"Unexpected {nameof(BackingMode)}: {Mode}");
            }

            var finalExp = selfExp;
            foreach (var fallback in _Fallbacks)
            {
                var fallbackExp = fallback.MakeExpression(dataVar, contextVar, outVar);
                finalExp = Expression.OrElse(finalExp, fallbackExp);
            }

            return finalExp;
        }

        /// <summary>
        /// Create a Parser from the given method.
        /// 
        /// The method must:
        ///  - be static
        ///  - return a bool
        ///  - have 3 parameters
        ///     * ReadOnlySpan(char)
        ///     * in ReadContext, 
        ///     * out assignable to outputType
        /// </summary>
        public static Parser ForMethod(MethodInfo method)
        {
            Utils.CheckArgumentNull(method, nameof(method));

            // parser must
            //   be a static method
            //   take a ReadOnlySpan<char>
            //   take an in ReadContext
            //   have an out parameter of a type assignable to the parameter of setter
            //   and return a boolean
            if (!method.IsStatic)
            {
                return Throw.ArgumentException<Parser>($"{nameof(method)} be a static method", nameof(method));
            }

            var args = method.GetParameters();
            if (args.Length != 3)
            {
                return Throw.ArgumentException<Parser>($"{nameof(method)} must have three parameters", nameof(method));
            }

            var p0 = args[0].ParameterType.GetTypeInfo();

            if (p0 != Types.ReadOnlySpanOfChar)
            {
                return Throw.ArgumentException<Parser>($"The first parameter of {nameof(method)} must be a {nameof(ReadOnlySpan<char>)}", nameof(method));
            }

            if (!args[1].IsReadContextByRef(out var msg))
            {
                return Throw.ArgumentException<Parser>($"The second parameter of {nameof(method)} must be an `in {nameof(ReadContext)}`; {msg}", nameof(method));
            }

            var arg2 = args[2];
            var p2 = arg2.ParameterType.GetTypeInfo();

            if (!p2.IsByRef)
            {
                return Throw.ArgumentException<Parser>($"The third parameter of {nameof(method)} must be an out", nameof(method));
            }

            var underlying = p2.GetElementTypeNonNull();
            var nullability = arg2.DetermineNullability();

            var parserRetType = method.ReturnType.GetTypeInfo();
            if (parserRetType != Types.Bool)
            {
                return Throw.ArgumentException<Parser>($"{nameof(method)} must return a bool", nameof(method));
            }

            return new Parser(method, underlying, ImmutableArray<Parser>.Empty, nullability);
        }

        /// <summary>
        /// Create a Parser from the given constructor.
        /// 
        /// The method must:
        ///  - take either a ReadOnlySpan(char)
        /// or
        ///  - take parameters
        ///     * ReadOnlySpan(char)
        ///     * in ReadContext
        /// </summary>
        public static Parser ForConstructor(ConstructorInfo constructor)
        {
            Utils.CheckArgumentNull(constructor, nameof(constructor));

            var ps = constructor.GetParameters();
            if (ps.Length == 1)
            {
                var firstP = ps[0].ParameterType.GetTypeInfo();

                if (firstP != Types.ReadOnlySpanOfChar)
                {
                    return Throw.ArgumentException<Parser>($"{nameof(constructor)} first parameter must be a ReadOnlySpan<char>", nameof(constructor));
                }
            }
            else if (ps.Length == 2)
            {
                var firstP = ps[0].ParameterType.GetTypeInfo();

                if (firstP != Types.ReadOnlySpanOfChar)
                {
                    return Throw.ArgumentException<Parser>($"{nameof(constructor)} first parameter must be a ReadOnlySpan<char>", nameof(constructor));
                }

                if (!ps[1].IsReadContextByRef(out var msg))
                {
                    return Throw.ArgumentException<Parser>($"{nameof(constructor)} second parameter must be an `in {nameof(ReadContext)}`; {msg}", nameof(constructor));
                }
            }
            else
            {
                return Throw.ArgumentException<Parser>($"{nameof(constructor)} must have one or two parameters", nameof(constructor));
            }

            return new Parser(constructor, ImmutableArray<Parser>.Empty);
        }

        /// <summary>
        /// Create a Parser from the given delegate.
        /// </summary>
        public static Parser ForDelegate<TOutput>(ParserDelegate<TOutput> del)
        {
            Utils.CheckArgumentNull(del, nameof(del));

            var nullability = del.Method.GetParameters()[2].DetermineNullability();

            return new Parser(del, typeof(TOutput).GetTypeInfo(), ImmutableArray<Parser>.Empty, nullability);
        }

        /// <summary>
        /// Returns the default parser for the given type, if any exists.
        /// </summary>
        [return: NullableExposed("May not be known, null is cleanest way to handle it")]
        public static Parser? GetDefault(TypeInfo forType)
        {
            Utils.CheckArgumentNull(forType, nameof(forType));

            if (forType.IsEnum)
            {
                var parsingClass = Types.DefaultEnumTypeParser.MakeGenericType(forType).GetTypeInfo();
                var parserField = parsingClass.GetFieldNonNull(nameof(DefaultTypeParsers.DefaultEnumTypeParser<StringComparison>.TryParseEnumParser), InternalStatic);
                var parser = (Parser?)parserField.GetValue(null);

                return parser;
            }

            var nullableElem = Nullable.GetUnderlyingType(forType)?.GetTypeInfo();
            if (nullableElem != null && nullableElem.IsEnum)
            {
                var parsingClass = Types.DefaultEnumTypeParser.MakeGenericType(nullableElem).GetTypeInfo();
                var parserField = parsingClass.GetFieldNonNull(nameof(DefaultTypeParsers.DefaultEnumTypeParser<StringComparison>.TryParseNullableEnumParser), InternalStatic);
                var parser = (Parser?)parserField.GetValue(null);

                return parser;
            }

            if (!TypeParsers.TryGetValue(forType, out var ret))
            {
                return null;
            }

            return ret;
        }

        /// <summary>
        /// Describes this Parser.
        /// 
        /// This is provided for debugging purposes, and the format is not guaranteed to be stable between releases.
        /// </summary>
        public override string ToString()
        {
            return
                Mode switch
                {
                    BackingMode.Method => $"{nameof(Parser)} backed by method {Method} creating {Creates}",
                    BackingMode.Delegate => $"{nameof(Parser)} backed by delegate {Delegate} creating {Creates}",
                    BackingMode.Constructor => $"{nameof(Parser)} backed by constructor {Constructor} creating {Creates}",
                    _ => Throw.InvalidOperationException<string>($"Unexpected {nameof(BackingMode)}: {Mode}"),
                };
        }

        /// <summary>
        /// Returns true if the given Parser is equivalent to this one
        /// </summary>
        public bool Equals(Parser? parser)
        {
            if (ReferenceEquals(parser, null)) return false;

            var selfMode = Mode;
            var otherMode = parser.Mode;

            if (selfMode != otherMode) return false;

            return
                selfMode switch
                {
                    BackingMode.Constructor => Constructor.Value == parser.Constructor.Value,
                    BackingMode.Delegate => Delegate.Value == parser.Delegate.Value,
                    BackingMode.Method => Method.Value == parser.Method.Value,
                    _ => Throw.InvalidOperationException<bool>($"Unexpected {nameof(BackingMode)}: {selfMode}"),
                };
        }

        /// <summary>
        /// Returns true if the given object is equivalent to this one
        /// </summary>
        public override bool Equals(object? obj)
        {
            if (obj is Parser p)
            {
                return Equals(p);
            }

            return false;
        }

        /// <summary>
        /// Returns a stable hash for this Parser.
        /// </summary>
        public override int GetHashCode()
        => HashCode.Combine(nameof(Parser), Mode, Method, Constructor, Delegate);

        /// <summary>
        /// Compare two Parsers for equality
        /// </summary>
        public static bool operator ==(Parser? a, Parser? b)
        => Utils.NullReferenceEquality(a, b);

        /// <summary>
        /// Compare two Parsers for inequality
        /// </summary>
        public static bool operator !=(Parser? a, Parser? b)
        => !(a == b);

        /// <summary>
        /// Convenience operator, equivalent to calling Parser.ForMethod if non-null.
        /// 
        /// Returns null if method is null.
        /// </summary>
        public static explicit operator Parser?(MethodInfo? method)
        => method == null ? null : ForMethod(method);

        /// <summary>
        /// Convenience operator, equivalent to calling Parser.ForConstructor if non-null.
        /// 
        /// Returns null if cons is null.
        /// </summary>
        public static explicit operator Parser?(ConstructorInfo? cons)
        => cons == null ? null : ForConstructor(cons);

        /// <summary>
        /// Convenience operator, equivalent to calling Parser.ForDelegate if non-null.
        /// 
        /// Returns null if del is null.
        /// </summary>
        public static explicit operator Parser?(Delegate? del)
        {
            if (del == null) return null;

            var delType = del.GetType().GetTypeInfo();

            if (delType.IsGenericType && delType.GetGenericTypeDefinition() == Types.ParserDelegate)
            {
                var t = delType.GetGenericArguments()[0].GetTypeInfo();
                var n = del.Method.GetParameters()[2].DetermineNullability();

                return new Parser(del, t, ImmutableArray<Parser>.Empty, n);
            }

            var mtd = del.Method;
            var ret = mtd.ReturnType.GetTypeInfo();
            if (ret != Types.Bool)
            {
                return Throw.InvalidOperationException<Parser>($"Delegate must return a bool");
            }

            var args = mtd.GetParameters();
            if (args.Length != 3)
            {
                return Throw.InvalidOperationException<Parser>($"Delegate must take 3 parameters");
            }

            var p0 = args[0].ParameterType.GetTypeInfo();
            if (p0 != Types.ReadOnlySpanOfChar)
            {
                return Throw.InvalidOperationException<Parser>($"The first parameter to the delegate must be a {nameof(ReadOnlySpan<char>)}");
            }

            if (!args[1].IsReadContextByRef(out var msg))
            {
                return Throw.InvalidOperationException<Parser>($"The second parameter to the delegate must be an `in {nameof(ReadContext)}`; {msg}");
            }

            var p2 = args[2];
            var createsRef = p2.ParameterType.GetTypeInfo();
            if (!createsRef.IsByRef)
            {
                return Throw.InvalidOperationException<Parser>($"The third parameter to the delegate must be an out type, was not by ref");
            }

            var creates = createsRef.GetElementTypeNonNull();
            var createsNullability = p2.DetermineNullability();

            var parserDel = Types.ParserDelegate.MakeGenericType(creates);
            var invoke = del.GetType().GetTypeInfo().GetMethodNonNull("Invoke");

            var reboundDel = System.Delegate.CreateDelegate(parserDel, del, invoke);

            return new Parser(reboundDel, creates, ImmutableArray<Parser>.Empty, createsNullability);
        }
    }
}
