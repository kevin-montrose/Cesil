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
    /// Delegate type for setters, where instance is passed by ref.
    /// </summary>
    public delegate void SetterByRefDelegate<TRow, TValue>(ref TRow instance, TValue value, in ReadContext context);

    /// <summary>
    /// Represents code used to set parsed values onto types.
    /// 
    /// Wraps a static method, an instance method, a delegate, a field, or a constructor
    ///   parameter.
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
                return
                    Mode switch
                    {
                        BackingMode.Field => Field.Value.IsStatic,
                        BackingMode.Method => Method.Value.IsStatic,
                        BackingMode.Delegate => !RowType.HasValue,
                        BackingMode.ConstructorParameter => false,
                        _ => Throw.InvalidOperationException<bool>($"Unexpected {nameof(BackingMode)}: {Mode}"),
                    };
            }
        }

        internal readonly NonNull<ParameterInfo> ConstructorParameter;

        internal readonly NonNull<MethodInfo> Method;

        internal readonly NonNull<FieldInfo> Field;

        internal readonly NonNull<Delegate> Delegate;

        internal readonly NonNull<TypeInfo> RowType;
        internal readonly NullHandling? RowNullability;

        internal readonly TypeInfo Takes;
        internal readonly NullHandling TakesNullability;

        internal readonly bool TakesContext;

        internal readonly bool IsRowByRef;

        private Setter(TypeInfo rowType, NullHandling rowNullability, TypeInfo takes, ParameterInfo param, NullHandling takesNullability)
        {
            RowType.Value = rowType;
            RowNullability = rowNullability;
            Takes = takes;
            TakesNullability = takesNullability;
            ConstructorParameter.Value = param;
            TakesContext = false;
            IsRowByRef = false;
        }

        private Setter(TypeInfo? rowType, NullHandling? rowNullability, TypeInfo takes, MethodInfo method, bool takesContext, NullHandling takesNullability, bool isRowByRef)
        {
            RowType.SetAllowNull(rowType);
            RowNullability = rowNullability;
            Takes = takes;
            TakesNullability = takesNullability;
            Method.Value = method;
            TakesContext = takesContext;
            IsRowByRef = isRowByRef;
        }

        private Setter(TypeInfo? rowType, NullHandling? rowNullability, TypeInfo takes, Delegate del, NullHandling takesNullability, bool isRowByRef)
        {
            RowType.SetAllowNull(rowType);
            RowNullability = rowNullability;
            Takes = takes;
            TakesNullability = takesNullability;
            Delegate.Value = del;
            TakesContext = true;
            IsRowByRef = isRowByRef;
        }

        private Setter(TypeInfo? rowType, NullHandling? rowNullability, TypeInfo takes, FieldInfo field, NullHandling takesNullability)
        {
            RowType.SetAllowNull(rowType);
            RowNullability = rowNullability;
            Takes = takes;
            TakesNullability = takesNullability;
            Field.Value = field;
            TakesContext = false;
            IsRowByRef = false;
        }

        internal Expression MakeExpression(ParameterExpression rowVar, ParameterExpression valVar, ParameterExpression ctxVar)
        {
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

        private Setter ChangeRowNullHandling(NullHandling nullHandling)
        {
            if (nullHandling == RowNullability)
            {
                return this;
            }

            if (RowNullability == null)
            {
                return Throw.InvalidOperationException<Setter>($"{this} does not take rows, and so cannot have a {nameof(NullHandling)} specified");
            }

            Utils.ValidateNullHandling(RowType.Value, nullHandling);

            return
                Mode switch
                {
                    BackingMode.Field => new Setter(RowType.Value, nullHandling, Takes, Field.Value, TakesNullability),
                    BackingMode.Method => new Setter(RowType.Value, nullHandling, Takes, Method.Value, TakesContext, TakesNullability, IsRowByRef),
                    BackingMode.Delegate => new Setter(RowType.Value, nullHandling, Takes, Delegate.Value, TakesNullability, IsRowByRef),
                    _ => Throw.ImpossibleException<Setter>($"Unexpected: {nameof(BackingMode)}: {Mode}")
                };
        }

        private Setter ChangeValueNullHandling(NullHandling nullHandling)
        {
            if (nullHandling == TakesNullability)
            {
                return this;
            }

            Utils.ValidateNullHandling(Takes, nullHandling);

            var rowType = RowType.HasValue ? RowType.Value : null;

            return
                Mode switch
                {
                    BackingMode.Field => new Setter(rowType, RowNullability, Takes, Field.Value, nullHandling),
                    BackingMode.Method => new Setter(rowType, RowNullability, Takes, Method.Value, TakesContext, nullHandling, IsRowByRef),
                    BackingMode.Delegate => new Setter(rowType, RowNullability, Takes, Delegate.Value, nullHandling, IsRowByRef),
                    _ => Throw.ImpossibleException<Setter>($"Unexpected: {nameof(BackingMode)}: {Mode}")
                };
        }

        /// <summary>
        /// Returns a Setter that differs from this by explicitly allowing
        ///   null rows be passed to it.
        ///   
        /// If the backing field, delegate, or method does not expect null rows
        ///   this could result in errors at runtime.
        /// </summary>
        public Setter AllowNullRows()
        => ChangeRowNullHandling(NullHandling.AllowNull);

        /// <summary>
        /// Returns a Setter that differs from this by explicitly forbidding
        ///   null rows be passed to it.
        ///   
        /// If the .NET runtime cannot guarantee that nulls will not be passed,
        ///   null checks will be injected.
        /// </summary>
        public Setter ForbidNullRows()
        => ChangeRowNullHandling(NullHandling.ForbidNull);

        /// <summary>
        /// Returns a Setter that differs from this by explicitly allowing
        ///   null values be passed to it.
        ///   
        /// If the backing field, delegate, or method does not expect null values
        ///   this could result in errors at runtime.
        /// </summary>
        public Setter AllowNullValues()
        => ChangeValueNullHandling(NullHandling.AllowNull);

        /// <summary>
        /// Returns a Setter that differs from this by explicitly forbidding
        ///   null values be passed to it.
        ///   
        /// If the .NET runtime cannot guarantee that nulls will not be passed,
        ///   null checks will be injected.
        /// </summary>
        public Setter ForbidNullValues()
        => ChangeValueNullHandling(NullHandling.ForbidNull);

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
        ///  - take 2 parameters, the row type (which may be passed by ref), and the result of the parser or
        ///  - take 3 parameters, the row type (which may be passed by ref), the result of the parser, and `in ReadContext`
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
            NullHandling? onTypeNullability;
            TypeInfo takesType;
            NullHandling takesNullability;
            bool takesContext;
            bool isRowByRef;

            var args = method.GetParameters();

            if (method.IsStatic)
            {
                onType = null;
                onTypeNullability = null;

                if (args.Length == 1)
                {
                    // has to be a static method taking the _value_ to be set

                    var arg0 = args[0];

                    takesType = arg0.ParameterType.GetTypeInfo();
                    takesNullability = arg0.DetermineNullability();
                    takesContext = false;
                    isRowByRef = false;
                }
                else if (args.Length == 2)
                {
                    // one of three cases:
                    //   1. static method taking the row type and the value to be set
                    //   2. static method taking the row type _by ref_ and the value to be set
                    //   3. static method taking the value to be set and an in ReadContext

                    var arg0 = args[0];
                    var arg1 = args[1];

                    var p0 = arg0.ParameterType.GetTypeInfo();
                    var p1 = arg1.ParameterType.GetTypeInfo();

                    if (args[1].IsReadContextByRef(out var _))
                    {
                        // we're in case 3
                        onType = null;
                        onTypeNullability = null;
                        isRowByRef = false;

                        takesType = p0;

                        takesNullability = arg0.DetermineNullability();
                        takesContext = true;
                    }
                    else
                    {
                        // we're in case 2 or 3
                        // so p0 may be by ref or not, but p1 must NOT be by ref

                        if (p1.IsByRef)
                        {
                            return Throw.ArgumentException<Setter>($"{nameof(Setter)} backed by a static method taking 2 parameters cannot have a by ref second parameter unless that parameter is an `in {nameof(ReadContext)}`", nameof(method));
                        }

                        if (p0.IsByRef)
                        {
                            onType = p0.GetElementTypeNonNull();
                            isRowByRef = true;
                        }
                        else
                        {
                            onType = p0;
                            isRowByRef = false;
                        }

                        onTypeNullability = arg0.DetermineNullability();
                        takesType = p1;
                        takesNullability = arg1.DetermineNullability();
                        takesContext = false;
                    }
                }
                else if (args.Length == 3)
                {
                    var arg0 = args[0];
                    var arg1 = args[1];

                    var p0 = arg0.ParameterType.GetTypeInfo();
                    if (p0.IsByRef)
                    {
                        onType = p0.GetElementTypeNonNull();
                        isRowByRef = true;
                    }
                    else
                    {
                        onType = p0;
                        isRowByRef = false;
                    }

                    onTypeNullability = arg0.DetermineNullability();
                    takesType = arg1.ParameterType.GetTypeInfo();
                    takesNullability = arg1.DetermineNullability();

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
                onTypeNullability = NullHandling.CannotBeNull;

                isRowByRef = false;                             // instance is never (explicitly) by ref

                if (args.Length == 1)
                {
                    var arg0 = args[0];
                    takesType = arg0.ParameterType.GetTypeInfo();
                    takesNullability = arg0.DetermineNullability();
                    takesContext = false;
                }
                else if (args.Length == 2)
                {
                    var arg0 = args[0];
                    takesType = arg0.ParameterType.GetTypeInfo();
                    takesNullability = arg0.DetermineNullability();

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

            return new Setter(onType, onTypeNullability, takesType, method, takesContext, takesNullability, isRowByRef);
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
            NullHandling? rowTypeNullability;
            if (field.IsStatic)
            {
                rowType = null;
                rowTypeNullability = null;
            }
            else
            {
                rowType = field.DeclaringTypeNonNull();

                rowTypeNullability = NullHandling.CannotBeNull;
            }

            var takesType = field.FieldType.GetTypeInfo();
            var nullability = field.DetermineNullability();

            return new Setter(rowType, rowTypeNullability, takesType, field, nullability);
        }

        /// <summary>
        /// Create a Setter from the given delegate.
        /// </summary>
        public static Setter ForDelegate<TValue>(StaticSetterDelegate<TValue> del)
        {
            Utils.CheckArgumentNull(del, nameof(del));

            var takesType = typeof(TValue).GetTypeInfo();
            var nullability = del.Method.GetParameters()[0].DetermineNullability();

            return new Setter(null, null, takesType, del, nullability, false);
        }

        /// <summary>
        /// Create a Setter from the given delegate.
        /// </summary>
        public static Setter ForDelegate<TRow, TValue>(SetterDelegate<TRow, TValue> del)
        {
            Utils.CheckArgumentNull(del, nameof(del));

            var setOnType = typeof(TRow).GetTypeInfo();
            var takesType = typeof(TValue).GetTypeInfo();

            var ps = del.Method.GetParameters();

            var onTypeNullability = ps[0].DetermineNullability();
            var takesNullability = ps[1].DetermineNullability();

            return new Setter(setOnType, onTypeNullability, takesType, del, takesNullability, false);
        }

        /// <summary>
        /// Create a Setter from the given delegate.
        /// </summary>
        public static Setter ForDelegate<TRow, TValue>(SetterByRefDelegate<TRow, TValue> del)
        {
            Utils.CheckArgumentNull(del, nameof(del));

            var setOnType = typeof(TRow).GetTypeInfo().GetTypeInfo();
            var takesType = typeof(TValue).GetTypeInfo();

            var ps = del.Method.GetParameters();

            var onTypeNullability = ps[0].DetermineNullability();
            var takesNullability = ps[1].DetermineNullability();

            return new Setter(setOnType, onTypeNullability, takesType, del, takesNullability, true);
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
                return new Setter(cons.DeclaringTypeNonNull(), NullHandling.AllowNull, parameter.ParameterType.GetTypeInfo(), parameter, parameter.DetermineNullability());
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
        public bool Equals(Setter? setter)
        {
            if (ReferenceEquals(setter, null)) return false;

            var mode = Mode;
            var otherMode = setter.Mode;

            if (mode != otherMode) return false;
            if (TakesNullability != setter.TakesNullability) return false;
            if (Takes != setter.Takes) return false;
            if (IsStatic != setter.IsStatic) return false;

            if (RowType.HasValue)
            {
                if (!setter.RowType.HasValue)
                {
                    return false;
                }

                if (RowType.Value != setter.RowType.Value)
                {
                    return false;
                }

                if (RowNullability != setter.RowNullability)
                {
                    return false;
                }
            }
            else
            {
                if (setter.RowType.HasValue)
                {
                    return false;
                }
            }

            return
                mode switch
                {
                    BackingMode.Delegate => Delegate.Value == setter.Delegate.Value,
                    BackingMode.Field => Field.Value == setter.Field.Value,
                    BackingMode.Method => Method.Value == setter.Method.Value,
                    BackingMode.ConstructorParameter => ConstructorParameter.Value == setter.ConstructorParameter.Value,

                    _ => Throw.ImpossibleException<bool>($"Unexpected {nameof(BackingMode)}: {mode}"),
                };
        }

        /// <summary>
        /// Returns a stable hash for this Setter.
        /// </summary>
        public override int GetHashCode()
        => HashCode.Combine(nameof(Setter), Delegate, Field, IsStatic, Method, Mode, RowType, HashCode.Combine(Takes, ConstructorParameter, TakesNullability, RowNullability));

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
                        return $"{nameof(Setter)} backed by delegate {Delegate} taking {Takes} ({TakesNullability})";
                    }
                    else
                    {
                        return $"{nameof(Setter)} backed by delegate {Delegate} taking {RowType} ({RowNullability}) and {Takes} ({TakesNullability})";
                    }
                case BackingMode.Method:
                    if (IsStatic)
                    {
                        return $"{nameof(Setter)} backed by method {Method} taking {Takes} ({TakesNullability})";
                    }
                    else
                    {
                        return $"{nameof(Setter)} on {RowType} backed by method {Method} taking {Takes} ({TakesNullability})";
                    }
                case BackingMode.Field:
                    if (IsStatic)
                    {
                        return $"{nameof(Setter)} backed by field {Field} of {Takes} ({TakesNullability})";
                    }
                    else
                    {
                        return $"{nameof(Setter)} on {RowType} backed by field {Field} of {Takes} ({TakesNullability})";
                    }
                case BackingMode.ConstructorParameter:
                    return $"{nameof(Setter)} on {RowType} backed by the constructor parameter {ConstructorParameter} of {Takes} ({TakesNullability})";
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

                    var mtdPs = del.Method.GetParameters();

                    var rowNullability = mtdPs[0].DetermineNullability();
                    var takesNullability = mtdPs[1].DetermineNullability();

                    return new Setter(rowType, rowNullability, takesType, del, takesNullability, false);
                }
                else if (delGenType == Types.StaticSetterDelegate)
                {
                    var genArgs = delType.GetGenericArguments();
                    var takesType = genArgs[0].GetTypeInfo();
                    var nullability = del.Method.GetParameters()[0].DetermineNullability();

                    return new Setter(null, null, takesType, del, nullability, false);
                }
                else if (delGenType == Types.SetterByRefDelegate)
                {
                    var genArgs = delType.GetGenericArguments();
                    var rowType = genArgs[0].GetTypeInfo().MakeByRefType().GetTypeInfo();
                    var takesType = genArgs[1].GetTypeInfo();

                    var mtdPs = del.Method.GetParameters();

                    var rowNullability = mtdPs[0].DetermineNullability();
                    var takesNullability = mtdPs[1].DetermineNullability();

                    return new Setter(rowType, rowNullability, takesType, del, takesNullability, true);
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
                // 2 cases
                //  - row type, value type, in ReadContext
                //  - ref row type, value type, in ReadContext

                var p0 = ps[0];
                var p1 = ps[1];

                var rowType = p0.ParameterType.GetTypeInfo();
                var rowNullability = p0.DetermineNullability();
                var takesType = p1.ParameterType.GetTypeInfo();
                var takesNullability = p1.DetermineNullability();

                if (takesType.IsByRef)
                {
                    return Throw.InvalidOperationException<Setter>($"Delegate taking 3 parameters cannot have a by ref second parameter");
                }

                if (!ps[2].IsReadContextByRef(out var msg))
                {
                    return Throw.InvalidOperationException<Setter>($"Delegate taking 3 parameters must have a third parameter of `in {nameof(ReadContext)}`; {msg}");
                }

                var firstGenArg = rowType;
                var secondGenArg = takesType;
                var delegateType = Types.SetterDelegate;
                var isByRef = false;

                if (firstGenArg.IsByRef)
                {
                    firstGenArg = firstGenArg.GetElementTypeNonNull();
                    delegateType = Types.SetterByRefDelegate;
                    isByRef = true;
                }

                var setterDelType = delegateType.MakeGenericType(firstGenArg, secondGenArg);

                var reboundDel = System.Delegate.CreateDelegate(setterDelType, del, invoke);

                return new Setter(rowType, rowNullability, takesType, reboundDel, takesNullability, isByRef);
            }
            else if (ps.Length == 2)
            {
                var p0 = ps[0];
                var takesType = p0.ParameterType.GetTypeInfo();
                var takesNullability = p0.DetermineNullability();

                if (!ps[1].IsReadContextByRef(out var msg))
                {
                    return Throw.InvalidOperationException<Setter>($"Delegate taking 2 parameters must have a second parameter of `in {nameof(ReadContext)}`; {msg}");
                }

                var setterDelType = Types.StaticSetterDelegate.MakeGenericType(takesType);

                var reboundDel = System.Delegate.CreateDelegate(setterDelType, del, invoke);

                return new Setter(null, null, takesType, reboundDel, takesNullability, false);
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
