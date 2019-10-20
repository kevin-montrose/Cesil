# Cesil
Modern CSV (De)Serializer

# PRE-RELEASE

This code isn't well tested yet, **YOU PROBABLY DON'T WANT TO USE IT!!!**

# Configuration

Before (de)serializing, you must configure a `IBoundConfiguration<T>` with some options.

Default options will be used if you just use `Configuration.For<T>()` or `Configuration.ForDynamic()`.

Custom options can be built with an `OptionsBuilder`, to base on existing `Options` call `Options.NewBuilder()` 
(ie. `Options.Default.NewBuilder()` will create an `OptionsBuilder` with default options pre-populated).  Call
`.Build()` on an `OptionsBuilder` to create an `Options` to pass to `Configuration.For(<T> | Dynamic)`.

You can configure:

 - Value separator character (typically `,`)
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
 - A custom `IDynamicTypeConverter` for determining how to convert dynamic values to concrete types
 
## Buffer Size Hints

Buffer size hints are only taken as guidance, allocations in excess of the hints may still occur.

If `WriteBufferSizeHint` is set to `0`, writes will be written directly to the provided `TextWriter` - this typically slows performance, 
but may reduce overall allocations.

If `ReadBufferSizeHint` is set to `0`, Cesil will try to use a single-page of buffer for reading.

## ITypeDescriber

The two methods on `ITypeDescriber` (`EnumerateMembersToSerialize` and `EnumerateMembersToDeserialize`) are used to discover which members
to de(serialize) on a type.  The method `GetInstanceProvider(TypeInfo)` is used to control how instances of a type are acquired during
deserialization.

The default type describer (de)serializes public properties, honors `[DataMember]`, and looks for `ShouldSerializeXXX()` and `ResetXXX()` methods.
It requires deserialized types have a parameterless constructor, which is used to create new instances.

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

Formatters must be static methods that take a type to be serialized, an `in WriteContext`, an `IBufferWriter<char>`, and return a bool.

A formatter should return false if it cannot format the given value into the given `IBufferWriter<char>`.

#### Should Serialize

Should serialize methods are optional.  If present, they can be instance methods or static methods.  They must return a boolean.

If instance methods, they must be on the type being serialized or on a type it can be assigned to.

If a should serialize method is configured, and it returns false at serialization time, the value will not be emitted.

### Deserializing

`DeserializableMember`s have a name (which is the column name), a setter or a field, a parser, whether or not the column is required, and a reset method.

#### Setters

A setter can be either an instance method or a static method.  It may not return a value.

If a setter is an instance method, it must take a single value of the type returned by the paired parser.

If a static method, it can take one or two values.  If it takes values, the first value must be of the type being deserialized (or one it
is assignable to) and the second value must be the type returned by the paired parser.

#### Parsers

Parsers must be a static method, and must have three parameters - the first being a `ReadOnlySpan<char>`, the second being an `in ReadContext`, and the third being an `out T` where
T is the type of the paired field or a value passed to the paired setter.

#### Reset

Reset methods are optional.  If present, they can be instance methods or static methods.

If an instance method, cannot take any parameters and must be on the serialized type or a type it is assignable to.

If a static method can take 0 or 1 parameters, and if it takes a parameter it must be of the type being serialized or a type it can be assigned to.

A member's reset method is called before it's setter.

# Dynamic Support

`Configuration.ForDynamic(Options)` returns a `BoundConfiguration<dynamic>` that can be used to read and write dynamic rows of data.

Cesil assumes that rows have a consistent number of cells, but otherwise imposes no constraints on the data in cells during dynamic operations.

Cells in returned rows may be accessed by index (base 0) or (if headers were present) by name.

## Casting `dynamic`

The `IDynamicTypeConverter` configured when reading is used to implement casting individual rows and cells to concrete types - `DefaultDynamicTypeConverter` is used by default and
supports casting cells to any type which has a default parser, rows to POCOs initialized with either constructors or properties, rows to `ValueTuple`s, or rows to `Tuple`s.

### `IDynamicTypeConverter`

`IDynamicTypeConverter` exposes two methods `GetCellConverter` and `GetRowConverter` for supporting the two cases for casting, either whole rows or individual cell values.

`GetCellConverter` returns a `DynamicCellConverter` which can wrap either:

 - a constructor taking a single `object` (or, equivalently, `dynamic`) parameter 
 - a method with `ReadOnlySpan<char>`, `in ReadContext`, and an `out` parameters which returns a `bool`

