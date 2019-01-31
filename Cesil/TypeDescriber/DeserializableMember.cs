using System;
using System.Collections.Generic;
using System.Reflection;

namespace Cesil
{
    /// <summary>
    /// Represents a member of a type to use when deserializing.
    /// </summary>
    public sealed class DeserializableMember
    {
        private static readonly IReadOnlyDictionary<TypeInfo, MethodInfo> TypeParsers;
        
        static DeserializableMember()
        {
            // load up default parsers
            var ret = new Dictionary<TypeInfo, MethodInfo>();
            foreach (var mtd in Types.DefaultTypeParsersType.GetMethods(BindingFlags.Static | BindingFlags.NonPublic))
            {
                var secondArg = mtd.GetParameters()[1];
                var forType = secondArg.ParameterType.GetElementType().GetTypeInfo();

                ret.Add(forType, mtd);
            }

            TypeParsers = ret;
        }

        /// <summary>
        /// The name of the column that maps to this member.
        /// </summary>
        public string Name { get; }
        
        internal MethodInfo Setter { get; }

        internal FieldInfo Field { get; }

        internal MethodInfo Parser { get; }

        internal bool IsRequired { get; }

        private DeserializableMember(string name, MethodInfo setter, FieldInfo field, MethodInfo parser, bool isRequired)
        {
            Name = name;
            Setter = setter;
            Field = field;
            Parser = parser;
            IsRequired = isRequired;
        }

        /// <summary>
        /// Returns the default parser for the given type, if any exists.
        /// </summary>
        public static MethodInfo GetDefaultParser(TypeInfo forType)
        {
            if (forType.IsEnum)
            {
                if (forType.GetCustomAttribute<FlagsAttribute>() == null)
                {
                    var parsingClass = Types.DefaultEnumTypeParserType.MakeGenericType(forType).GetTypeInfo();
                    var parsingMtd = parsingClass.GetMethod(nameof(DefaultTypeParsers.DefaultEnumTypeParser<StringComparison>.TryParseEnum), BindingFlags.Static | BindingFlags.NonPublic);   // <StringComparison> doesn't matter here

                    return parsingMtd;
                }
                else
                {
                    var parsingClass = Types.DefaultFlagsEnumTypeParserType.MakeGenericType(forType).GetTypeInfo();
                    var parsingMtd = parsingClass.GetMethod(nameof(DefaultTypeParsers.DefaultFlagsEnumTypeParser<StringComparison>.TryParseFlagsEnum), BindingFlags.Static | BindingFlags.NonPublic);   // <StringComparison> doesn't matter here

                    return parsingMtd;
                }
            }

            var nullableElem = Nullable.GetUnderlyingType(forType)?.GetTypeInfo();
            if(nullableElem != null && nullableElem.IsEnum)
            {
                if (nullableElem.GetCustomAttribute<FlagsAttribute>() == null)
                {
                    var parsingClass = Types.DefaultEnumTypeParserType.MakeGenericType(nullableElem).GetTypeInfo();
                    var parsingMtd = parsingClass.GetMethod(nameof(DefaultTypeParsers.DefaultEnumTypeParser<StringComparison>.TryParseNullableEnum), BindingFlags.Static | BindingFlags.NonPublic);   // <StringComparison> doesn't matter here

                    return parsingMtd;
                }
                else
                {
                    var parsingClass = Types.DefaultFlagsEnumTypeParserType.MakeGenericType(nullableElem).GetTypeInfo();
                    var parsingMtd = parsingClass.GetMethod(nameof(DefaultTypeParsers.DefaultFlagsEnumTypeParser<StringComparison>.TryParseNullableFlagsEnum), BindingFlags.Static | BindingFlags.NonPublic);   // <StringComparison> doesn't matter here

                    return parsingMtd;
                }
            }

            if (!TypeParsers.TryGetValue(forType, out var ret))
            {
                return null;
            }

            return ret;
        }

        /// <summary>
        /// Creates a DeserializableMember for the given property.
        /// </summary>
        public static DeserializableMember ForProperty(PropertyInfo property)
        => Create(property.Name, property.SetMethod, GetDefaultParser(property.PropertyType.GetTypeInfo()), false);

        /// <summary>
        /// Creates a DeserializableMember for the given property, with the given name.
        /// </summary>
        public static DeserializableMember ForProperty(PropertyInfo property, string name)
        => Create(name, property.SetMethod, GetDefaultParser(property.PropertyType.GetTypeInfo()), false);

        /// <summary>
        /// Creates a DeserializableMember for the given property, with the given name and parser.
        /// </summary>
        public static DeserializableMember ForProperty(PropertyInfo property, string name, MethodInfo parser)
        => Create(name, property.SetMethod, parser, false);

        /// <summary>
        /// Creates a DeserializableMember for the given property, with the given name, parser, and whether it is required.
        /// </summary>
        public static DeserializableMember ForProperty(PropertyInfo property, string name, MethodInfo parser, bool isRequired)
        => Create(name, property.SetMethod, parser, isRequired);

        /// <summary>
        /// Creates a DeserializableMember for the given field.
        /// </summary>
        public static DeserializableMember ForField(FieldInfo field)
        => Create(field.Name, field, GetDefaultParser(field.FieldType.GetTypeInfo()), false);

