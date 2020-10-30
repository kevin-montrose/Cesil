using Microsoft.CodeAnalysis;

// todo: actual track these
#pragma warning disable RS2008

namespace Cesil.SourceGenerator
{
    internal static class Diagnostics
    {
        internal static readonly DiagnosticDescriptor NoCesilReference =
            new DiagnosticDescriptor(
                "CES1000",
                "Missing Cesil Reference",
                "Could not find a type exported by Cesil, are you missing a reference?",
                "Cesil",
                DiagnosticSeverity.Error,
                true
            );

        internal static readonly DiagnosticDescriptor NoGetterOnSerializableProperty =
            new DiagnosticDescriptor(
                "CES1001",
                "Property lacking getter",
                "Serializable properties must declare a getter",
                "Cesil",
                DiagnosticSeverity.Error,
                true
            );

        internal static readonly DiagnosticDescriptor SerializablePropertyCannotHaveParameters =
            new DiagnosticDescriptor(
                "CES1002",
                "Property cannot take parameters",
                "Serializable properties cannot take parameters",
                "Cesil",
                DiagnosticSeverity.Error,
                true
            );

        internal static readonly DiagnosticDescriptor CouldNotExtractConstantValue =
           new DiagnosticDescriptor(
               "CES1003",
               "Constant expression's value could not extracted",
               "Constant expression's value could not extracted",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static readonly DiagnosticDescriptor UnexpectedConstantValueType =
           new DiagnosticDescriptor(
               "CES1004",
               "Constant expression's type was unexpected",
               "Expected a value of type {0}, found {1}",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static readonly DiagnosticDescriptor NameSpecifiedMultipleTimes =
            new DiagnosticDescriptor(
               "CES1005",
               "Member's Name was specified multiple times",
               "Only one attribute may specify Name per member",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static readonly DiagnosticDescriptor EmitDefaultValueSpecifiedMultipleTimes =
            new DiagnosticDescriptor(
               "CES1006",
               "Member's EmitDefaultValue was specified multiple times",
               "Only one attribute may specify EmitDefaultValue per member",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static readonly DiagnosticDescriptor FormatterTypeSpecifiedMultipleTimes =
            new DiagnosticDescriptor(
               "CES1007",
               "Member's FormatterType was specified multiple times",
               "Only one attribute may specify FormatterType per member",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static readonly DiagnosticDescriptor ShouldSerializeTypeSpecifiedMultipleTimes =
            new DiagnosticDescriptor(
               "CES1008",
               "Member's ShouldSerializeType was specified multiple times",
               "Only one attribute may specify ShouldSerializeType per member",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static readonly DiagnosticDescriptor FormatterMethodNameSpecifiedMultipleTimes =
            new DiagnosticDescriptor(
               "CES1009",
               "Member's FormatterMethodName was specified multiple times",
               "Only one attribute may specify FormatterMethodName per member",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static readonly DiagnosticDescriptor ShouldSerializeMethodNameSpecifiedMultipleTimes =
            new DiagnosticDescriptor(
               "CES1010",
               "Member's ShouldSerializeMethodName was specified multiple times",
               "Only one attribute may specify ShouldSerializeMethodName per member",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static readonly DiagnosticDescriptor FormatterBothMustBeSet =
            new DiagnosticDescriptor(
               "CES1011",
               "Both FormatterType and FormatterMethodName must be set",
               "Either both must be set, or neither must be set.  Only one was set.",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static readonly DiagnosticDescriptor ShouldSerializeBothMustBeSet =
            new DiagnosticDescriptor(
               "CES1012",
               "Both ShouldSerializeType and ShouldSerializeMethodName must be set",
               "Either both must be set, or neither must be set.  Only one was set.",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static readonly DiagnosticDescriptor CouldNotFindMethod =
            new DiagnosticDescriptor(
               "CES1013",
               "Could not find method",
               "No method {1} on {0} found.",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static readonly DiagnosticDescriptor MultipleMethodsFound =
            new DiagnosticDescriptor(
               "CES1014",
               "More than one method found",
               "Multiple methods with name {1} on {0} were found.",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static readonly DiagnosticDescriptor MethodNotPublicOrInternal =
            new DiagnosticDescriptor(
               "CES1015",
               "Method not public or internal",
               "Method {0} is not accessible.  It must either be public, or internal and declared in the compiled assembly.",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static readonly DiagnosticDescriptor MethodNotStatic =
            new DiagnosticDescriptor(
               "CES1016",
               "Method not static",
               "Method {0} must be static.",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static readonly DiagnosticDescriptor BadFormatterParameters =
            new DiagnosticDescriptor(
               "CES1016",
               "Invalid Formatter method parameters",
               "Method {0} does not take correct parameters.  Should take {1} (or a type it can be assigned to), in WriteContext, and IBufferWriter<char>.",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static readonly DiagnosticDescriptor MethodCannotBeGeneric =
            new DiagnosticDescriptor(
               "CES1017",
               "Method cannot be generic",
               "Method {0} is generic, which is not supported.",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static readonly DiagnosticDescriptor MethodMustReturnBool =
           new DiagnosticDescriptor(
               "CES1018",
               "Method must return bool",
               "Method {0} does not return bool, but must do so.",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static readonly DiagnosticDescriptor BadShouldSerializeParameters_StaticOne =
           new DiagnosticDescriptor(
               "CES1019",
               "Invalid ShouldSerialize method parameters",
               "Method {0}, which is static and takes one parameter, should take {1} (or a type it can be assigned to).",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static readonly DiagnosticDescriptor BadShouldSerializeParameters_StaticTwo =
           new DiagnosticDescriptor(
               "CES1020",
               "Invalid ShouldSerialize method parameters",
               "Method {0}, which is static and takes two parameters, should take {1} (or a type it can be assigned to), and in WriteContext.",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static readonly DiagnosticDescriptor ShouldSerializeInstanceOnWrongType =
            new DiagnosticDescriptor(
               "CES1021",
               "ShouldSerialize method on wrong type",
               "Method {0}, which is an instance method, is declared on the wrong type (expected declaration on {1}).",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static readonly DiagnosticDescriptor BadShouldSerializeParameters_TooMany =
           new DiagnosticDescriptor(
               "CES1022",
               "Invalid ShouldSerialize method parameters",
               "Method {0} takes too many parameters.",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static readonly DiagnosticDescriptor BadShouldSerializeParameters_InstanceOne =
           new DiagnosticDescriptor(
               "CES1023",
               "Invalid ShouldSerialize method parameters",
               "Method {0}, which is instance and takes one parameter, should take in WriteContext.",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static readonly DiagnosticDescriptor OrderSpecifiedMultipleTimes =
            new DiagnosticDescriptor(
               "CES1024",
               "Member's Order was specified multiple times",
               "Only one attribute may specify Order per member",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static readonly DiagnosticDescriptor MethodMustReturnNonVoid =
            new DiagnosticDescriptor(
               "CES1025",
               "Method cannot return void",
               "Method {0} must return a value, found void.",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static readonly DiagnosticDescriptor SerializableMemberMustHaveNameSetForMethod =
            new DiagnosticDescriptor(
               "CES1026",
               "SerializableMemberAttribute must have Name set",
               "Method {0} with [SerializableMember] must have property Name explicitly set.",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static readonly DiagnosticDescriptor NoSystemMemoryReference =
            new DiagnosticDescriptor(
               "CES1027",
               "Missing system.Memory Reference",
               "Could not find a type exported by System.Memory, are you missing a reference?",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static readonly DiagnosticDescriptor GenericError =
            new DiagnosticDescriptor(
                "CES1999",
                "Unexpected error occurred",
                "Something went wrong: {0}",
                "Cesil",
                DiagnosticSeverity.Error,
                true
            );
    }
}