`GetRowConverter` returns a `DynamicRowConverter` which can wrap one of:
 
 - a constructor taking a single `object` (or, equivalently, `dynamic`) parameter 
 - a constructor taking some number of parameters, and a map of each parameter to a column index (base 0)
 - a constructor taking no parameters, a collection of setter methods on the constructed type, and a map of each setter to a column index (base 0)
   * setter methods must take a single parameter and return void, ie. behave like auto property setters
 - a method with `ReadOnlySpan<char>`, `in ReadContext`, and an `out` parameters which returns a `bool`

# Using `IBoundConfiguration<T>`
 
A `IBoundConfiguration<T>` instance expose 4 methods: `CreateReader`, `CreateAsyncReader`, `CreateWriter`, and `CreateAsyncWriter`.  These return `IReader<T>`, `IAsyncReader<T>`, `IWriter<T>` and `IAsyncWriter<T>` instances respectively.

## Disposing

The synchronous interfaces implement `IDisposable` and are meant to be used with `using` statements.

The asynchronous interfaces implement `IAsyncDisposable` and are meant to be used with the `await using` statements added in C# 8.0.

## IReader

`IReader<T>` exposes the following methods in addition to `Dispose()`:

 - `ReadAll()` - reads all rows into a new `List<T>` and returns it
 - `ReadAll(List<T>)` - reads all rows and adds them to the provided `List<T>`, which must not be `null`
 - `EnumerateAll()` - lazily reads each row as the returned enumerable is enumerated.
 - `TryRead(out T)` - reads a single row into the out parameter, returning true if a row was available and false otherwise
 - `TryReadWithReuse(ref T)` - reads a single row setting values on the given ref parameter, allocates a new `T` if needed.  Returns true if a row was available, and false otherwise.
 
## IAsyncReader
 
`IAsyncReader<T>` exposes the following methods in addition to `DisposeAsync()`:

 - `ReadAllAsync(CancellationToken = default)` - reads all rows into a new `List<T>` and returns it.
 - `ReadAllAsync(List<T>, CancellationToken = default)` - reads all rows into the given `List<T>`, which must not be `null`.
 - `EnumerateAllAsync()` - lazily reads each row as the returned async enumerable is enumerated.  Intended to be used with `await foreach` statements added in C# 8.0.
 - `TryReadAsync(CancellationToken = default)` - reads a single row, returning a `ReadResult<T>` that indicates if a row was available and, if so, the `T` read.
 - `TryReadAsync(ref T, CancellationToken = default)` - reads a single row setting values on the given ref parameter, allocating a new `T` if needed.  Returns a `ReadResult<T>` that indicates if a row was available and, if so, the `T` read.
 
All methods return `ValueTask`s, and will complete synchronously if possible - only invoking async machinery if needed to avoid blocking.

## IWriter

`IWriter<T>` exposes the following methods in addition to `Dispose()`:

 - `Write(T)` - writes a single row.
 - `WriteAll(IEnumerable<T>)` - writes all rows in the enumerable.

## IAsyncWriter

`IAsyncWriter<T>` exposes the following methods in addition to `DisposeAsync()`:

 - `WriteAsync(T, CancellationToken = default)` - writes a single row.
 - `WriteAllAsync(IEnumerable<T>, CancellationToken = default)` - writes all rows in the enumerable.
 - `WriteAllAsync(IAsyncEnumerable<T>, CancellationToken = default)` - writes all rows in the async enumerable.
 
 
All methods return `ValueTask`s, and will complete synchronously if possible - only invoking async machinery if needed to avoid blocking.

## ReadContext and WriteContext

Parser and formatter methods are called with readonly instances of `ReadContext` and `WriteContext` structs.  These structs expose the following

 - `RowNumber` - the (base 0) index of the row being read or written
 - `ColumnNumber` - the (base 0) index of the column being read or written
 - `ColumnName` - the name of the column being written, if the name is known (which is not the case if reading without headers)
 - `Context` - the object passed to a method on `IBoundConfiguration<T>` when creating a reader or writer, if any

For cases where type describers are not sufficiently flexible, the user specified context item on `ReadContext` and `WriteContext` can be used to pass state 
that is reader or writer specific to parsers and formatters.