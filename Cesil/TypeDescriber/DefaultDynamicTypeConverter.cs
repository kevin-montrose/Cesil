using System;
using System.Collections.Generic;
using System.Reflection;

namespace Cesil
{
    /// <summary>
    /// The default implementation of IDynamicTypeConverter used to
    ///   determine how to map a dynamic cell or row to a concrete type.
    ///   
    /// It will map cells to types using DeserializableMember.GetDefaultParser() or a
    ///    constructor that takes a ReadOnlySpan(char).
    /// 
    /// It will map rows to Tuple, ValueTuple, IEnumerable(T), IEnumerable, user defined 
    ///   types with constructors of the same arity as the row, user defined types with
    ///   a constructor taking a single object/dynamic, or user defined types where column
    ///   names match properties and a zero-parameter constructor is defined.
    /// </summary>
    public class DefaultDynamicTypeConverter : IDynamicTypeConverter
    {
        /// <summary>
        /// Represents a constructor - columns pair.
        /// 
        /// Used by GetRowConstuctorTakingTypedParameters to indicate
        ///   a constructor to invoke with particular column values
        ///   to map a dynamic row to a concrete type.
        /// </summary>
        protected sealed class ConstructorWithParameters
        {
            /// <summary>
            /// Constructor to invoke.
            /// </summary>
            public ConstructorInfo Constructor { get; }
            /// <summary>
            /// Indexes of the columns to pass to Constructor.
            /// </summary>
            public IEnumerable<int> ColumnIndexes { get; }

            /// <summary>
            /// Create a new ConstructorWithParameters with the given constructor
            ///   and columns.
            /// </summary>
            public ConstructorWithParameters(ConstructorInfo cons, IEnumerable<int> cols)
            {
                if (cons == null)
                {
                    Throw.ArgumentNullException(nameof(cons));
                }

                if (cols == null)
                {
                    Throw.ArgumentNullException(nameof(cols));
                }

                Constructor = cons;
                ColumnIndexes = cols;
            }
        }

        /// <summary>
        /// Represents a constructor - setters - columns triple.
        /// 
        /// Used by GetRowConstructorAndSetters to indicate
        ///   a constructor to invoke to create a type, and then a
        ///   series of setters to invoke and pass columns at the
        ///   corresponding indexes to.
        /// </summary>
        protected sealed class ConstructorWithSetters
        {
            /// <summary>
            /// Constructor to invoke.
            /// </summary>
            public ConstructorInfo Constructor { get; }
            /// <summary>
            /// Setters to invoke with column values.
            /// </summary>
            public IEnumerable<MethodInfo> Setters { get; }
            /// <summary>
            /// Indexes of the columns to pass to each setter.
            /// </summary>
            public IEnumerable<int> ColumnIndexes { get; }

            /// <summary>
            /// Create a new ConstructorWithParameters with the given constructor
            ///   and columns.
            /// </summary>
            public ConstructorWithSetters(ConstructorInfo cons, IEnumerable<MethodInfo> setters, IEnumerable<int> cols)
            {
                if (cons == null)
                {
                    Throw.ArgumentNullException(nameof(cons));
                }

                if (setters == null)
                {
                    Throw.ArgumentNullException(nameof(setters));
                }

                if (cols == null)
                {
                    Throw.ArgumentNullException(nameof(cols));
                }

                Constructor = cons;
                Setters = setters;
                ColumnIndexes = cols;
            }
        }

        /// <summary>
        /// Create a new instance of DefaultDynamicTypeConverter.
        /// 
        /// A pre-allocated instances can be found on DynamicTypeConverters.
        /// </summary>
        public DefaultDynamicTypeConverter() { }

