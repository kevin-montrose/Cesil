# Cesil
Modern CSV (De)Serializer

[![Run Tests (Debug)](https://github.com/kevin-montrose/Cesil/workflows/Run%20Tests%20(Debug)/badge.svg)](https://github.com/kevin-montrose/Cesil/actions?query=workflow%3A%22Run+Tests+%28Debug%29%22)

# PRE-RELEASE

This code isn't well tested yet, **YOU PROBABLY DON'T WANT TO USE IT!!!**

# Documentation

Consult [The Wiki™](https://github.com/kevin-montrose/Cesil/wiki) for documentation, and [Cesil's Github Pages for references](https://kevin-montrose.github.io/Cesil/api/Cesil.html).

You may be interested in:

 - [Configurations](https://github.com/kevin-montrose/Cesil/wiki/Configurations)
 - [Reading](https://github.com/kevin-montrose/Cesil/wiki/Reading)
 - [Writing](https://github.com/kevin-montrose/Cesil/wiki/Writing)
 - [Convenience Utilities](https://github.com/kevin-montrose/Cesil/wiki/Convenience-Utilities)
 - [Default Type Describer](https://github.com/kevin-montrose/Cesil/wiki/Default-Type-Describer)

# Quick Start

 1. Install the latest Cesil off of [Nuget](https://www.nuget.org/packages/Cesil/).
 2. Add `using Cesil;` to your C# file
 3. Create a configuration (with default Options) with either `Configuration.For<TYourType>()` or `Configuration.ForDynamic()`, as appropriate for your use case
 4. Create a reader or writer using one of the `CreateReader`, `CreateAsyncReader`, `CreateWriter`, or `CreateAsyncWriter` methods on the configuration.
    * Each of these methods has a number of overloads, supporting using `TextReaders`, `Pipes`, and so on.
 5. Use the methods on the `IReader<TRow>`, `IAsyncReader<TRow>`, `IWriter<TRow>`, or `IAsyncWriter<TRow>` interface to read or write your data.

## Example: Reading Synchronously

Using a [convient method](https://github.com/kevin-montrose/Cesil/wiki/Convenience-Utilities#reading):

```csharp
using Cesil;

// ...

using(TextReader reader = /* some TextReader */)
{
	IEnumerable<MyType> rows = CesilUtils.Enumerate<MyType>(reader);
}
```

In a more explicit, and configurable, way using [explicit configuration](https://github.com/kevin-montrose/Cesil/wiki/Configurations) and [options](https://github.com/kevin-montrose/Cesil/wiki/Options).

```csharp
using Cesil;

// ...

Options myOptions = /* ... */
IBoundConfiguration<MyType> myConfig = Configuration.For<MyType>(myOptions);

using(TextReader reader = /* ... */)
using(IReader<MyType> csv = myConfig.CreateReader(reader))
{
	IEnumerable<MyType> rows = csv.EnumerateAll();
}
```

For more detail, see [Reading](https://github.com/kevin-montrose/Cesil/wiki/Reading).

## Example: Reading Asynchronously

Using a [convient method](https://github.com/kevin-montrose/Cesil/wiki/Convenience-Utilities#reading):

```csharp
using Cesil;

// ...

using(TextReader reader = /* some TextReader */)
{
	IAsyncEnumerable<MyType> rows = CesilUtils.EnumerateAsync<MyType>(reader);
}
```

In a more explicit, and configurable, way using [explicit configuration](https://github.com/kevin-montrose/Cesil/wiki/Configurations) and [options](https://github.com/kevin-montrose/Cesil/wiki/Options).

```csharp
using Cesil;

// ...

Options myOptions = /* ... */
IBoundConfiguration<MyType> myConfig = Configuration.For<MyType>(myOptions);

using(TextReader reader = /* ... */)
await using(IAsyncReader<MyType> csv = myConfig.CreateAsyncReader(reader))
{
	IAsyncReader<MyType> rows = csv.EnumerateAllAsync();
}
```

For more detail, see [Reading](https://github.com/kevin-montrose/Cesil/wiki/Reading).

## Example: Writing Synchronously

Using a [convient method](https://github.com/kevin-montrose/Cesil/wiki/Convenience-Utilities#writing):

```csharp
using Cesil;

// ...

IEnumerable<MyType> myRows = /* ... */

using(TextWriter writer = /* .. */)
{
	CesilUtilities.Write(myRows, writer);
}
```

In a more explicit, and configurable, way using [explicit configuration](https://github.com/kevin-montrose/Cesil/wiki/Configurations) and [options](https://github.com/kevin-montrose/Cesil/wiki/Options).

```csharp
using Cesil;

// ...

IEnumerable<MyType> myRows = /* ... */

Options myOptions = /* ... */
IBoundConfiguration<MyType> myConfig = Configuration.For<MyType>(myOptions);

using(TextWriter writer = /* ... */)
using(IWriter<MyType> csv = myConfig.CreateWriter(writer))
{
	csv.WriteAll(myRows);
}
```

For more detail, see [Writing](https://github.com/kevin-montrose/Cesil/wiki/Writing).

## Example: Writing Asynchronously

Using a [convient method](https://github.com/kevin-montrose/Cesil/wiki/Convenience-Utilities#writing):

```csharp
using Cesil;

// ...

// IAsyncEnumerable<MyType> will also work
IEnumerable<MyType> myRows = /* ... */

using(TextWriter writer = /* .. */)
{
	await CesilUtilities.WriteAsync(myRows, writer);
}
```

In a more explicit, and configurable, way using [explicit configuration](https://github.com/kevin-montrose/Cesil/wiki/Configurations) and [options](https://github.com/kevin-montrose/Cesil/wiki/Options).

```csharp
using Cesil;

// ...

// IAsyncEnumerable<MyType> will also work
IEnumerable<MyType> myRows = /* ... */

Options myOptions = /* ... */
IBoundConfiguration<MyType> myConfig = Configuration.For<MyType>(myOptions);

using(TextWriter writer = /* ... */)
await using(IWriter<MyType> csv = myConfig.CreateAsyncWriter(writer))
{
	await csv.WriteAllAsync(myRows);
}
```

For more detail, see [Writing](https://github.com/kevin-montrose/Cesil/wiki/Writing).
