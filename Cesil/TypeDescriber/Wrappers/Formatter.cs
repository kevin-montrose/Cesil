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
    public delegate bool FormatterDelegate<T>(T value, in WriteContext context, IBufferWriter<char> buffer);

    /// <summary>
    /// Represents code used to format a value into a IBufferWriter(char).
    /// 
    /// Wraps either a MethodInfo or a FormatterDelegate.
    /// </summary>
    public sealed class Formatter : IEquatable<Formatter>
    {
        internal delegate bool DynamicFormatterDelegate(object value, in WriteContext context, IBufferWriter<char> buffer);


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
                if (Method != null) return BackingMode.Method;
                if (Delegate != null) return BackingMode.Delegate;

                return BackingMode.None;
            }
        }

        internal MethodInfo Method { get; }
        internal Delegate Delegate { get; }

        internal TypeInfo Takes { get; }

        internal DynamicFormatterDelegate DynamicDelegate;

        private Formatter(TypeInfo takes, MethodInfo method)
        {
            Takes = takes;
            Method = method;
            Delegate = null;
        }

        private Formatter(TypeInfo takes, Delegate del)
        {
            Takes = takes;
            Method = null;
            Delegate = del;
        }

        internal void PrimeDynamicDelegate(IDelegateCache cache)
        {
            if (DynamicDelegate != null) return;

            if (cache.TryGet<Formatter, DynamicFormatterDelegate>(this, out var cached))
            {
                DynamicDelegate = cached;
                return;
            }

            switch (Mode)
            {
                case BackingMode.Method:
                    {
                        var p0 = Expressions.Parameter_Object;
                        var p1 = Expressions.Parameter_WriteContext_ByRef;
                        var p2 = Expressions.Parameter_IBufferWriterOfChar;

                        var asT = Expression.Convert(p0, Takes);
                        var call = Expression.Call(Method, asT, p1, p2);

                        var block = Expression.Block(call);

                        var lambda = Expression.Lambda<DynamicFormatterDelegate>(block, p0, p1, p2);
                        var del = lambda.Compile();

                        DynamicDelegate = del;
                    }
                    break;
                case BackingMode.Delegate:
                    {
                        var p0 = Expressions.Parameter_Object;
                        var p1 = Expressions.Parameter_WriteContext_ByRef;
                        var p2 = Expressions.Parameter_IBufferWriterOfChar;

                        var asT = Expression.Convert(p0, Takes);

                        var delRef = Expression.Constant(Delegate);

                        var call = Expression.Invoke(delRef, asT, p1, p2);

                        var block = Expression.Block(call);

                        var lambda = Expression.Lambda<DynamicFormatterDelegate>(block, p0, p1, p2);
                        var del = lambda.Compile();

                        DynamicDelegate = del;
                    }
                    break;

                default:
                    Throw.InvalidOperationException($"Unexpected {nameof(BackingMode)}: {Mode}");
                    // just for control flow
                    break;
            }

            cache.Add(this, DynamicDelegate);
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
        public static Formatter ForMethod(MethodInfo formatter)
        {
            if (!formatter.IsStatic)
            {
                Throw.ArgumentException($"{nameof(formatter)} must be a static method", nameof(formatter));
            }

            var formatterRetType = formatter.ReturnType.GetTypeInfo();
            if (formatterRetType != Types.BoolType)
            {
                Throw.ArgumentException($"{nameof(formatter)} must return bool", nameof(formatter));
            }

            var args = formatter.GetParameters();
            if (args.Length != 3)
            {
                Throw.ArgumentException($"{nameof(formatter)} must take 3 parameters", nameof(formatter));
            }

            var takes = args[0].ParameterType.GetTypeInfo();

            var p2 = args[1].ParameterType.GetTypeInfo();
            if (!p2.IsByRef)
            {
                Throw.ArgumentException($"The second paramater to {nameof(formatter)} must be an in {nameof(WriteContext)}, was not by ref", nameof(formatter));
            }

            if (p2.GetElementType() != Types.WriteContextType)
            {
                Throw.ArgumentException($"The second paramater to {nameof(formatter)} must be an in {nameof(WriteContext)}", nameof(formatter));
            }

            if (args[2].ParameterType.GetTypeInfo() != Types.IBufferWriterOfCharType)
            {
                Throw.ArgumentException($"The third paramater to {nameof(formatter)} must be a {nameof(IBufferWriter<char>)}", nameof(formatter));
            }

            return new Formatter(takes, formatter);
        }

        /// <summary>
        /// Create a Formatter from the given delegate.
        /// </summary>
        public static Formatter ForDelegate<T>(FormatterDelegate<T> formatter)
        {
            if (formatter == null)
            {
                Throw.ArgumentNullException(nameof(formatter));
            }

            return new Formatter(typeof(T).GetTypeInfo(), formatter);
        }

        /// <summary>
        /// Returns the default formatter for the given type, if one exists.
        /// </summary>
        public static Formatter GetDefault(TypeInfo forType)
        {
            if (forType.IsEnum)
            {   
                if (forType.GetCustomAttribute<FlagsAttribute>() == null)
                {
                    var formattingClass = Types.DefaultEnumTypeFormatterType.MakeGenericType(forType).GetTypeInfo();
                    var formatterField = formattingClass.GetField(nameof(DefaultTypeFormatters.DefaultEnumTypeFormatter<StringComparison>.TryParseEnumFormatter), BindingFlags.Static | BindingFlags.NonPublic);
                    var formatter = (Formatter)formatterField.GetValue(null);

                    return formatter;
                }
                else
                {
                    var formattingClass = Types.DefaultFlagsEnumTypeFormatterType.MakeGenericType(forType).GetTypeInfo();
                    var formatterField = formattingClass.GetField(nameof(DefaultTypeFormatters.DefaultFlagsEnumTypeFormatter<StringComparison>.TryParseFlagsEnumFormatter), BindingFlags.Static | BindingFlags.NonPublic);
                    var formatter = (Formatter)formatterField.GetValue(null);

                    return formatter;
                }
            }

            var nullableElem = Nullable.GetUnderlyingType(forType)?.GetTypeInfo();
            if (nullableElem != null && nullableElem.IsEnum)
            {
                if (nullableElem.GetCustomAttribute<FlagsAttribute>() == null)
                {
                    var formattingClass = Types.DefaultEnumTypeFormatterType.MakeGenericType(nullableElem).GetTypeInfo();
                    var formatterField = formattingClass.GetField(nameof(DefaultTypeFormatters.DefaultEnumTypeFormatter<StringComparison>.TryParseNullableEnumFormatter), BindingFlags.Static | BindingFlags.NonPublic);
                    var formatter = (Formatter)formatterField.GetValue(null);

                    return formatter;
                }
                else
                {
                    var formattingClass = Types.DefaultFlagsEnumTypeFormatterType.MakeGenericType(nullableElem).GetTypeInfo();
                    var formatterField = formattingClass.GetField(nameof(DefaultTypeFormatters.DefaultFlagsEnumTypeFormatter<StringComparison>.TryParseNullableFlagsEnumFormatter), BindingFlags.Static | BindingFlags.NonPublic);
                    var formatter = (Formatter)formatterField.GetValue(null);

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
        public override bool Equals(object obj)
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
        public bool Equals(Formatter f)
        {
            if (f == null) return false;

            if (Takes != f.Takes) return false;

            var otherMode = f.Mode;
            if (otherMode != Mode) return false;

            switch (otherMode)
            {
                case BackingMode.Method:
                    return Method == f.Method;
                case BackingMode.Delegate:
                    return Delegate == f.Delegate;
                default:
                    Throw.InvalidOperationException($"Unexpected {nameof(BackingMode)}: {otherMode}");
                    // just for control flow
                    return default;
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
                    Throw.InvalidOperationException($"Unexpected {nameof(BackingMode)}: {Mode}");
                    // just for control flow
                    return default;
            }
        }

        /// <summary>
        /// Convenience operator, equivalent to calling Formatter.ForMethod if non-null.
        /// 
        /// Returns null if mtd is null.
        /// </summary>
        public static explicit operator Formatter(MethodInfo mtd)
        => mtd == null ? null : ForMethod(mtd);

        /// <summary>
        /// Convenience operator, equivalent to calling Formatter.ForDelegate if non-null.
        /// 
        /// Returns null if del is null.
        /// </summary>
        public static explicit operator Formatter(Delegate del)
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
                Throw.InvalidOperationException($"Delegate must return a bool");
            }

            var args = mtd.GetParameters();
            if (args.Length != 3)
            {
                Throw.InvalidOperationException($"Delegate must take 3 parameters");
            }

            var takes = args[0].ParameterType.GetTypeInfo();

            var p2 = args[1].ParameterType.GetTypeInfo();
            if (!p2.IsByRef)
            {
                Throw.InvalidOperationException($"The second paramater to the delegate must be an in {nameof(WriteContext)}, was not by ref");
            }

            if (p2.GetElementType() != Types.WriteContextType)
            {
                Throw.InvalidOperationException($"The second paramater to the delegate must be an in {nameof(WriteContext)}");
            }

            if (args[2].ParameterType.GetTypeInfo() != Types.IBufferWriterOfCharType)
            {
                Throw.InvalidOperationException($"The third paramater to the delegate must be a {nameof(IBufferWriter<char>)}");
            }

            var formatterDel = Types.FormatterDelegateType.MakeGenericType(takes);
            var invoke = del.GetType().GetMethod("Invoke");

            var reboundDel = Delegate.CreateDelegate(formatterDel, del, invoke);

            return new Formatter(takes, reboundDel);
        }

        /// <summary>
        /// Compare two Formatters for equality
        /// </summary>
        public static bool operator ==(Formatter a, Formatter b)
        => Utils.NullReferenceEquality(a, b);

        /// <summary>
        /// Compare two Formatters for inequality
        /// </summary>
        public static bool operator !=(Formatter a, Formatter b)
        => !(a == b);
    }
}