        /// <summary>
        /// Returns a DynamicCellConverter that can be used to parse the targetType,
        ///    if a default parser for the type exists or a constructor accepting
        ///    ReadOnlySpan(char) is on the the type.
        /// </summary>
        public virtual DynamicCellConverter GetCellConverter(int columnNumber, string columnName, TypeInfo targetType)
        {
            var parser = GetCellParsingMethodFor(columnNumber, columnName, targetType);
            if (parser != null)
            {
                return DynamicCellConverter.ForMethod(parser);
            }

            var cons = GetCellConstructorFor(columnNumber, columnName, targetType);
            if (cons != null)
            {
                return DynamicCellConverter.ForConstructor(cons);
            }

            return null;
        }

        /// <summary>
        /// Returns the default cell parsing method for the given type, if any.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        protected virtual MethodInfo GetCellParsingMethodFor(int columnNumber, string columName, TypeInfo targetType)
        => DeserializableMember.GetDefaultParser(targetType);

        /// <summary>
        /// Returns the default constructor for the given type, if any.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        protected virtual ConstructorInfo GetCellConstructorFor(int columNumber, string columName, TypeInfo targetType)
        => targetType.GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, Types.ReadOnlySpanOfCharType_Array, null);

        /// <summary>
        /// Returns a DynamicRowConverter that can be used to parse the targetType,
        ///    if a default parser for the type exists or a constructor accepting
        ///    the appropriate number of objects (can be dynamic in source) is on 
        ///    the the type.
        /// </summary>
        public virtual DynamicRowConverter GetRowConverter(int rowNumber, int columnCount, string[] columnNames, TypeInfo targetType)
        {
            var parser = GetRowParsingMethodFor(rowNumber, columnCount, columnNames, targetType);
            if (parser != null)
            {
                return DynamicRowConverter.ForMethod(parser);
            }

            var oneParamCons = GetRowConstructorTakingDynamicFor(rowNumber, columnCount, columnNames, targetType);
            if (oneParamCons != null)
            {
                return DynamicRowConverter.ForConstructorTakingDynamic(oneParamCons);
            }

            var consTuple = GetRowConstuctorTakingTypedParameters(rowNumber, columnCount, columnNames, targetType);
            if (consTuple != null)
            {
                return DynamicRowConverter.ForConstructorTakingTypedParameters(consTuple.Constructor, consTuple.ColumnIndexes);
            }

            var setterTuple = GetRowConstructorAndSetters(rowNumber, columnCount, columnNames, targetType);
            if (setterTuple != null)
            {
                return DynamicRowConverter.ForEmptyConstructorAndSetters(setterTuple.Constructor, setterTuple.Setters, setterTuple.ColumnIndexes);
            }

            return null;
        }

        /// <summary>
        /// Returns the default parsing method for the given type, if any,
        ///   that maps a whole row to the given type.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        protected virtual MethodInfo GetRowParsingMethodFor(int rowNumber, int columnCount, string[] columnNames, TypeInfo targetType)
        {
            if (IsValueTuple(targetType))
            {
                var mtd = Types.TupleDynamicParser.MakeGenericType(targetType);
                return mtd.GetMethod(nameof(TupleDynamicParsers<object>.TryConvertValueTuple), BindingFlags.Static | BindingFlags.NonPublic);
            }
            else if (IsTuple(targetType))
            {
                var mtd = Types.TupleDynamicParser.MakeGenericType(targetType);
                return mtd.GetMethod(nameof(TupleDynamicParsers<object>.TryConvertTuple), BindingFlags.Static | BindingFlags.NonPublic);
            }

            return null;
        }

