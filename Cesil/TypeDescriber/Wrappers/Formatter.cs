using System;
using System.Buffers;
using System.Collections.Generic;
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
    /// Wraps either a MethodInfo or a FormatterDelegate.
    /// </summary>
    public sealed class Formatter : IEquatable<Formatter>, ICreatesCacheableDelegate<Formatter.DynamicFormatterDelegate>
    {
        internal delegate bool DynamicFormatterDelegate(object? value, in WriteContext context, IBufferWriter<char> buffer);

        private static readonly IReadOnlyDictionary<TypeInfo, Formatter> TypeFormatters;

        static Formatter()
        {
            // load up default formatters
            var ret = new Dictionary<TypeInfo, Formatter>();
            foreach (var mtd in Types.DefaultTypeFormattersType.GetMethods(BindingFlags.Static | BindingFlags.NonPublic))
            {
                var firstArg = mtd.GetParameters()[0];
                var forType = firstArg.ParameterType;

                ret.Add(forType.GetTypeInfo(), Formatter.ForMethod(mtd));
            }

            TypeFormatters = ret;
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

        private NonNull<DynamicFormatterDelegate> _CachedDelegate;
        ref NonNull<DynamicFormatterDelegate> ICreatesCacheableDelegate<DynamicFormatterDelegate>.CachedDelegate => ref _CachedDelegate;

        private Formatter(TypeInfo takes, MethodInfo method)
        {
            Takes = takes;
            Method.Value = method;
            Delegate.Clear();
        }

        private Formatter(TypeInfo takes, Delegate del)
        {
            Takes = takes;
            Method.Clear();
            Delegate.Value = del;
        }

        DynamicFormatterDelegate ICreatesCacheableDelegate<DynamicFormatterDelegate>.CreateDelegate()
        {
            switch (Mode)
            {
                case BackingMode.Method:
                    {
                        var p0 = Expressions.Parameter_Object;
                        var p1 = Expressions.Parameter_WriteContext_ByRef;
                        var p2 = Expressions.Parameter_IBufferWriterOfChar;

                        var asT = Expression.Convert(p0, Takes);
                        var call = Expression.Call(Method.Value, asT, p1, p2);

                        var block = Expression.Block(call);

                        var lambda = Expression.Lambda<DynamicFormatterDelegate>(block, p0, p1, p2);
                        var del = lambda.Compile();

                        return del;
                    }
                case BackingMode.Delegate:
                    {
                        var p0 = Expressions.Parameter_Object;
                        var p1 = Expressions.Parameter_WriteContext_ByRef;
                        var p2 = Expressions.Parameter_IBufferWriterOfChar;

                        var asT = Expression.Convert(p0, Takes);

                        var delRef = Expression.Constant(Delegate.Value);

                        var call = Expression.Invoke(delRef, asT, p1, p2);

                        var block = Expression.Block(call);

                        var lambda = Expression.Lambda<DynamicFormatterDelegate>(block, p0, p1, p2);
                        var del = lambda.Compile();

                        return del;
                    }
                default:
                    return Throw.InvalidOperationException<DynamicFormatterDelegate>($"Unexpected {nameof(BackingMode)}: {Mode}");
            }
        }

        void ICreatesCacheableDelegate<DynamicFormatterDelegate>.Guarantee(IDelegateCache cache)
        => IDelegateCacheHelpers.GuaranteeImpl<Formatter, DynamicFormatterDelegate>(this, cache);

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
            if (formatterRetType != Types.BoolType)
            {
                return Throw.ArgumentException<Formatter>($"{nameof(method)} must return bool", nameof(method));
            }

            var args = method.GetParameters();
            if (args.Length != 3)
            {
                return Throw.ArgumentException<Formatter>($"{nameof(method)} must take 3 parameters", nameof(method));
            }

            var takes = args[0].ParameterType.GetTypeInfo();

            var p2 = args[1].ParameterType.GetTypeInfo();
            if (!p2.IsByRef)
            {
                return Throw.ArgumentException<Formatter>($"The second paramater to {nameof(method)} must be an in {nameof(WriteContext)}, was not by ref", nameof(method));
            }

            if (p2.GetElementTypeNonNull() != Types.WriteContextType)
            {
                return Throw.ArgumentException<Formatter>($"The second paramater to {nameof(method)} must be an in {nameof(WriteContext)}", nameof(method));
            }

            if (args[2].ParameterType.GetTypeInfo() != Types.IBufferWriterOfCharType)
            {
                return Throw.ArgumentException<Formatter>($"The third paramater to {nameof(method)} must be a {nameof(IBufferWriter<char>)}", nameof(method));
            }

            return new Formatter(takes, method);
        }

        /// <summary>
        /// Create a Formatter from the given delegate.
        /// </summary>
        public static Formatter ForDelegate<TValue>(FormatterDelegate<TValue> del)
        {
            Utils.CheckArgumentNull(del, nameof(del));

            return new Formatter(typeof(TValue).GetTypeInfo(), del);
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
                if (forType.GetCustomAttribute<FlagsAttribute>() == null)
                {
                    var formattingClass = Types.DefaultEnumTypeFormatterType.MakeGenericType(forType).GetTypeInfo();
                    var formatterField = formattingClass.GetFieldNonNull(nameof(DefaultTypeFormatters.DefaultEnumTypeFormatter<StringComparison>.TryParseEnumFormatter), BindingFlags.Static | BindingFlags.NonPublic);
                    var formatter = (Formatter?)formatterField.GetValue(null);

                    return formatter;
                }
                else
                {
                    var formattingClass = Types.DefaultFlagsEnumTypeFormatterType.MakeGenericType(forType).GetTypeInfo();
                    var formatterField = formattingClass.GetFieldNonNull(nameof(DefaultTypeFormatters.DefaultFlagsEnumTypeFormatter<StringComparison>.TryParseFlagsEnumFormatter), BindingFlags.Static | BindingFlags.NonPublic);
                    var formatter = (Formatter?)formatterField.GetValue(null);

                    return formatter;
                }
            }

            var nullableElem = Nullable.GetUnderlyingType(forType)?.GetTypeInfo();
            if (nullableElem != null && nullableElem.IsEnum)
            {
                if (nullableElem.GetCustomAttribute<FlagsAttribute>() == null)
                {
                    var formattingClass = Types.DefaultEnumTypeFormatterType.MakeGenericType(nullableElem).GetTypeInfo();
                    var formatterField = formattingClass.GetFieldNonNull(nameof(DefaultTypeFormatters.DefaultEnumTypeFormatter<StringComparison>.TryParseNullableEnumFormatter), BindingFlags.Static | BindingFlags.NonPublic);
                    var formatter = (Formatter?)formatterField.GetValue(null);

                    return formatter;
                }
                else
                {
                    var formattingClass = Types.DefaultFlagsEnumTypeFormatterType.MakeGenericType(nullableElem).GetTypeInfo();
                    var formatterField = formattingClass.GetFieldNonNull(nameof(DefaultTypeFormatters.DefaultFlagsEnumTypeFormatter<StringComparison>.TryParseNullableFlagsEnumFormatter), BindingFlags.Static | BindingFlags.NonPublic);
                    var formatter = (Formatter?)formatterField.GetValue(null);

                    return formatter;
                }
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

            switch (otherMode)
            {
                case BackingMode.Method:
                    return Method.Value == formatter.Method.Value;
                case BackingMode.Delegate:
                    return Delegate.Value == formatter.Delegate.Value;
                default:
                    return Throw.InvalidOperationException<bool>($"Unexpected {nameof(BackingMode)}: {otherMode}");
            }
        }

        /// <summary>
        /// Returns a hashcode for this Getter.
        /// </summary>
        public override int GetHashCode()
        => HashCode.Combine(nameof(Formatter), Takes, Mode, Delegate, Method);

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
                    return $"{nameof(Formatter)} for {Takes} backed by method {Method}";
                case BackingMode.Delegate:
                    return $"{nameof(Formatter)} for {Takes} backed by delegate {Delegate}";
                default:
                    return Throw.InvalidOperationException<string>($"Unexpected {nameof(BackingMode)}: {Mode}");
            }
        }

        /// <summary>
        /// Convenience operator, equivalent to calling Formatter.ForMethod if non-null.
        /// 
        /// Returns null if mtd is null.
        /// </summary>
        public static explicit operator Formatter?(MethodInfo? mtd)
        => mtd == null ? null : ForMethod(mtd);

        /// <summary>
        /// Convenience operator, equivalent to calling Formatter.ForDelegate if non-null.
        /// 
        /// Returns null if del is null.
        /// </summary>
        public static explicit operator Formatter?(Delegate? del)
        {
            if (del == null) return null;

            var delType = del.GetType().GetTypeInfo();

            if (delType.IsGenericType && delType.GetGenericTypeDefinition() == Types.FormatterDelegateType)
            {
                var t = delType.GetGenericArguments()[0].GetTypeInfo();

                return new Formatter(t, del);
            }

            var mtd = del.Method;
            var ret = mtd.ReturnType.GetTypeInfo();
            if (ret != Types.BoolType)
            {
                return Throw.InvalidOperationException<Formatter>($"Delegate must return a bool");
            }

            var args = mtd.GetParameters();
            if (args.Length != 3)
            {
                return Throw.InvalidOperationException<Formatter>($"Delegate must take 3 parameters");
            }

            var takes = args[0].ParameterType.GetTypeInfo();

            var p2 = args[1].ParameterType.GetTypeInfo();
            if (!p2.IsByRef)
            {
                return Throw.InvalidOperationException<Formatter>($"The second paramater to the delegate must be an in {nameof(WriteContext)}, was not by ref");
            }

            if (p2.GetElementTypeNonNull() != Types.WriteContextType)
            {
                return Throw.InvalidOperationException<Formatter>($"The second paramater to the delegate must be an in {nameof(WriteContext)}");
            }

            if (args[2].ParameterType.GetTypeInfo() != Types.IBufferWriterOfCharType)
            {
                return Throw.InvalidOperationException<Formatter>($"The third paramater to the delegate must be a {nameof(IBufferWriter<char>)}");
            }

            var formatterDel = Types.FormatterDelegateType.MakeGenericType(takes);
            var invoke = del.GetType().GetTypeInfo().GetMethodNonNull("Invoke");

            var reboundDel = System.Delegate.CreateDelegate(formatterDel, del, invoke);

            return new Formatter(takes, reboundDel);
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