        /// <summary>
        /// Creates a DeserializableMember for the given field, with the given name.
        /// </summary>
        public static DeserializableMember ForField(FieldInfo field, string name)
        => Create(name, field, GetDefaultParser(field.FieldType.GetTypeInfo()), false);

        /// <summary>
        /// Creates a DeserializableMember for the given field, with the given name and parser.
        /// </summary>
        public static DeserializableMember ForField(FieldInfo field, string name, MethodInfo parser)
        => Create(name, field, parser, false);

        /// <summary>
        /// Creates a DeserializableMember for the given property, with the given name, parser, and whether it is required.
        /// </summary>
        public static DeserializableMember ForField(FieldInfo field, string name, MethodInfo parser, bool isRequired)
        => Create(name, field, parser, isRequired);

        /// <summary>
        /// Create a DeserializableMember with an explicit name, backing field, parser, and whether it is required.
        /// </summary>
        public static DeserializableMember Create(string name, FieldInfo field, MethodInfo parser, bool isRequired)
        => Create(name, null, field, parser, isRequired);

        /// <summary>
        /// Create a Deserializable member with an explicit name, setter, parser, and whether it is required.
        /// </summary>
        public static DeserializableMember Create(string name, MethodInfo setter, MethodInfo parser, bool isRequired)
        => Create(name, setter, null, parser, isRequired);

        private static DeserializableMember Create(string name, MethodInfo setter, FieldInfo field, MethodInfo parser, bool isRequired)
        {
            if(name == null)
            {
                Throw.ArgumentNullException(nameof(name));
            }
            
            if (field == null && setter == null)
            {
                Throw.InvalidOperation($"At least one of {nameof(field)} and {nameof(setter)} must be non-null");
            }

            if (field != null && setter != null)
            {
                Throw.InvalidOperation($"Only one of {nameof(field)} and {nameof(setter)} can be non-null");
            }

            if(parser == null)
            {
                Throw.ArgumentNullException(nameof(parser));
            }

            if (name.Length == 0)
            {
                Throw.ArgumentException($"{nameof(name)} must be at least 1 character long", nameof(name));
            }

            TypeInfo valueType;

            // setter must take single parameter (the result of parser)
            //   can be instance or static                                  // todo: do we have tests for both?
            //   and cannot return a value
            // -- OR --
            // setter must take two parameters, 
            //    the first is the record value
            //    the second is the value (the result of parser)
            //    cannot return a value
            //    and must be static
            if (setter != null)
            {
                var args = setter.GetParameters();
                if (args.Length == 1)

                {
                    valueType = args[0].ParameterType.GetTypeInfo();

                    var returnsNoValue = setter.ReturnType == Types.VoidType;

                    if (!returnsNoValue)
                    {
                        Throw.ArgumentException($"{nameof(setter)} must not return a value", nameof(setter));
                    }
                }
                else if (args.Length == 2)
                {
                    valueType = args[1].ParameterType.GetTypeInfo();

                    var returnsNoValue = setter.ReturnType == Types.VoidType;

                    if (!returnsNoValue)
                    {
                        Throw.ArgumentException($"{nameof(setter)} must not return a value", nameof(setter));
                    }

                    if (!setter.IsStatic)
                    {
                        Throw.ArgumentException($"{nameof(setter)} taking two parameters must be static", nameof(setter));
                    }
                }
                else
                {
                    Throw.ArgumentException($"{nameof(setter)} must take one or two parameters", nameof(setter));
                    return default; // just for flow control, the above won't actually return
                }
            }
            else
            {
                valueType = field.FieldType.GetTypeInfo();
            }

            // parser must take 
            //   a ReadOnlySpan<char>
            //   have an out parameter of a type assignable to the parameter of setter
            //   and return a boolean
            {
                var args = parser.GetParameters();
                if (args.Length != 2)
                {
                    Throw.ArgumentException($"{nameof(parser)} must have two parameters", nameof(parser));
                }

                var p1 = args[0].ParameterType.GetTypeInfo();
                var p2 = args[1].ParameterType.GetTypeInfo();

                if (p1 != Types.ReadOnlySpanOfCharType)
                {
                    Throw.ArgumentException($"The first parameter of {nameof(parser)} must be a {nameof(ReadOnlySpan<char>)}", nameof(parser));
                }

                if (!p2.IsByRef)
                {
                    Throw.ArgumentException($"The second parameter of {nameof(parser)} must be an out", nameof(parser));
                }

                var underlying = p2.GetElementType().GetTypeInfo();
                if (!valueType.IsAssignableFrom(underlying))
                {
                    Throw.ArgumentException($"The second parameter of {nameof(parser)} must be an out assignable to {valueType.FullName} (the value passed to {nameof(setter)})", nameof(parser));
                }

                var parserRetType = parser.ReturnType.GetTypeInfo();
                if (parserRetType != Types.BoolType)
                {
                    Throw.ArgumentException($"{nameof(parser)} must must return a bool", nameof(parser));
                }
            }

            return new DeserializableMember(name, setter, field, parser, isRequired);
        }

        /// <summary>
        /// Describes this DeserializableMember.
        /// 
        /// This is provided for debugging purposes, and the format is not guaranteed to be stable between releases.
        /// </summary>
        public override string ToString()
        => $"{nameof(Name)}: {Name}\r\n{nameof(Setter)}: {Setter}\r\n{nameof(Field)}: {Field}\r\n{nameof(Parser)}: {Parser}";
    }
}
