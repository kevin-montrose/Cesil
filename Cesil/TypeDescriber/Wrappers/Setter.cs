using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Cesil
{
    /// <summary>
    /// Delegate type for setters that don't take an instance.
    /// </summary>
    public delegate void StaticSetterDelegate<TValue>(TValue value, in ReadContext context);

    /// <summary>
    /// Delegate type for setters.
    /// </summary>
    public delegate void SetterDelegate<TRow, TValue>(TRow instance, TValue value, in ReadContext context);

    /// <summary>
    /// Represents code used to set parsed values onto types.
    /// 
    /// Wraps either a MethodInfo, a FieldInfo, a SetterDelegate, a StaticSetterDelegate, or a constructor parameter.
    /// </summary>
    public sealed class Setter : IEquatable<Setter>
    {
        internal BackingMode Mode
        {
            get
            {
                if (Method.HasValue)
                {
                    return BackingMode.Method;
                }

                if (Field.HasValue)
                {
                    return BackingMode.Field;
                }

                if (Delegate.HasValue)
                {
                    return BackingMode.Delegate;
                }

                if (ConstructorParameter.HasValue)
                {
                    return BackingMode.ConstructorParameter;
                }

                return BackingMode.None;
            }
        }

        internal bool IsStatic
        {
            get
            {
                switch (Mode)
                {
                    case BackingMode.Field: return Field.Value.IsStatic;
                    case BackingMode.Method: return Method.Value.IsStatic;
                    case BackingMode.Delegate: return !RowType.HasValue;
                    case BackingMode.ConstructorParameter: return false;
                    default:
                        return Throw.InvalidOperationException<bool>($"Unexpected {nameof(BackingMode)}: {Mode}");
                }
            }
        }

        internal readonly NonNull<ParameterInfo> ConstructorParameter;

        internal readonly NonNull<MethodInfo> Method;

        internal readonly NonNull<FieldInfo> Field;

        internal readonly NonNull<Delegate> Delegate;

        internal readonly NonNull<TypeInfo> RowType;

        internal readonly TypeInfo Takes;

        internal readonly bool TakesContext;

        private Setter(TypeInfo rowType, TypeInfo takes, ParameterInfo param)
        {
            RowType.Value = rowType;
            Takes = takes;
            ConstructorParameter.Value = param;
            TakesContext = false;
        }

        private Setter(TypeInfo? rowType, TypeInfo takes, MethodInfo method, bool takesContext)
        {
            RowType.SetAllowNull(rowType);
            Takes = takes;
            Method.Value = method;
            TakesContext = takesContext;
        }

        private Setter(TypeInfo? rowType, TypeInfo takes, Delegate del)
        {
            RowType.SetAllowNull(rowType);
            Takes = takes;
            Delegate.Value = del;
            TakesContext = true;
        }

        private Setter(TypeInfo? rowType, TypeInfo takes, FieldInfo field)
        {
            RowType.SetAllowNull(rowType);
            Takes = takes;
            Field.Value = field;
            TakesContext = false;
        }

        internal Expression MakeExpression(ParameterExpression rowVar, ParameterExpression valVar, ParameterExpression ctxVar)
        {
            // todo: no reason this can't be chainable?

            Expression selfExp;

            switch (Mode)
            {
                case BackingMode.Method:
                    {
                        var setterMtd = Method.Value;

                        if (IsStatic)
                        {
                            if (RowType.HasValue)
                            {
                                if (TakesContext)
                                {
                                    selfExp = Expression.Call(setterMtd, rowVar, valVar, ctxVar);
                                }
                                else
                                {
                                    selfExp = Expression.Call(setterMtd, rowVar, valVar);
                                }
                            }
                            else
                            {
                                if (TakesContext)
                                {
                                    selfExp = Expression.Call(setterMtd, valVar, ctxVar);
                                }
                                else
                                {
                                    selfExp = Expression.Call(setterMtd, valVar);
                                }
                            }
                        }
                        else
                        {
                            if (TakesContext)
                            {
                                selfExp = Expression.Call(rowVar, setterMtd, valVar, ctxVar);
                            }
                            else
                            {
                                selfExp = Expression.Call(rowVar, setterMtd, valVar);
                            }
                        }
                    }
                    break;
                case BackingMode.Field:
                    {
                        MemberExpression fieldExp;

                        if (IsStatic)
                        {
                            fieldExp = Expression.Field(null, Field.Value);
                        }
                        else
                        {
                            fieldExp = Expression.Field(rowVar, Field.Value);
                        }

                        selfExp = Expression.Assign(fieldExp, valVar);
                    }
                    break;
                case BackingMode.Delegate:
                    {
                        var setterDel = Delegate.Value;
                        var delRef = Expression.Constant(setterDel);

                        if (IsStatic)
                        {
                            selfExp = Expression.Invoke(delRef, valVar, ctxVar);
                        }
                        else
                        {
                            selfExp = Expression.Invoke(delRef, rowVar, valVar, ctxVar);
                        }
                    }
                    break;
                default:
                    return Throw.InvalidOperationException<Expression>($"Unexpected {nameof(BackingMode)}: {Mode}");
            }

            return selfExp;
        }

        /// <summary>
        /// Create a setter from a PropertyInfo.
        /// 
        /// Throws if the property does not have a setter.
        /// </summary>
        public static Setter ForProperty(PropertyInfo property)
        {
            Utils.CheckArgumentNull(property, nameof(property));

            var set = property.SetMethod;

            if (set == null)
            {
                return Throw.ArgumentException<Setter>("Property does not have a setter", nameof(property));
            }

            return ForMethod(set);
        }

        /// <summary>
        /// Create a setter from a method.
        /// 
        /// The method must return void.
        /// 
        /// If the method is a static method it may:
        ///  - take 1 parameter (the result of the parser) or
        ///  - take 2 parameters, the result of the parser and an `in ReadContext` or
        ///  - take 2 parameters, the row type and the result of the parser or
        ///  - take 3 parameters, the row type, the result of the parser, and `in ReadContext`
        /// 
        /// If the method is an instance method:
        ///  - it must be on the row type, and take 1 parameter (the result of the parser) or
        ///  - it must be on the row type, and take 2 parameters, the result of the parser and an `in ReadContext`
        /// </summary>
        public static Setter ForMethod(MethodInfo method)
        {
            Utils.CheckArgumentNull(method, nameof(method));

            var returnsNoValue = method.ReturnType == Types.Void;

            if (!returnsNoValue)
            {
                return Throw.ArgumentException<Setter>($"{nameof(method)} must not return a value", nameof(method));
            }

            TypeInfo? onType;
            TypeInfo takesType;
            bool takesContext;

            var args = method.GetParameters();

            if (method.IsStatic)
            {
                onType = null;

                if (args.Length == 1)
                {
                    takesType = args[0].ParameterType.GetTypeInfo();
                    takesContext = false;
                }
                else if (args.Length == 2)
                {
                    var p0 = args[0].ParameterType.GetTypeInfo();

                    var p1 = args[1].ParameterType.GetTypeInfo();
                    if (!p1.IsByRef)
                    {
                        onType = p0;
                        takesType = p1;
                        takesContext = false;
                    }
                    else
                    {
                        var p1Elem = p1.GetElementTypeNonNull();
                        if (p1Elem != Types.ReadContext)
                        {
                            return Throw.ArgumentException<Setter>($"{nameof(Setter)} backed by a static method taking 2 parameters where the second parameter is by ref must have a second parameter of `in {nameof(ReadContext)}`, was not `{nameof(ReadContext)}`", nameof(method));
                        }

                        onType = null;
                        takesType = p0;
                        takesContext = true;
                    }
                }
                else if (args.Length == 3)
                {
                    onType = args[0].ParameterType.GetTypeInfo();
                    takesType = args[1].ParameterType.GetTypeInfo();

                    var p2 = args[2].ParameterType.GetTypeInfo();
                    if (!args[2].IsReadContextByRef(out var msg))
                    {
                        return Throw.ArgumentException<Setter>($"{nameof(Setter)} backed by an static method taking 3 parameters must have a third parameter of `in {nameof(ReadContext)}`; {msg}", nameof(method));
                    }

                    takesContext = true;
                }
                else
                {
                    return Throw.ArgumentException<Setter>($"A static method backing a {nameof(Setter)} must take 1, 2, or 3 parameters", nameof(method));
                }
            }
            else
            {
                onType = method.DeclaringTypeNonNull();

                if (args.Length == 1)
                {
                    takesType = args[0].ParameterType.GetTypeInfo();
                    takesContext = false;
                }
                else if (args.Length == 2)
                {
                    takesType = args[0].ParameterType.GetTypeInfo();

                    if (!args[1].IsReadContextByRef(out var msg))
                    {
                        return Throw.ArgumentException<Setter>($"{nameof(Setter)} backed by an instance method taking 2 parameters must have a second parameter of `in {nameof(ReadContext)}`; {msg}", nameof(method));
                    }

                    takesContext = true;
                }
                else
                {
                    return Throw.ArgumentException<Setter>($"An instance method backing a {nameof(Setter)} must take 1, or 2 parameters", nameof(method));
                }
            }

            return new Setter(onType, takesType, method, takesContext);
        }

        /// <summary>
        /// Creates setter from a field.
        /// 
        /// Field can be either an instance field or static field.
        /// </summary>
        public static Setter ForField(FieldInfo field)
        {
            Utils.CheckArgumentNull(field, nameof(field));

            TypeInfo? rowType;
            if (field.IsStatic)
            {
                rowType = null;
            }
            else
            {
                rowType = field.DeclaringTypeNonNull();
            }

            var takesType = field.FieldType.GetTypeInfo();

            return new Setter(rowType, takesType, field);
        }

        /// <summary>
        /// Create a Setter from the given delegate.
        /// </summary>
        public static Setter ForDelegate<TValue>(StaticSetterDelegate<TValue> del)
        {
            Utils.CheckArgumentNull(del, nameof(del));

            var takesType = typeof(TValue).GetTypeInfo();

            return new Setter(null, takesType, del);
        }

        /// <summary>
        /// Create a Setter from the given delegate.
        /// </summary>
        public static Setter ForDelegate<TRow, TValue>(SetterDelegate<TRow, TValue> del)
        {
            Utils.CheckArgumentNull(del, nameof(del));

            var setOnType = typeof(TRow).GetTypeInfo();
            var takesType = typeof(TValue).GetTypeInfo();

            return new Setter(setOnType, takesType, del);
        }

        /// <summary>
        /// Create a Setter from the given constructor parameter.
        /// </summary>
        public static Setter ForConstructorParameter(ParameterInfo parameter)
        {
            Utils.CheckArgumentNull(parameter, nameof(parameter));

            var mem = parameter.Member;
            if (mem is ConstructorInfo cons)
            {
                return new Setter(cons.DeclaringType!.GetTypeInfo(), parameter.ParameterType.GetTypeInfo(), parameter);
            }
            else
            {
                return Throw.ArgumentException<Setter>($"Expected parameter to be on a constructor; found {mem}", nameof(parameter));
            }
        }

        /// <summary>
        /// Returns true if this object equals the given Setter.
        /// </summary>
        public override bool Equals(object? obj)
        {
            if (obj is Setter s)
            {
                return Equals(s);
            }

            return false;
        }

        /// <summary>
        /// Returns true if this object equals the given Setter.
        /// </summary>
        public bool Equals(Setter setter)
        {
            if (ReferenceEquals(setter, null)) return false;

            var mode = Mode;
            var otherMode = setter.Mode;

            if (mode != otherMode) return false;
            if (Takes != setter.Takes) return false;
            if (IsStatic != setter.IsStatic) return false;

            if (RowType.HasValue)
            {
                if (!setter.RowType.HasValue) return false;

                if (RowType.Value != setter.RowType.Value) return false;
            }
            else
            {
                if (setter.RowType.HasValue) return false;
            }

            switch (mode)
            {
                case BackingMode.Delegate: return Delegate.Value == setter.Delegate.Value;
                case BackingMode.Field: return Field.Value == setter.Field.Value;
                case BackingMode.Method: return Method.Value == setter.Method.Value;
                case BackingMode.ConstructorParameter: return ConstructorParameter.Value == setter.ConstructorParameter.Value;

                default:
                    return Throw.Exception<bool>($"Unexpected {nameof(BackingMode)}: {mode}");
            }
        }

        /// <summary>
        /// Returns a stable hash for this Setter.
        /// </summary>
        public override int GetHashCode()
        => HashCode.Combine(nameof(Setter), Delegate, Field, IsStatic, Method, Mode, RowType, HashCode.Combine(Takes, ConstructorParameter));

        /// <summary>
        /// Describes this Setter.
        /// 
        /// This is provided for debugging purposes, and the format is not guaranteed to be stable between releases.
        /// </summary>
        public override string ToString()
        {
            switch (Mode)
            {
                case BackingMode.Delegate:
                    if (IsStatic)
                    {
                        return $"{nameof(Setter)} backed by delegate {Delegate} taking {Takes}";
                    }
                    else
                    {
                        return $"{nameof(Setter)} backed by delegate {Delegate} taking {RowType} and {Takes}";
                    }
                case BackingMode.Method:
                    if (IsStatic)
                    {
                        return $"{nameof(Setter)} backed by method {Method} taking {Takes}";
                    }
                    else
                    {
                        return $"{nameof(Setter)} on {RowType} backed by method {Method} taking {Takes}";
                    }
                case BackingMode.Field:
                    if (IsStatic)
                    {
                        return $"{nameof(Setter)} backed by field {Field} of {Takes}";
                    }
                    else
                    {
                        return $"{nameof(Setter)} on {RowType} backed by field {Field} of {Takes}";
                    }
                case BackingMode.ConstructorParameter:
                    return $"{nameof(Setter)} on {RowType} backed by the constructor parameter {ConstructorParameter}";
                default:
                    return Throw.InvalidOperationException<string>($"Unexpected {nameof(BackingMode)}: {Mode}");
            }
        }

        /// <summary>
        /// Convenience operator, equivalent to calling Setter.ForMethod if non-null.
        /// 
        /// Returns null if method is null.
        /// </summary>
        public static explicit operator Setter?(MethodInfo? method)
        => method == null ? null : ForMethod(method);

        /// <summary>
        /// Convenience operator, equivalent to calling Setter.ForField if non-null.
        /// 
        /// Returns null if field is null.
        /// </summary>
        public static explicit operator Setter?(FieldInfo? field)
        => field == null ? null : ForField(field);

        /// <summary>
        /// Convenience operator, equivalent to calling Setter.ForConstructorParameter if non-null.
        /// 
        /// Returns null if parameter is null.
        /// </summary>
        public static explicit operator Setter?(ParameterInfo? parameter)
        => parameter == null ? null : ForConstructorParameter(parameter);

        /// <summary>
        /// Convenience operator, equivalent to calling Setter.ForDelegate if non-null.
        /// 
        /// Returns null if field is null.
        /// </summary>
        public static explicit operator Setter?(Delegate? del)
        {
            if (del == null) return null;

            var delType = del.GetType().GetTypeInfo();
            if (delType.IsGenericType)
            {
                var delGenType = delType.GetGenericTypeDefinition().GetTypeInfo();
                if (delGenType == Types.SetterDelegate)
                {
                    var genArgs = delType.GetGenericArguments();
                    var rowType = genArgs[0].GetTypeInfo();
                    var takesType = genArgs[1].GetTypeInfo();

                    return new Setter(rowType, takesType, del);
                }
                else if (delGenType == Types.StaticSetterDelegate)
                {
                    var genArgs = delType.GetGenericArguments();
                    var takesType = genArgs[0].GetTypeInfo();

                    return new Setter(null, takesType, del);
                }
            }

            var mtd = del.Method;
            var retType = mtd.ReturnType.GetTypeInfo();
            if (retType != Types.Void)
            {
                return Throw.InvalidOperationException<Setter>($"Delegate must return void, found {retType}");
            }

            var ps = mtd.GetParameters();
            var invoke = delType.GetMethodNonNull("Invoke");
            if (ps.Length == 3)
            {
                var rowType = ps[0].ParameterType.GetTypeInfo();
                var takesType = ps[1].ParameterType.GetTypeInfo();

                if (!ps[2].IsReadContextByRef(out var msg))
                {
                    return Throw.InvalidOperationException<Setter>($"Delegate taking 3 parameters must have a third parameter of `in {nameof(ReadContext)}`; {msg}");
                }

                var setterDelType = Types.SetterDelegate.MakeGenericType(rowType, takesType);

                var reboundDel = System.Delegate.CreateDelegate(setterDelType, del, invoke);

                return new Setter(rowType, takesType, reboundDel);
            }
            else if (ps.Length == 2)
            {
                var takesType = ps[0].ParameterType.GetTypeInfo();

                if (!ps[1].IsReadContextByRef(out var msg))
                {
                    return Throw.InvalidOperationException<Setter>($"Delegate taking 2 parameters must have a second parameter of `in {nameof(ReadContext)}`; {msg}");
                }

                var setterDelType = Types.StaticSetterDelegate.MakeGenericType(takesType);

                var reboundDel = System.Delegate.CreateDelegate(setterDelType, del, invoke);

                return new Setter(null, takesType, reboundDel);
            }
            else
            {
                return Throw.InvalidOperationException<Setter>("Delegate must take 2 or 3 parameters");
            }
        }

        /// <summary>
        /// Compare two Setters for equality
        /// </summary>
        public static bool operator ==(Setter? a, Setter? b)
        => Utils.NullReferenceEquality(a, b);

        /// <summary>
        /// Compare two Setters for inequality
        /// </summary>
        public static bool operator !=(Setter? a, Setter? b)
        => !(a == b);
    }
}