        /// <summary>
        /// Returns the single-object-taking constructor for the given type (or one assignable
        ///   to it), if any, that maps a whole row to the given type.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        protected virtual ConstructorInfo GetRowConstructorTakingDynamicFor(int rowNumber, int columnCount, string[] columnNames, TypeInfo targetType)
        {
            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition().GetTypeInfo() == Types.IEnumerableOfTType)
            {
                var elementType = targetType.GetGenericArguments()[0];
                var genEnum = Types.DynamicRowEnumerableType.MakeGenericType(elementType);
                var cons = genEnum.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { Types.ObjectType }, null);
                return cons;
            }
            else if (targetType == Types.IEnumerableType)
            {
                var cons = Types.DynamicRowEnumerableNonGenericType.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { Types.ObjectType }, null);
                return cons;
            }

            return null;
        }

        /// <summary>
        /// Returns a constructor and an array of column indexes that corresponds
        ///   to a constructor on the given type (or one assignable to it) that takes
        ///   typed parameters for each of the included columns.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        protected virtual ConstructorWithParameters GetRowConstuctorTakingTypedParameters(int rowNumber, int columnCount, string[] columnNames, TypeInfo targetType)
        {
            if (IsConstructorPOCO(columnCount, targetType, out var paramsCons, out var columnIndexes))
            {
                return new ConstructorWithParameters(paramsCons, columnIndexes);
            }

            return null;
        }

        /// <summary>
        /// Returns a constructor, setters, and an array of column indexes that corresponds
        ///   to the setters that takes typed parameters for each of the included columns.
        /// 
        /// Override to tweak behavior.
        /// </summary>
        protected virtual ConstructorWithSetters GetRowConstructorAndSetters(int rowNumber, int columnCount, string[] columnNames, TypeInfo targetType)
        {
            if (columnNames == null) return null;

            if (IsPropertyPOCO(targetType, columnNames, out var zeroCons, out var setters, out var columnIndexes))
            {
                return new ConstructorWithSetters(zeroCons, setters, columnIndexes);
            }

            return null;
        }

        private static bool IsTuple(TypeInfo t)
        {
            if (!t.IsGenericType || t.IsGenericTypeDefinition) return false;

            var genType = t.GetGenericTypeDefinition();
            return Array.IndexOf(Types.TupleTypes, genType) != -1;
        }

        private static bool IsValueTuple(TypeInfo t)
        {
            if (!t.IsGenericType || t.IsGenericTypeDefinition) return false;

            var genType = t.GetGenericTypeDefinition();
            return Array.IndexOf(Types.ValueTupleTypes, genType) != -1;
        }

        private static bool IsConstructorPOCO(int width, TypeInfo type, out ConstructorInfo selectedCons, out int[] columnIndexes)
        {
            foreach (var cons in type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                var consPs = cons.GetParameters();
                if (consPs.Length != width) continue;

                selectedCons = cons;
                columnIndexes = new int[consPs.Length];
                for (var i = 0; i < columnIndexes.Length; i++)
                {
                    columnIndexes[i] = i;
                }

                return true;
            }

            selectedCons = null;
            columnIndexes = null;
            return false;
        }

        private static bool IsPropertyPOCO(TypeInfo type, string[] columnNames, out ConstructorInfo emptyCons, out MethodInfo[] setters, out int[] columnIndexes)
        {
            emptyCons = type.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
            if (emptyCons == null)
            {
                setters = null;
                columnIndexes = null;
                return false;
            }

            var allProperties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

            setters = new MethodInfo[allProperties.Length];
            columnIndexes = new int[allProperties.Length];

            var ix = 0;
            for (var i = 0; i < columnNames.Length; i++)
            {
                var colName = columnNames[i];

                PropertyInfo prop = null;
                for (var j = 0; j < allProperties.Length; j++)
                {
                    var p = allProperties[j];
                    if (p.Name == colName)
                    {
                        prop = p;
                    }
                }

                if (prop == null) continue;

                var setterMtd = prop.SetMethod;
                if (setterMtd == null) continue;

                if (setterMtd.ReturnType.GetTypeInfo() != Types.VoidType) continue;

                if (setterMtd.GetParameters().Length != 1) continue;

                setters[ix] = setterMtd;
                columnIndexes[ix] = i;

                ix++;
            }

            if (ix != setters.Length)
            {
                Array.Resize(ref setters, ix);
                Array.Resize(ref columnIndexes, ix);
            }

            return true;
        }
    }
}