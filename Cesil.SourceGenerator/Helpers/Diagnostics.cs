using System.Linq;
using Microsoft.CodeAnalysis;

// todo: actual track these
#pragma warning disable RS2008

namespace Cesil.SourceGenerator
{
    internal static class Diagnostics
    {
        private static readonly DiagnosticDescriptor _NoCesilReference =
            new DiagnosticDescriptor(
                "CES1000",
                "Missing Cesil Reference",
                "Could not find a type exported by Cesil, are you missing a reference?",
                "Cesil",
                DiagnosticSeverity.Error,
                true
            );

        internal static Diagnostic NoCesilReference(Location? loc)
        => Diagnostic.Create(_NoCesilReference, loc);

        private static readonly DiagnosticDescriptor _NoGetterOnSerializableProperty =
            new DiagnosticDescriptor(
                "CES1001",
                "Property lacking getter",
                "Serializable properties must declare a getter",
                "Cesil",
                DiagnosticSeverity.Error,
                true
            );

        internal static Diagnostic NoGetterOnSerializableProperty(Location? loc)
        => Diagnostic.Create(_NoGetterOnSerializableProperty, loc);


        private static readonly DiagnosticDescriptor _SerializablePropertyCannotHaveParameters =
            new DiagnosticDescriptor(
                "CES1002",
                "Property cannot take parameters",
                "Serializable properties cannot take parameters",
                "Cesil",
                DiagnosticSeverity.Error,
                true
            );

        internal static Diagnostic SerializablePropertyCannotHaveParameters(Location? loc)
        => Diagnostic.Create(_SerializablePropertyCannotHaveParameters, loc);

