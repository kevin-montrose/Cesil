# Cesil
Modern CSV (De)Serializer

# PRE-RELEASE

This code isn't well tested yet, YOU PROBABLY DON'T WANT TO USE IT!!!

# Configuration

Before (de)serializing, you must configure a `BoundConfiguration<T>` with some options.

Default options will be used if you just use `Configuration.For<T>()`.

Custom options can be built with an `OptionsBuilder`, to base on existing `Options` call `Options.NewBuilder()` 
(ie. `Options.Default.NewBuilder()` will create an `OptionsBuilder` with default options pre-populated).  Call
`.Build()` on an `OptionsBuilder` to create an `Options` to pass to `Configuration.For<T>`.

You can configure:

 - Value separate character (typically `,`)
 - Escaped value start/end character (typically `"`)
 - Escape start character (used in escaped values, typically also `"`)
 - Row ending character sequence (typically `\r\n`)
 - Whether to expect headers
 - Whether to write headers
 - Whether to write a trailing new line after the last row
 - Comment start character, if any (typically not set, but if set typically `#`)
 - `MemoryPool<char>` to use for allocations
 - A buffer size hint for writing
 - A buffer size hint for reading
 - A custom `ITypeDescriber` for determining columns to read and write
 
## Buffer Size Hints

Buffer size hints are only taken as guidance, allocations in excess of the hints may still occur.

If `WriteBufferSizeHint` is set to `0`, writes will be written directly to the provided `TextWriter` - this typically slows performance, 
but may reduce overall allocations.

If `ReadBufferSizeHint` is set to `0`, Cesil will try to use a single-page of buffer for reading.

## ITypeDescriber

The two methods on `ITypeDescriber` (`EnumerateMembersToSerialize` and `EnumerateMembersToDeserialize`) are used to discover which members
to de(serialize) on a type.  By default, the instance of `DefaultTypeDesciber` in `TypeDescribers.Default` is used.

The default type describer (de)serializes public properties, honors `[DataMember]`, and looks for `ShouldSerializeXXX()` methods.

!!TODO: Reset methods?!!

`ManualTypeDescriber` and `SurrogateTypeDescriber` are provided for the cases where you want to explicitly add each member to be
(de)serialized or when you want to act as if the type being serialized was in fact another (surrogate) type.

### Serializing

`SerializableMember`s have a name (which is the column name), a getter or a field, a formatter, an optional "should serialize" method, and
whether or not to emit a default value.

#### Getters

Getters must be either instance methods on the serialized type (or a type it can be assigned to), or a static method.  If a static method
a getter can take a single parameter, which is of the type being serialized (or a type it can be assigned to).

Getters must return a type that can be passed to it's paired formatter.

#### Formatters

Formatters must be static methods that take a type to be serialized, an `IBufferWriter<char>`, and return a bool.

A formatter should return false if it cannot format the given value into the given `IBufferWriter<char>`.


#### Should Serialize

Should serialize methods are optional.  If present, they can be instance methods or static methods.  They must return a boolean.

If instance methods, they must be on the type being serialized or on a type it can be assigned to.

If a should serialize method is configured, and it returns false at serialization time, the value will not be emitted.

### Deserializing

`DeserializableMember`s have a name (which is the column name), a setter or a field, a parser, and whether or not the column is required.

### Setters

A setter can be either an instance method or a static method.  It may not return a value.

If a setter is an instance method, it must take a single value of the type returned by the paired parser.

If a static method, it can take one or two values.  If it takes values, the first value must be of the type being deserialized (or one it
is assignable to) and the second value must be the type returned by the paired parser.

### Parsers

Parsers must be a static method, and must have two parameters - the first being a `ReadOnlySpan<char>` and the second being an `out T` where
T is the type of the paired field or a value passed to the paired setter.