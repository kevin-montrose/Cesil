using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq.Expressions;
using System.Reflection;

namespace Cesil
{
    /// <summary>
    /// Delegate type for formatters.
    /// </summary>
    public delegate bool FormatterDelegate<TValue>(TValue value, in WriteContext context, IBufferWriter<char> writer);

    /// <summary>
    /// Represents code used to format a value into a IBufferWriter(char).
    /// 
    /// Wraps either a static method or a delegate.
    /// </summary>
    public sealed class Formatter :
        IEquatable<Formatter>,
        ICreatesCacheableDelegate<Formatter.DynamicFormatterDelegate>,
        IElseSupporting<Formatter>
    {
        internal delegate bool DynamicFormatterDelegate(object? value, in WriteContext context, IBufferWriter<char> buffer);

        // internal for testing purposes
        internal static readonly IReadOnlyDictionary<TypeInfo, Formatter> TypeFormatters = CreateTypeFormatters();

        private static IReadOnlyDictionary<TypeInfo, Formatter> CreateTypeFormatters()
        {
            var ret = new Dictionary<TypeInfo, Formatter>();
            foreach (var mtd in Types.DefaultTypeFormatters.GetMethods(BindingFlagsConstants.InternalStatic))
            {
                var firstArg = mtd.GetParameters()[0];
                var forType = firstArg.ParameterType;

                ret.Add(forType.GetTypeInfo(), Formatter.ForMethod(mtd));
            }

            return ret;
        }

        internal BackingMode Mode
        {
            get
            {
                if (Method.HasValue) return BackingMode.Method;
                if (Delegate.HasValue) return BackingMode.Delegate;

                return BackingMode.None;
            }
        }

        internal readonly NonNull<MethodInfo> Method;

        internal readonly NonNull<Delegate> Delegate;

        internal readonly TypeInfo Takes;

        DynamicFormatterDelegate? ICreatesCacheableDelegate<DynamicFormatterDelegate>.CachedDelegate { get; set; }

        private readonly ImmutableArray<Formatter> _Fallbacks;
        ImmutableArray<Formatter> IElseSupporting<Formatter>.Fallbacks => _Fallbacks;

        private Formatter(TypeInfo takes, MethodInfo method, ImmutableArray<Formatter> fallbacks)
        {
            Takes = takes;
            Method.Value = method;
            Delegate.Clear();
            _Fallbacks = fallbacks;
        }

        private Formatter(TypeInfo takes, Delegate del, ImmutableArray<Formatter> fallbacks)
        {
            Takes = takes;
            Method.Clear();
            Delegate.Value = del;
            _Fallbacks = fallbacks;
        }

        Formatter IElseSupporting<Formatter>.Clone(ImmutableArray<Formatter> newFallbacks)
        {
            return
                Mode switch
                {
                    BackingMode.Delegate => new Formatter(Takes, Delegate.Value, newFallbacks),
                    BackingMode.Method => new Formatter(Takes, Method.Value, newFallbacks),
                    _ => Throw.ImpossibleException<Formatter>($"Unexpected {nameof(BackingMode)}: {Mode}"),
                };
        }

        DynamicFormatterDelegate ICreatesCacheableDelegate<DynamicFormatterDelegate>.CreateDelegate()
        {
            var p0 = Expressions.Parameter_Object;
            var p1 = Expressions.Parameter_WriteContext_ByRef;
            var p2 = Expressions.Parameter_IBufferWriterOfChar;

            var block = Expression.Block(MakeExpression(p0, p1, p2));

            var lambda = Expression.Lambda<DynamicFormatterDelegate>(block, p0, p1, p2);
            var del = lambda.Compile();

            return del;
        }

        internal Expression MakeExpression(ParameterExpression objectParam, ParameterExpression writeContextParam, ParameterExpression bufferWriterParam)
        {
            Expression selfExp;

            var asT = Expression.Convert(objectParam, Takes);

            switch (Mode)
            {
                case BackingMode.Delegate:
                    {
                        var delRef = Expression.Constant(Delegate.Value);
                        selfExp = Expression.Invoke(delRef, asT, writeContextParam, bufferWriterParam);

                        break;
                    }
                case BackingMode.Method:
                    {
                        var call = Expression.Call(Method.Value, asT, writeContextParam, bufferWriterParam);
                        selfExp = call;

                        break;
                    }
                default:
                    return Throw.ImpossibleException<Expression>($"Unexpected {nameof(BackingMode)}: {Mode}");
            }

            var ret = selfExp;
            foreach (var fallback in _Fallbacks)
            {
                var fallbackExp = fallback.MakeExpression(objectParam, writeContextParam, bufferWriterParam);
                ret = Expression.OrElse(ret, fallbackExp);
            }

            return ret;
        }

        DynamicFormatterDelegate ICreatesCacheableDelegate<DynamicFormatterDelegate>.Guarantee(IDelegateCache cache)
        => IDelegateCacheHelpers.GuaranteeImpl<Formatter, DynamicFormatterDelegate>(this, cache);

        /// <summary>
        /// Create a new formatter that will try this formatter, but if it returns false
        ///   it will then try the given fallback Formatter.
        /// </summary>
        public Formatter Else(Formatter fallbackFormatter)
        {
            Utils.CheckArgumentNull(fallbackFormatter, nameof(fallbackFormatter));

            if (!fallbackFormatter.Takes.IsAssignableFrom(Takes))
            {
                return Throw.ArgumentException<Formatter>($"{fallbackFormatter} does not take a value assignable from {Takes}, and cannot be used as a fallback for this {nameof(Formatter)}", nameof(fallbackFormatter));
            }

            return this.DoElse(fallbackFormatter);
        }

        /// <summary>
        /// Create a formatter from a method.
        /// 
        /// Formatter needs to:
        ///   * be static
        ///   * take
        ///     - the type to be formatter (or one it is assignable to)
        ///     - an in (or by ref) WriteContext
        ///     -an IBufferWriter(char)
        ///   * return bool (false indicates insufficient space was available)
        /// </summary>
        public static Formatter ForMethod(MethodInfo method)
        {
            Utils.CheckArgumentNull(method, nameof(method));

            if (!method.IsStatic)
            {
                return Throw.ArgumentException<Formatter>($"{nameof(method)} must be a static method", nameof(method));
            }

            var formatterRetType = method.ReturnType.GetTypeInfo();
            if (formatterRetType != Types.Bool)
            {
                return Throw.ArgumentException<Formatter>($"{nameof(method)} must return bool", nameof(method));
            }

            var args = method.GetParameters();
            if (args.Length != 3)
            {
                return Throw.ArgumentException<Formatter>($"{nameof(method)} must take 3 parameters", nameof(method));
            }

            var takes = args[0].ParameterType.GetTypeInfo();

            if (!args[1].IsWriteContextByRef(out var msg))
            {
                return Throw.ArgumentException<Formatter>($"The second parameter to {nameof(method)} must be an `in {nameof(WriteContext)}`; {msg}", nameof(method));
            }

            if (args[2].ParameterType.GetTypeInfo() != Types.IBufferWriterOfChar)
            {
                return Throw.ArgumentException<Formatter>($"The third parameter to {nameof(method)} must be a {nameof(IBufferWriter<char>)}", nameof(method));
            }

            return new Formatter(takes, method, ImmutableArray<Formatter>.Empty);
        }

        /// <summary>
        /// Create a Formatter from the given delegate.
        /// </summary>
        public static Formatter ForDelegate<TValue>(FormatterDelegate<TValue> del)
        {
            Utils.CheckArgumentNull(del, nameof(del));

            return new Formatter(typeof(TValue).GetTypeInfo(), del, ImmutableArray<Formatter>.Empty);
        }

        /// <summary>
        /// Returns the default formatter for the given type, if one exists.
        /// </summary>
        [return: NullableExposed("May not be known, null is cleanest way to handle it")]
        public static Formatter? GetDefault(TypeInfo forType)
        {
            Utils.CheckArgumentNull(forType, nameof(forType));

            if (forType.IsEnum)
            {
                var formattingClass = Types.DefaultEnumTypeFormatter.MakeGenericType(forType).GetTypeInfo();
                var formatterField = formattingClass.GetFieldNonNull(nameof(DefaultTypeFormatters.DefaultEnumTypeFormatter<StringComparison>.TryEnumFormatter), BindingFlagsConstants.InternalStatic);
                var formatter = (Formatter?)formatterField.GetValue(null);

                return formatter;
            }

            var nullableElem = Nullable.GetUnderlyingType(forType)?.GetTypeInfo();
            if (nullableElem != null && nullableElem.IsEnum)
            {
                var formattingClass = Types.DefaultEnumTypeFormatter.MakeGenericType(nullableElem).GetTypeInfo();
                var formatterField = formattingClass.GetFieldNonNull(nameof(DefaultTypeFormatters.DefaultEnumTypeFormatter<StringComparison>.TryNullableEnumFormatter), BindingFlagsConstants.InternalStatic);
                var formatter = (Formatter?)formatterField.GetValue(null);

                return formatter;
            }

            if (!TypeFormatters.TryGetValue(forType, out var ret))
            {
                return null;
            }

            return ret;
        }

        /// <summary>
        /// Compares for equality to another Formatter.
        /// </summary>
        public override bool Equals(object? obj)
        {
            if (obj is Formatter f)
            {
                return Equals(f);
            }

            return false;
        }

        /// <summary>
        /// Compares for equality to another Formatter.
        /// </summary>
        public bool Equals(Formatter formatter)
        {
            if (ReferenceEquals(formatter, null)) return false;

            if (Takes != formatter.Takes) return false;

            var otherMode = formatter.Mode;
            if (otherMode != Mode) return false;

            if (_Fallbacks.Length != formatter._Fallbacks.Length) return false;

            for (var i = 0; i < _Fallbacks.Length; i++)
            {
                var sf = _Fallbacks[i];
                var of = formatter._Fallbacks[i];

                if (sf != of) return false;
            }

            return
                otherMode switch
                {
                    BackingMode.Method => Method.Value == formatter.Method.Value,
                    BackingMode.Delegate => Delegate.Value == formatter.Delegate.Value,
                    _ => Throw.InvalidOperationException<bool>($"Unexpected {nameof(BackingMode)}: {otherMode}"),
                };
        }

        /// <summary>
        /// Returns a hash code for this Getter.
        /// </summary>
        public override int GetHashCode()
        => HashCode.Combine(nameof(Formatter), Takes, Mode, Delegate, Method, _Fallbacks.Length);

        /// <summary>
        /// Describes this Formatter.
        /// 
        /// This is provided for debugging purposes, and the format is not guaranteed to be stable between releases.
        /// </summary>
        public override string ToString()
        {
            switch (Mode)
            {
                case BackingMode.Method:
                    if (_Fallbacks.Length > 0)
                    {
                        return $"{nameof(Formatter)} for {Takes} backed by method {Method} with fallbacks {string.Join(", ", _Fallbacks)}";
                    }
                    else
                    {
                        return $"{nameof(Formatter)} for {Takes} backed by method {Method}";
                    }
                case BackingMode.Delegate:
                    if (_Fallbacks.Length > 0)
                    {
                        return $"{nameof(Formatter)} for {Takes} backed by delegate {Delegate} with fallbacks {string.Join(", ", _Fallbacks)}";
                    }
                    else
                    {
                        return $"{nameof(Formatter)} for {Takes} backed by delegate {Delegate}";
                    }
                default:
                    return Throw.InvalidOperationException<string>($"Unexpected {nameof(BackingMode)}: {Mode}");
            }
        }

        /// <summary>
        /// Convenience operator, equivalent to calling Formatter.ForMethod if non-null.
        /// 
        /// Returns null if method is null.
        /// </summary>
        public static explicit operator Formatter?(MethodInfo? method)
        => method == null ? null : ForMethod(method);

        /// <summary>
        /// Convenience operator, equivalent to calling Formatter.ForDelegate if non-null.
        /// 
        /// Returns null if del is null.
        /// </summary>
        public static explicit operator Formatter?(Delegate? del)
        {
            if (del == null) return null;

            var delType = del.GetType().GetTypeInfo();

            if (delType.IsGenericType && delType.GetGenericTypeDefinition() == Types.FormatterDelegate)
            {
                var t = delType.GetGenericArguments()[0].GetTypeInfo();

                return new Formatter(t, del, ImmutableArray<Formatter>.Empty);
            }

            var mtd = del.Method;
            var ret = mtd.ReturnType.GetTypeInfo();
            if (ret != Types.Bool)
            {
                return Throw.InvalidOperationException<Formatter>($"Delegate must return a bool");
            }

            var args = mtd.GetParameters();
            if (args.Length != 3)
            {
                return Throw.InvalidOperationException<Formatter>($"Delegate must take 3 parameters");
            }

            var takes = args[0].ParameterType.GetTypeInfo();

            if (!args[1].IsWriteContextByRef(out var msg))
            {
                return Throw.InvalidOperationException<Formatter>($"The second parameter to the delegate must be an `in {nameof(WriteContext)}`; {msg}");
            }

            if (args[2].ParameterType.GetTypeInfo() != Types.IBufferWriterOfChar)
            {
                return Throw.InvalidOperationException<Formatter>($"The third parameter to the delegate must be a {nameof(IBufferWriter<char>)}");
            }

            var formatterDel = Types.FormatterDelegate.MakeGenericType(takes);
            var invoke = del.GetType().GetTypeInfo().GetMethodNonNull("Invoke");

            var reboundDel = System.Delegate.CreateDelegate(formatterDel, del, invoke);

            return new Formatter(takes, reboundDel, ImmutableArray<Formatter>.Empty);
        }

        /// <summary>
        /// Compare two Formatters for equality
        /// </summary>
        public static bool operator ==(Formatter? a, Formatter? b)
        => Utils.NullReferenceEquality(a, b);

        /// <summary>
        /// Compare two Formatters for inequality
        /// </summary>
        public static bool operator !=(Formatter? a, Formatter? b)
        => !(a == b);
    }
}