        private static readonly DiagnosticDescriptor _CouldNotExtractConstantValue =
           new DiagnosticDescriptor(
               "CES1003",
               "Constant expression's value could not extracted",
               "Constant expression's value could not extracted",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic CouldNotExtractConstantValue(Location? loc)
        => Diagnostic.Create(_CouldNotExtractConstantValue, loc);

        private static readonly DiagnosticDescriptor _UnexpectedConstantValueType =
           new DiagnosticDescriptor(
               "CES1004",
               "Constant expression's type was unexpected",
               "Expected a value of type {0}, found {1}",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic UnexpectedConstantValueType(Location? loc, System.Reflection.TypeInfo[] expected, System.Reflection.TypeInfo? found)
        {
            var foundName = found?.Name ?? "--UNKNOWN--";

            var expectedStr = string.Join(", or ", expected.Select(s => s.Name));

            return Diagnostic.Create(_UnexpectedConstantValueType, loc, expectedStr, foundName);
        }

        private static readonly DiagnosticDescriptor _NameSpecifiedMultipleTimes =
            new DiagnosticDescriptor(
               "CES1005",
               "Member's Name was specified multiple times",
               "Only one attribute may specify Name per member",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic NameSpecifiedMultipleTimes(Location? loc)
        => Diagnostic.Create(_NameSpecifiedMultipleTimes, loc);

        private static readonly DiagnosticDescriptor _EmitDefaultValueSpecifiedMultipleTimes =
            new DiagnosticDescriptor(
               "CES1006",
               "Member's EmitDefaultValue was specified multiple times",
               "Only one attribute may specify EmitDefaultValue per member",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic EmitDefaultValueSpecifiedMultipleTimes(Location? loc)
        => Diagnostic.Create(_EmitDefaultValueSpecifiedMultipleTimes, loc);

        private static readonly DiagnosticDescriptor _FormatterTypeSpecifiedMultipleTimes =
            new DiagnosticDescriptor(
               "CES1007",
               "Member's FormatterType was specified multiple times",
               "Only one attribute may specify FormatterType per member",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic FormatterTypeSpecifiedMultipleTimes(Location? loc)
        => Diagnostic.Create(_FormatterTypeSpecifiedMultipleTimes, loc);

        private static readonly DiagnosticDescriptor _ShouldSerializeTypeSpecifiedMultipleTimes =
            new DiagnosticDescriptor(
               "CES1008",
               "Member's ShouldSerializeType was specified multiple times",
               "Only one attribute may specify ShouldSerializeType per member",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic ShouldSerializeTypeSpecifiedMultipleTimes(Location? loc)
        => Diagnostic.Create(_ShouldSerializeTypeSpecifiedMultipleTimes, loc);

        private static readonly DiagnosticDescriptor _FormatterMethodNameSpecifiedMultipleTimes =
            new DiagnosticDescriptor(
               "CES1009",
               "Member's FormatterMethodName was specified multiple times",
               "Only one attribute may specify FormatterMethodName per member",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic FormatterMethodNameSpecifiedMultipleTimes(Location? loc)
        => Diagnostic.Create(_FormatterMethodNameSpecifiedMultipleTimes, loc);

        private static readonly DiagnosticDescriptor _ShouldSerializeMethodNameSpecifiedMultipleTimes =
            new DiagnosticDescriptor(
               "CES1010",
               "Member's ShouldSerializeMethodName was specified multiple times",
               "Only one attribute may specify ShouldSerializeMethodName per member",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic ShouldSerializeMethodNameSpecifiedMultipleTimes(Location? loc)
        => Diagnostic.Create(_ShouldSerializeMethodNameSpecifiedMultipleTimes, loc);

        private static readonly DiagnosticDescriptor _FormatterBothMustBeSet =
            new DiagnosticDescriptor(
               "CES1011",
               "Both FormatterType and FormatterMethodName must be set",
               "Either both must be set, or neither must be set.  Only one was set.",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic FormatterBothMustBeSet(Location? loc)
        => Diagnostic.Create(_FormatterBothMustBeSet, loc);

        private static readonly DiagnosticDescriptor _ShouldSerializeBothMustBeSet =
            new DiagnosticDescriptor(
               "CES1012",
               "Both ShouldSerializeType and ShouldSerializeMethodName must be set",
               "Either both must be set, or neither must be set.  Only one was set.",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic ShouldSerializeBothMustBeSet(Location? loc)
        => Diagnostic.Create(_ShouldSerializeBothMustBeSet, loc);

        private static readonly DiagnosticDescriptor _CouldNotFindMethod =
            new DiagnosticDescriptor(
               "CES1013",
               "Could not find method",
               "No method {1} on {0} found.",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic CouldNotFindMethod(Location? loc, string method, string type)
        => Diagnostic.Create(_CouldNotFindMethod, loc, method, type);

        private static readonly DiagnosticDescriptor _MultipleMethodsFound =
            new DiagnosticDescriptor(
               "CES1014",
               "More than one method found",
               "Multiple methods with name {1} on {0} were found.",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic MultipleMethodsFound(Location? loc, string type, string method)
        => Diagnostic.Create(_MultipleMethodsFound, loc, type, method);

        private static readonly DiagnosticDescriptor _MethodNotPublicOrInternal =
            new DiagnosticDescriptor(
               "CES1015",
               "Method not public or internal",
               "Method {0} is not accessible.  It must either be public, or internal and declared in the compiled assembly.",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic MethodNotPublicOrInternal(Location? loc, IMethodSymbol method)
        => Diagnostic.Create(_MethodNotPublicOrInternal, loc, method.Name);

        private static readonly DiagnosticDescriptor _MethodNotStatic =
            new DiagnosticDescriptor(
               "CES1016",
               "Method not static",
               "Method {0} must be static.",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic MethodNotStatic(Location? loc, IMethodSymbol method)
        => Diagnostic.Create(_MethodNotStatic, loc, method.Name);

        private static readonly DiagnosticDescriptor _BadFormatterParameters =
            new DiagnosticDescriptor(
               "CES1016",
               "Invalid Formatter method parameters",
               "Method {0} does not take correct parameters.  Should take {1} (or a type it can be assigned to), in WriteContext, and IBufferWriter<char>.",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic BadFormatterParameters(Location? loc, IMethodSymbol method, ITypeSymbol take)
        => Diagnostic.Create(_BadFormatterParameters, loc, method.Name, take.Name);

        private static readonly DiagnosticDescriptor _MethodCannotBeGeneric =
            new DiagnosticDescriptor(
               "CES1017",
               "Method cannot be generic",
               "Method {0} is generic, which is not supported.",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic MethodCannotBeGeneric(Location? loc, IMethodSymbol method)
        => Diagnostic.Create(_MethodCannotBeGeneric, loc, method.Name);

        private static readonly DiagnosticDescriptor _MethodMustReturnBool =
           new DiagnosticDescriptor(
               "CES1018",
               "Method must return bool",
               "Method {0} does not return bool, but must do so.",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic MethodMustReturnBool(Location? loc, IMethodSymbol method)
        => Diagnostic.Create(_MethodMustReturnBool, loc, method.Name);

        private static readonly DiagnosticDescriptor _BadShouldSerializeParameters_StaticOne =
           new DiagnosticDescriptor(
               "CES1019",
               "Invalid ShouldSerialize method parameters",
               "Method {0}, which is static and takes one parameter, should take {1} (or a type it can be assigned to).",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic BadShouldSerializeParameters_StaticOne(Location? loc, IMethodSymbol method, ITypeSymbol take)
        => Diagnostic.Create(_BadShouldSerializeParameters_StaticOne, loc, method.Name, take.Name);

        private static readonly DiagnosticDescriptor _BadShouldSerializeParameters_StaticTwo =
           new DiagnosticDescriptor(
               "CES1020",
               "Invalid ShouldSerialize method parameters",
               "Method {0}, which is static and takes two parameters, should take {1} (or a type it can be assigned to), and in WriteContext.",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic BadShouldSerializeParameters_StaticTwo(Location? loc, IMethodSymbol method, ITypeSymbol take)
        => Diagnostic.Create(_BadShouldSerializeParameters_StaticTwo, loc, method.Name, take.Name);

        private static readonly DiagnosticDescriptor _ShouldSerializeInstanceOnWrongType =
            new DiagnosticDescriptor(
               "CES1021",
               "ShouldSerialize method on wrong type",
               "Method {0}, which is an instance method, is declared on the wrong type (expected declaration on {1}).",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic ShouldSerializeInstanceOnWrongType(Location? loc, IMethodSymbol method, ITypeSymbol expectedOn)
        => Diagnostic.Create(_ShouldSerializeInstanceOnWrongType, loc, method.Name, expectedOn.Name);

        private static readonly DiagnosticDescriptor _BadShouldSerializeParameters_TooMany =
           new DiagnosticDescriptor(
               "CES1022",
               "Invalid ShouldSerialize method parameters",
               "Method {0} takes too many parameters.",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic BadShouldSerializeParameters_TooMany(Location? loc, IMethodSymbol method)
        => Diagnostic.Create(_BadShouldSerializeParameters_TooMany, loc, method.Name);

        private static readonly DiagnosticDescriptor _BadShouldSerializeParameters_InstanceOne =
           new DiagnosticDescriptor(
               "CES1023",
               "Invalid ShouldSerialize method parameters",
               "Method {0}, which is instance and takes one parameter, should take in WriteContext.",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic BadShouldSerializeParameters_InstanceOne(Location? loc, IMethodSymbol method)
        => Diagnostic.Create(_BadShouldSerializeParameters_InstanceOne, loc, method.Name);

        private static readonly DiagnosticDescriptor _OrderSpecifiedMultipleTimes =
            new DiagnosticDescriptor(
               "CES1024",
               "Member's Order was specified multiple times",
               "Only one attribute may specify Order per member",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic OrderSpecifiedMultipleTimes(Location? loc)
        => Diagnostic.Create(_OrderSpecifiedMultipleTimes, loc);

        private static readonly DiagnosticDescriptor _MethodMustReturnNonVoid =
            new DiagnosticDescriptor(
               "CES1025",
               "Method cannot return void",
               "Method {0} must return a value, found void.",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic MethodMustReturnNonVoid(Location? loc, IMethodSymbol method)
        => Diagnostic.Create(_MethodMustReturnNonVoid, loc, method.Name);

        private static readonly DiagnosticDescriptor _SerializableMemberMustHaveNameSetForMethod =
            new DiagnosticDescriptor(
               "CES1026",
               "SerializableMemberAttribute must have Name set",
               "Method {0} with [SerializableMember] must have property Name explicitly set.",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic SerializableMemberMustHaveNameSetForMethod(Location? loc, IMethodSymbol method)
        => Diagnostic.Create(_SerializableMemberMustHaveNameSetForMethod, loc, method.Name);

        private static readonly DiagnosticDescriptor _NoSystemMemoryReference =
            new DiagnosticDescriptor(
               "CES1027",
               "Missing system.Memory Reference",
               "Could not find a type exported by System.Memory, are you missing a reference?",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic NoSystemMemoryReference(Location? loc)
        => Diagnostic.Create(_NoSystemMemoryReference, loc);

        private static readonly DiagnosticDescriptor _BadGetterParameters_StaticOne =
           new DiagnosticDescriptor(
               "CES1028",
               "Invalid Getter method parameters",
               "Method {0}, which is static and takes one parameter, should take {1} (or a type it can be assigned to), or in WriteContext.",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic BadGetterParameters_StaticOne(Location? loc, IMethodSymbol method, ITypeSymbol take)
        => Diagnostic.Create(_BadGetterParameters_StaticOne, loc, method.Name, take.Name);

        private static readonly DiagnosticDescriptor _BadGetterParameters_StaticTwo =
           new DiagnosticDescriptor(
               "CES1029",
               "Invalid Getter method parameters",
               "Method {0}, which is static and takes two parameters, should take {1} (or a type it can be assigned to) and in WriteContext.",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic BadGetterParameters_StaticTwo(Location? loc, IMethodSymbol method, ITypeSymbol take)
        => Diagnostic.Create(_BadGetterParameters_StaticTwo, loc, method.Name, take.Name);

        private static readonly DiagnosticDescriptor _BadGetterParameters_TooMany =
           new DiagnosticDescriptor(
               "CES1030",
               "Invalid Getter method parameters",
               "Method {0} takes too many parameters.",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic BadGetterParameters_TooMany(Location? loc, IMethodSymbol method)
        => Diagnostic.Create(_BadGetterParameters_TooMany, loc, method.Name);

        private static readonly DiagnosticDescriptor _BadGetterParameters_InstanceOne =
           new DiagnosticDescriptor(
               "CES1031",
               "Invalid Getter method parameters",
               "Method {0}, which is an instance method and takes one parameter, should in WriteContext.",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic BadGetterParameters_InstanceOne(Location? loc, IMethodSymbol method)
        => Diagnostic.Create(_BadGetterParameters_InstanceOne, loc, method.Name);

        private static readonly DiagnosticDescriptor _NoBuiltInFormatter =
            new DiagnosticDescriptor(
               "CES1032",
               "No default Formatter",
               "There is no default Formatter for {0}, you must provide one.",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic NoBuiltInFormatter(Location? loc, ITypeSymbol type)
        => Diagnostic.Create(_NoBuiltInFormatter, loc, type.Name);

        private static readonly DiagnosticDescriptor _IsRequiredSpecifiedMultipleTimes =
            new DiagnosticDescriptor(
               "CES1033",
               "Member's IsRequired was specified multiple times",
               "Only one attribute may specify IsRequired per member",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic IsRequiredSpecifiedMultipleTimes(Location? loc)
        => Diagnostic.Create(_IsRequiredSpecifiedMultipleTimes, loc);

        private static readonly DiagnosticDescriptor _BadSetterParameters_TooFew =
            new DiagnosticDescriptor(
               "CES1035",
               "Invalid Setter method parameters",
               "Method {0} takes too few parameters, expected it to take at least a parsed value",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic BadSetterParameters_TooFew(Location? loc, IMethodSymbol method)
        => Diagnostic.Create(_BadSetterParameters_TooFew, loc, method.Name);

        private static readonly DiagnosticDescriptor _MethodMustReturnVoid =
            new DiagnosticDescriptor(
               "CES1036",
               "Method must return void",
               "Method {0} must return void, but returns a value.",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic MethodMustReturnVoid(Location? loc, IMethodSymbol method)
        => Diagnostic.Create(_MethodMustReturnVoid, loc, method.Name);

        private static readonly DiagnosticDescriptor _BadSetterParameters_StaticOne =
            new DiagnosticDescriptor(
               "CES1037",
               "Invalid Setter method parameters",
               "Method {0}, which is static and takes one parameter, should take a value (not by reference).",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic BadSetterParameters_StaticOne(Location? loc, IMethodSymbol method)
        => Diagnostic.Create(_BadSetterParameters_StaticOne, loc, method.Name);

        private static readonly DiagnosticDescriptor _BadSetterParameters_StaticTwo =
            new DiagnosticDescriptor(
               "CES1038",
               "Invalid Setter method parameters",
               "Method {0}, which is static and takes two parameters, should take either a value and `in ReadContext` or {1} (possible by ref) and a value.",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic BadSetterParameters_StaticTwo(Location? loc, IMethodSymbol method, ITypeSymbol take)
        => Diagnostic.Create(_BadSetterParameters_StaticTwo, loc, method.Name, take.Name);

        private static readonly DiagnosticDescriptor _BadSetterParameters_StaticThree =
            new DiagnosticDescriptor(
               "CES1039",
               "Invalid Setter method parameters",
               "Method {0}, which is static and takes three parameters, should take {1} (possible by ref), a value, and an `in ReadContext`.",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic BadSetterParameters_StaticThree(Location? loc, IMethodSymbol method, ITypeSymbol take)
        => Diagnostic.Create(_BadSetterParameters_StaticThree, loc, method.Name, take.Name);

        private static readonly DiagnosticDescriptor _BadSetterParameters_TooMany =
            new DiagnosticDescriptor(
               "CES1040",
               "Invalid Setter method parameters",
               "Method {0} takes too many parameters, expect at most 2 for instance method and 3 for static methods.",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic BadSetterParameters_TooMany(Location? loc, IMethodSymbol method)
        => Diagnostic.Create(_BadSetterParameters_TooMany, loc, method.Name);

        private static readonly DiagnosticDescriptor _BadSetter_NotOnRow =
             new DiagnosticDescriptor(
               "CES1041",
               "Invalid Setter method",
               "Method {0} is an instance method, and must be invokable on the row type ({1}).",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic BadSetter_NotOnRow(Location? loc, IMethodSymbol method, ITypeSymbol type)
        => Diagnostic.Create(_BadSetter_NotOnRow, loc, method.Name, type.Name);

        private static readonly DiagnosticDescriptor _BadSetterParameters_InstanceOne =
            new DiagnosticDescriptor(
               "CES1042",
               "Invalid Setter method parameters",
               "Method {0}, which is instance and takes one parameter, should take a value (not by ref).",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic BadSetterParameters_InstanceOne(Location? loc, IMethodSymbol method)
        => Diagnostic.Create(_BadSetterParameters_InstanceOne, loc, method.Name);

        private static readonly DiagnosticDescriptor _BadSetterParameters_InstanceTwo =
            new DiagnosticDescriptor(
               "CES1043",
               "Invalid Setter method parameters",
               "Method {0}, which is instance and takes two parameters, should take a value (not by ref) and `in ReadContext`.",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic BadSetterParameters_InstanceTwo(Location? loc, IMethodSymbol method)
        => Diagnostic.Create(_BadSetterParameters_InstanceTwo, loc, method.Name);

        private static readonly DiagnosticDescriptor _ResetTypeSpecifiedMultipleTimes =
            new DiagnosticDescriptor(
               "CES1044",
               "Member's ResetType was specified multiple times",
               "Only one attribute may specify ResetType per member",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic ResetTypeSpecifiedMultipleTimes(Location? loc)
        => Diagnostic.Create(_ResetTypeSpecifiedMultipleTimes, loc);

        private static readonly DiagnosticDescriptor _ParserTypeSpecifiedMultipleTimes =
            new DiagnosticDescriptor(
               "CES1045",
               "Member's ParserType was specified multiple times",
               "Only one attribute may specify ParserType per member",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic ParserTypeSpecifiedMultipleTimes(Location? loc)
        => Diagnostic.Create(_ParserTypeSpecifiedMultipleTimes, loc);

        private static readonly DiagnosticDescriptor _ResetMethodNameSpecifiedMultipleTimes =
            new DiagnosticDescriptor(
               "CES1046",
               "Member's ResetMethodName was specified multiple times",
               "Only one attribute may specify ResetMethodName per member",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic ResetMethodNameSpecifiedMultipleTimes(Location? loc)
        => Diagnostic.Create(_ResetMethodNameSpecifiedMultipleTimes, loc);

        private static readonly DiagnosticDescriptor _ParserMethodNameSpecifiedMultipleTimes =
            new DiagnosticDescriptor(
               "CES1047",
               "Member's ParserMethodName was specified multiple times",
               "Only one attribute may specify ParserMethodName per member",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic ParserMethodNameSpecifiedMultipleTimes(Location? loc)
        => Diagnostic.Create(_ParserMethodNameSpecifiedMultipleTimes, loc);

        private static readonly DiagnosticDescriptor _ResetBothMustBeSet =
            new DiagnosticDescriptor(
               "CES1048",
               "Both ResetType and ResetMethodName must be set",
               "Either both must be set, or neither must be set.  Only one was set.",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic ResetBothMustBeSet(Location? loc)
        => Diagnostic.Create(_ResetBothMustBeSet, loc);

        private static readonly DiagnosticDescriptor _ParserBothMustBeSet =
            new DiagnosticDescriptor(
               "CES1049",
               "Both ParserType and ParserMethodName must be set",
               "Either both must be set, or neither must be set.  Only one was set.",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic ParserBothMustBeSet(Location? loc)
        => Diagnostic.Create(_ParserBothMustBeSet, loc);

        private static readonly DiagnosticDescriptor _BadResetParameters_StaticOne =
            new DiagnosticDescriptor(
               "CES1050",
               "Invalid Reset method parameters",
               "Method {0}, which is static and takes one parameter, must take an `in ReadContext` or the row type ({1}).",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic BadResetParameters_StaticOne(Location? loc, IMethodSymbol method, ITypeSymbol rowType)
        => Diagnostic.Create(_BadResetParameters_StaticOne, loc, method.Name, rowType.Name);

        private static readonly DiagnosticDescriptor _BadResetParameters_StaticTwo =
            new DiagnosticDescriptor(
               "CES1051",
               "Invalid Reset method parameters",
               "Method {0}, which is static and takes two parameters, must take the row type ({1}, potentially by ref) and `in ReadContext`.",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic BadResetParameters_StaticTwo(Location? loc, IMethodSymbol method, ITypeSymbol rowType)
        => Diagnostic.Create(_BadResetParameters_StaticTwo, loc, method.Name, rowType.Name);

        private static readonly DiagnosticDescriptor _BadResetParameters_InstanceOne =
            new DiagnosticDescriptor(
               "CES1050",
               "Invalid Reset method parameters",
               "Method {0}, which is instance and takes one parameter, must take an `in ReadContext`.",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic BadResetParameters_InstanceOne(Location? loc, IMethodSymbol method)
        => Diagnostic.Create(_BadResetParameters_InstanceOne, loc, method.Name);

        private static readonly DiagnosticDescriptor _BadResetParameters_TooMany =
            new DiagnosticDescriptor(
               "CES1051",
               "Invalid Reset method parameters",
               "Method {0} takes too many parameters, expect at most 1 for instance methods and 2 for static methods.",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic BadResetParameters_TooMany(Location? loc, IMethodSymbol method)
        => Diagnostic.Create(_BadResetParameters_TooMany, loc, method.Name);

        private static readonly DiagnosticDescriptor _BadParserParameters =
            new DiagnosticDescriptor(
               "CES1052",
               "Invalid Parser method parameters",
               "Method {0} must take a ReadOnlySpan<char>, an `in ReadContext`, and produce an out value.",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic BadParserParameters(Location? loc, IMethodSymbol method)
        => Diagnostic.Create(_BadParserParameters, loc, method.Name);

        private static readonly DiagnosticDescriptor _NoSetterOnDeserializableProperty =
            new DiagnosticDescriptor(
                "CES1053",
                "Property lacking setter",
                "Deserializable properties must declare a setter",
                "Cesil",
                DiagnosticSeverity.Error,
                true
            );

        internal static Diagnostic NoSetterOnDeserializableProperty(Location? loc)
        => Diagnostic.Create(_NoSetterOnDeserializableProperty, loc);

        private static readonly DiagnosticDescriptor _DeserializablePropertyCannotHaveParameters =
            new DiagnosticDescriptor(
                "CES1054",
                "Property cannot take parameters",
                "Deserializable properties cannot take parameters",
                "Cesil",
                DiagnosticSeverity.Error,
                true
            );

        internal static Diagnostic DeserializablePropertyCannotHaveParameters(Location? loc)
        => Diagnostic.Create(_DeserializablePropertyCannotHaveParameters, loc);

        private static readonly DiagnosticDescriptor _DeserializableMemberMustHaveNameSetForMethod =
            new DiagnosticDescriptor(
               "CES1055",
               "DeserializableMemberAttribute must have Name set",
               "Method {0} with [DeserializableMember] must have property Name explicitly set.",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic DeserializableMemberMustHaveNameSetForMethod(Location? loc, IMethodSymbol method)
        => Diagnostic.Create(_DeserializableMemberMustHaveNameSetForMethod, loc, method.Name);

        private static readonly DiagnosticDescriptor _BadReset_NotOnRow =
             new DiagnosticDescriptor(
               "CES1056",
               "Invalid Reset method",
               "Method {0} is an instance method, and must be invokable on the row type ({1}).",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic BadReset_NotOnRow(Location? loc, IMethodSymbol method, ITypeSymbol type)
        => Diagnostic.Create(_BadReset_NotOnRow, loc, method.Name, type.Name);

        private static readonly DiagnosticDescriptor _NoBuiltInParser =
            new DiagnosticDescriptor(
               "CES1057",
               "No default Parser",
               "There is no default Parser for {0}, you must provide one.",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic NoBuiltInParser(Location? loc, ITypeSymbol type)
        => Diagnostic.Create(_NoBuiltInParser, loc, type.Name);

        private static readonly DiagnosticDescriptor _InstanceProviderTypeSpecifiedMultipleTimes =
            new DiagnosticDescriptor(
               "CES1058",
               "InstanceProviderType was specified multiple times",
               "InstanceProviderType may only be specified once",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic InstanceProviderTypeSpecifiedMultipleTimes(Location? loc)
        => Diagnostic.Create(_InstanceProviderTypeSpecifiedMultipleTimes, loc);

        private static readonly DiagnosticDescriptor _InstanceProviderMethodNameSpecifiedMultipleTimes =
            new DiagnosticDescriptor(
               "CES1059",
               "InstanceProviderMethodName was specified multiple times",
               "InstanceProviderMethodName may only be specified once",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic InstanceProviderMethodNameSpecifiedMultipleTimes(Location? loc)
        => Diagnostic.Create(_InstanceProviderMethodNameSpecifiedMultipleTimes, loc);

        private static readonly DiagnosticDescriptor _InstanceProviderBothMustBeSet =
            new DiagnosticDescriptor(
               "CES1060",
               "Both InstanceProviderType and InstanceProviderMethodName must be set",
               "Either both must be set, or neither must be set.  Only one was set.",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic InstanceProviderBothMustBeSet(Location? loc)
        => Diagnostic.Create(_InstanceProviderBothMustBeSet, loc);

        private static readonly DiagnosticDescriptor _BadInstanceProviderParameters =
            new DiagnosticDescriptor(
               "CES1061",
               "Invalid InstanceProvider method parameters",
               "Method {0} must take an `in ReadContext`, and produce an out value assignable to the attributed type.",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic BadInstanceProviderParameters(Location? loc, IMethodSymbol method)
        => Diagnostic.Create(_BadInstanceProviderParameters, loc, method.Name);

        private static readonly DiagnosticDescriptor _NoInstanceProvider =
            new DiagnosticDescriptor(
               "CES1062",
               "No InstanceProvider configured",
               "Type {0} does not have an InstanceProvider.  A type must have an accessible parameterless constructor, or an explicit InstanceProvider configured with InstanceProviderType and InstanceProviderMethodName on its [GenerateDeserializable].",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic NoInstanceProvider(Location? loc, ITypeSymbol type)
        => Diagnostic.Create(_NoInstanceProvider, loc, type.Name);

        private static readonly DiagnosticDescriptor _InstanceProviderConstructorAndMethodProvided =
            new DiagnosticDescriptor(
               "CES1063",
               "Both method and constructor InstanceProvider specified",
               "Type {0} has a constructor marked as an InstanceProvider, and a method provided as an InstanceProvider.  Only one may be specified.",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static Diagnostic InstanceProviderConstructorAndMethodProvided(Location? loc, ITypeSymbol type)
        => Diagnostic.Create(_InstanceProviderConstructorAndMethodProvided, loc, type.Name);

        private static readonly DiagnosticDescriptor _DeserializableMemberOnNonConstructorParameter =
        new DiagnosticDescriptor(
           "CES1064",
           "[GenerateDeserializableMemberAttribute] applied to non-constructor parameter",
           "Type {0}'s method {1} has parameter with [GenerateDeserializableMemberAttribute], which is not permitted.",
           "Cesil",
           DiagnosticSeverity.Error,
           true
       );

        internal static Diagnostic DeserializableMemberOnNonConstructorParameter(Location? loc, ITypeSymbol type, IMethodSymbol mtd)
        => Diagnostic.Create(_DeserializableMemberOnNonConstructorParameter, loc, type.Name, mtd.Name);

        private static readonly DiagnosticDescriptor _ConstructorHasMembersButIsntInstanceProvider =
        new DiagnosticDescriptor(
           "CES1065",
           "[GenerateDeserializableMemberAttribute] applied to constructor parameters, but constructor isn't annotated with [GenerateDeserializableInstanceProviderAttribute].",
           "Type {0} has constructor with annotated constructor parameters, but constructor is not an InstanceProvider.",
           "Cesil",
           DiagnosticSeverity.Error,
           true
       );

        internal static Diagnostic ConstructorHasMembersButIsntInstanceProvider(Location? loc, ITypeSymbol type)
        => Diagnostic.Create(_ConstructorHasMembersButIsntInstanceProvider, loc, type.Name);

        private static readonly DiagnosticDescriptor _AllConstructorParametersMustBeMembers =
        new DiagnosticDescriptor(
           "CES1066",
           "All parameters of [GenerateDeserializableInstanceProviderAttribute] constructor must be annotated with [GenerateDeserializableMemberAttribute].",
           "All of type {0}'s InstanceProvider constructor's parameters must be annotated with [GenerateDeserializableMemberAttribute].",
           "Cesil",
           DiagnosticSeverity.Error,
           true
       );

        internal static Diagnostic AllConstructorParametersMustBeMembers(Location? loc, ITypeSymbol type)
        => Diagnostic.Create(_AllConstructorParametersMustBeMembers, loc, type.Name);

        private static readonly DiagnosticDescriptor _ParametersMustBeRequired =
        new DiagnosticDescriptor(
           "CES1067",
           "Parameter cannot be optional, MemberRequired or IsRequired must be absent, Yes, or true.",
           "Parameter {1} on type {0}'s constructor cannot be optional.",
           "Cesil",
           DiagnosticSeverity.Error,
           true
       );

        internal static Diagnostic ParametersMustBeRequired(Location? loc, ITypeSymbol type, IParameterSymbol parameter)
        => Diagnostic.Create(_ParametersMustBeRequired, loc, type.Name, parameter.Name);

        private static readonly DiagnosticDescriptor _BadReset_MustBeStaticForParameters =
        new DiagnosticDescriptor(
           "CES1068",
           "For constructor parameters, Reset methods must be static",
           "Parameter {1} on type {0}'s constructor has a non-static Reset method {2}.",
           "Cesil",
           DiagnosticSeverity.Error,
           true
       );

        internal static Diagnostic BadReset_MustBeStaticForParameters(Location? loc, ITypeSymbol type, IParameterSymbol parameter, IMethodSymbol method)
        => Diagnostic.Create(_BadReset_MustBeStaticForParameters, loc, type.Name, parameter.Name, method.Name);

        private static readonly DiagnosticDescriptor _GenericError =
            new DiagnosticDescriptor(
                "CES1999",
                "Unexpected error occurred",
                "Something went wrong: {0}",
                "Cesil",
                DiagnosticSeverity.Error,
                true
            );

        internal static Diagnostic GenericError(Location? loc, string message)
        => Diagnostic.Create(_GenericError, loc, message);
    }
}
