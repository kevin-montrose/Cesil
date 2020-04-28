using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Cesil
{
    /// <summary>
    /// A collection of convenience methods for reading and writing using streams or files.
    /// 
    /// These methods are less flexible than directly using Configuration and are not as efficient
    /// as using IReader(T), IAsyncReader(T), IWriter(T) and IAsyncWriter(T), but they involve much
    /// less code.
    /// 
    /// Prefer them when the trade off for simplicity at the expense speed and flexibility makes sense.
    /// </summary>
    public static class CesilUtils
    {
        // todo: write to string methods

        // sync read methods

        /// <summary>
        /// Lazily enumerate rows of type TRow from the given TextReader.
        /// 
        /// An optional Options object may be used, if not provided Options.Default
        ///   will be used.
        ///   
        /// Takes an optional context object which is made available
        ///   during certain operations as a member on ReadContext.
        /// </summary>
        public static IEnumerable<TRow> Enumerate<TRow>(
            TextReader reader,
            [NullableExposed("options will default to Options.Default")]
            Options? options = null,
            [NullableExposed("context is truly optional")]
            object? context = null
        )
        {
            Utils.CheckArgumentNull(reader, nameof(reader));

            var c = Configuration.For<TRow>(options ?? Options.Default);

            return EnumerateFromStreamImpl(c, reader, context);
        }

        /// <summary>
        /// Lazily enumerate dynamic rows from the given TextReader.
        /// 
        /// An optional Options object may be used, if not provided Options.Default
        ///   will be used.
        ///   
        /// Takes an optional context object which is made available
        ///   during certain operations as a member on ReadContext.
        /// </summary>
        public static IEnumerable<dynamic> EnumerateDynamic(
            TextReader reader,
            [NullableExposed("options will default to Options.Default")]
            Options? options = null,
            [NullableExposed("context is truly optional")]
            object? context = null
        )
        {
            Utils.CheckArgumentNull(reader, nameof(reader));

            var c = Configuration.ForDynamic(options ?? Options.DynamicDefault);

            return EnumerateFromStreamImpl(c, reader, context);
        }

        private static IEnumerable<T> EnumerateFromStreamImpl<T>(IBoundConfiguration<T> c, TextReader reader, object? context)
        {
            using (var csv = c.CreateReader(reader, context))
            {
                foreach (var row in csv.EnumerateAll())
                {
                    yield return row;
                }
            }
        }

        /// <summary>
        /// Lazily enumerate rows of type TRow from the given file.  If the file as a byte-order-marker (BOM) the indicated encoding will be used,
        ///   otherwise utf-8 will be assumed.
        /// 
        /// An optional Options object may be used, if not provided Options.Default
        ///   will be used.
        ///   
        /// Takes an optional context object which is made available
        ///   during certain operations as a member on ReadContext.
        /// </summary>
        public static IEnumerable<TRow> EnumerateFromFile<TRow>(
            string path,
            [NullableExposed("options will default to Options.Default")]
            Options? options = null,
            [NullableExposed("context is truly optional")]
            object? context = null
        )
        {
            Utils.CheckArgumentNull(path, nameof(path));

            // reader will be disposed in the Enumerate call
            var reader = new StreamReader(path, true);
            return Enumerate<TRow>(reader, options, context);
        }

        /// <summary>
        /// Lazily enumerate dynamic rows from the given file.  If the file as a byte-order-marker (BOM) the indicated encoding will be used,
        ///   otherwise utf-8 will be assumed.
        /// 
        /// An optional Options object may be used, if not provided Options.Default
        ///   will be used.
        ///   
        /// Takes an optional context object which is made available
        ///   during certain operations as a member on ReadContext.
        /// </summary>
        public static IEnumerable<dynamic> EnumerateDynamicFromFile(
            string path,
            [NullableExposed("options will default to Options.Default")]
            Options? options = null,
            [NullableExposed("context is truly optional")]
            object? context = null
        )
        {
            Utils.CheckArgumentNull(path, nameof(path));

            // reader will be disposed in the Enumerate call
            var reader = new StreamReader(path, true);
            return EnumerateDynamic(reader, options, context);
        }

        /// <summary>
        /// Lazily enumerate rows of type TRow from the given string.
        /// 
        /// An optional Options object may be used, if not provided Options.Default
        ///   will be used.
        ///   
        /// Takes an optional context object which is made available
        ///   during certain operations as a member on ReadContext.
        /// </summary>
        public static IEnumerable<T> EnumerateFromString<T>(
            string data,
            [NullableExposed("options will default to Options.Default")]
            Options? options = null,
            [NullableExposed("context is truly optional")]
            object? context = null
        )
        {
            Utils.CheckArgumentNull(data, nameof(data));

            var reader = new StringReader(data);
            // reader will be disposed in the Enumerate call
            return Enumerate<T>(reader, options, context);
        }

        /// <summary>
        /// Lazily enumerate dynamic rows from the given string.
        /// 
        /// An optional Options object may be used, if not provided Options.Default
        ///   will be used.
        ///   
        /// Takes an optional context object which is made available
        ///   during certain operations as a member on ReadContext.
        /// </summary>
        public static IEnumerable<dynamic> EnumerateDynamicFromString(
            string data,
            [NullableExposed("options will default to Options.Default")]
            Options? options = null,
            [NullableExposed("context is truly optional")]
            object? context = null
        )
        {
            Utils.CheckArgumentNull(data, nameof(data));

            var reader = new StringReader(data);
            // reader will be disposed in the Enumerate call
            return EnumerateDynamic(reader, options, context);
        }

        // async read methods

        /// <summary>
        /// Lazily and asynchronously enumerate rows of type TRow from the given TextReader.
        /// 
        /// An optional Options object may be used, if not provided Options.Default
        ///   will be used.
        ///   
        /// Takes an optional context object which is made available
        ///   during certain operations as a member on ReadContext.
        ///   
        /// A CancellationToken may also be provided, CancellationToken.None will be used otherwise.
        /// </summary>
        public static IAsyncEnumerable<TRow> EnumerateAsync<TRow>(
            TextReader reader,
            [NullableExposed("options will default to Options.Default")]
            Options? options = null,
            [NullableExposed("context is truly optional")]
            object? context = null,
            CancellationToken cancel = default
        )
        {
            Utils.CheckArgumentNull(reader, nameof(reader));

            var c = Configuration.For<TRow>(options ?? Options.Default);

            return EnumerateFromStreamImplAsync(c, reader, context, cancel);
        }

        /// <summary>
        /// Lazily and asynchronously enumerate dynamic rows from the given TextReader.
        /// 
        /// An optional Options object may be used, if not provided Options.Default
        ///   will be used.
        ///   
        /// Takes an optional context object which is made available
        ///   during certain operations as a member on ReadContext.
        ///   
        /// A CancellationToken may also be provided, CancellationToken.None will be used otherwise.
        /// </summary>
        public static IAsyncEnumerable<dynamic> EnumerateDynamicAsync(
            TextReader reader,
            [NullableExposed("options will default to Options.Default")]
            Options? options = null,
            [NullableExposed("context is truly optional")]
            object? context = null,
            CancellationToken cancel = default
        )
        {
            Utils.CheckArgumentNull(reader, nameof(reader));

            var c = Configuration.ForDynamic(options ?? Options.DynamicDefault);

            return EnumerateFromStreamImplAsync(c, reader, context, cancel);
        }

        private static async IAsyncEnumerable<T> EnumerateFromStreamImplAsync<T>(IBoundConfiguration<T> c, TextReader reader, object? context, [EnumeratorCancellation]CancellationToken cancel)
        {
            await using (var csv = c.CreateAsyncReader(reader, context))
            {
                await using (var e = csv.EnumerateAllAsync().GetAsyncEnumerator(cancel))
                {
                    while (await e.MoveNextAsync())
                    {
                        yield return e.Current;
                    }
                }
            }
        }

        /// <summary>
        /// Lazily and asynchronously enumerate rows of type TRow from the given file.  If the file as a byte-order-marker (BOM) the indicated encoding will be used,
        ///   otherwise utf-8 will be assumed.
        /// 
        /// An optional Options object may be used, if not provided Options.Default
        ///   will be used.
        ///   
        /// Takes an optional context object which is made available
        ///   during certain operations as a member on ReadContext.
        ///   
        /// A CancellationToken may also be provided, CancellationToken.None will be used otherwise.
        /// </summary>
        public static IAsyncEnumerable<TRow> EnumerateFromFileAsync<TRow>(
            string path,
            [NullableExposed("options will default to Options.Default")]
            Options? options = null,
            [NullableExposed("context is truly optional")]
            object? context = null,
            CancellationToken cancel = default
        )
        {
            Utils.CheckArgumentNull(path, nameof(path));

            // reader will be disposed in the EnumerateAsync call
            var reader = new StreamReader(path, true);
            return EnumerateAsync<TRow>(reader, options, context, cancel);
        }

        /// <summary>
        /// Lazily and asynchronously enumerate dynamic rows from the given file.  If the file as a byte-order-marker (BOM) the indicated encoding will be used,
        ///   otherwise utf-8 will be assumed.
        /// 
        /// An optional Options object may be used, if not provided Options.Default
        ///   will be used.
        ///   
        /// Takes an optional context object which is made available
        ///   during certain operations as a member on ReadContext.
        ///   
        /// A CancellationToken may also be provided, CancellationToken.None will be used otherwise.
        /// </summary>
        public static IAsyncEnumerable<dynamic> EnumerateDynamicFromFileAsync(
            string path,
            [NullableExposed("options will default to Options.Default")]
            Options? options = null,
            [NullableExposed("context is truly optional")]
            object? context = null,
            CancellationToken cancel = default
        )
        {
            Utils.CheckArgumentNull(path, nameof(path));

            // reader will be disposed in the EnumerateAsync call
            var reader = new StreamReader(path, true);
            return EnumerateDynamicAsync(reader, options, context, cancel);
        }

        /// <summary>
        /// Lazily and asynchronously enumerate rows of type TRow from the given string.
        /// 
        /// An optional Options object may be used, if not provided Options.Default
        ///   will be used.
        ///   
        /// Takes an optional context object which is made available
        ///   during certain operations as a member on ReadContext.
        ///   
        /// A CancellationToken may also be provided, CancellationToken.None will be used otherwise.
        /// </summary>
        public static IAsyncEnumerable<TRow> EnumerateFromStringAsync<TRow>(
            string data,
            [NullableExposed("options will default to Options.Default")]
            Options? options = null,
            [NullableExposed("context is truly optional")]
            object? context = null,
            CancellationToken cancel = default
        )
        {
            Utils.CheckArgumentNull(data, nameof(data));

            // reader will be disposed in the EnumerateAsync call
            var reader = new StringReader(data);
            return EnumerateAsync<TRow>(reader, options, context, cancel);
        }

        /// <summary>
        /// Lazily and asynchronously enumerate dynamic rows from the given string.
        /// 
        /// An optional Options object may be used, if not provided Options.Default
        ///   will be used.
        ///   
        /// Takes an optional context object which is made available
        ///   during certain operations as a member on ReadContext.
        ///   
        /// A CancellationToken may also be provided, CancellationToken.None will be used otherwise.
        /// </summary>
        public static IAsyncEnumerable<dynamic> EnumerateDynamicFromStringAsync(
            string data,
            [NullableExposed("options will default to Options.Default")]
            Options? options = null,
            [NullableExposed("context is truly optional")]
            object? context = null,
            CancellationToken cancel = default
        )
        {
            Utils.CheckArgumentNull(data, nameof(data));

            // reader will be disposed in the EnumerateDynamicAsync call
            var reader = new StringReader(data);
            return EnumerateDynamicAsync(reader, options, context, cancel);
        }

        // sync write methods

        /// <summary>
        /// Write a collection of rows of TRow to the given TextWriter.
        /// 
        /// An optional Options object may be used, if not provided Options.Default
        ///   will be used.
        ///   
        /// Takes an optional context object which is made available
        ///   during certain operations as a member on WriteContext.
        /// </summary>
        public static void Write<TRow>(
            IEnumerable<TRow> rows,
            TextWriter writer,
            [NullableExposed("options will default to Options.Default")]
            Options? options = null,
            [NullableExposed("context is truly optional")]
            object? context = null
        )
        {
            Utils.CheckArgumentNull(rows, nameof(rows));
            Utils.CheckArgumentNull(writer, nameof(writer));

            var c = Configuration.For<TRow>(options ?? Options.Default);
            WriteImpl(c, rows, writer, context);
        }

        /// <summary>
        /// Write a collection of dynamic rows to the given TextWriter.
        /// 
        /// An optional Options object may be used, if not provided Options.Default
        ///   will be used.
        ///   
        /// Takes an optional context object which is made available
        ///   during certain operations as a member on WriteContext.
        /// </summary>
        public static void WriteDynamic(
            IEnumerable<dynamic> rows,
            TextWriter writer,
            [NullableExposed("options will default to Options.Default")]
            Options? options = null,
            [NullableExposed("context is truly optional")]
            object? context = null
        )
        {
            Utils.CheckArgumentNull(rows, nameof(rows));
            Utils.CheckArgumentNull(writer, nameof(writer));

            var c = Configuration.ForDynamic(options ?? Options.DynamicDefault);
            WriteImpl(c, rows, writer, context);
        }

        private static void WriteImpl<T>(IBoundConfiguration<T> c, IEnumerable<T> rows, TextWriter writer, object? context)
        {
            using (var csv = c.CreateWriter(writer, context))
            {
                csv.WriteAll(rows);
            }
        }

        /// <summary>
        /// Write a collection of rows of TRow to the given path.
        /// 
        /// The file be created if it does not existing, overwritten if it does, and the encoding used will be utf-8.
        /// 
        /// An optional Options object may be used, if not provided Options.Default
        ///   will be used.
        ///   
        /// Takes an optional context object which is made available
        ///   during certain operations as a member on WriteContext.
        /// </summary>
        public static void WriteToFile<TRow>(
            IEnumerable<TRow> rows,
            string path,
            [NullableExposed("options will default to Options.Default")]
            Options? options = null,
            [NullableExposed("context is truly optional")]
            object? context = null
        )
        {
            Utils.CheckArgumentNull(rows, nameof(rows));
            Utils.CheckArgumentNull(path, nameof(path));

            using (var stream = File.Create(path))
            using (var writer = new StreamWriter(stream, Encoding.UTF8))
            {
                Write(rows, writer, options, context);
            }
        }

        /// <summary>
        /// Write a collection of dynamic rows to the given path.
        /// 
        /// The file be created if it does not existing, overwritten if it does, and the encoding used will be utf-8.
        /// 
        /// An optional Options object may be used, if not provided Options.Default
        ///   will be used.
        ///   
        /// Takes an optional context object which is made available
        ///   during certain operations as a member on WriteContext.
        /// </summary>
        public static void WriteDynamicToFile(
            IEnumerable<dynamic> rows,
            string path,
            [NullableExposed("options will default to Options.Default")]
            Options? options = null,
            [NullableExposed("context is truly optional")]
            object? context = null
        )
        {
            Utils.CheckArgumentNull(rows, nameof(rows));
            Utils.CheckArgumentNull(path, nameof(path));

            using (var stream = File.Create(path))
            using (var writer = new StreamWriter(stream, Encoding.UTF8))
            {
                WriteDynamic(rows, writer, options, context);
            }
        }

        // async write methods

        /// <summary>
        /// Write a collection of rows of TRow to the given TextWriter asynchronously.
        /// 
        /// An optional Options object may be used, if not provided Options.Default
        ///   will be used.
        ///   
        /// Takes an optional context object which is made available
        ///   during certain operations as a member on WriteContext.
        ///   
        /// A CancellationToken may also be provided, CancellationToken.None will be used otherwise.
        /// </summary>
        public static ValueTask WriteAsync<TRow>(
            IEnumerable<TRow> rows,
            TextWriter writer,
            [NullableExposed("options will default to Options.Default")]
            Options? options = null,
            [NullableExposed("context is truly optional")]
            object? context = null,
            CancellationToken cancel = default
        )
        {
            Utils.CheckArgumentNull(rows, nameof(rows));
            Utils.CheckArgumentNull(writer, nameof(writer));

            return WriteAsync(new AsyncEnumerableAdapter<TRow>(rows), writer, options, context, cancel);
        }

        /// <summary>
        /// Write a collection of dynamic rows to the given TextWriter asynchronously.
        /// 
        /// An optional Options object may be used, if not provided Options.Default
        ///   will be used.
        ///   
        /// Takes an optional context object which is made available
        ///   during certain operations as a member on WriteContext.
        ///   
        /// A CancellationToken may also be provided, CancellationToken.None will be used otherwise.
        /// </summary>
        public static ValueTask WriteDynamicAsync(
            IEnumerable<dynamic> rows,
            TextWriter writer,
            [NullableExposed("options will default to Options.Default")]
            Options? options = null,
            [NullableExposed("context is truly optional")]
            object? context = null,
            CancellationToken cancel = default
        )
        {
            Utils.CheckArgumentNull(rows, nameof(rows));
            Utils.CheckArgumentNull(writer, nameof(writer));

            return WriteDynamicAsync(new AsyncEnumerableAdapter<dynamic>(rows), writer, options, context, cancel);
        }

        /// <summary>
        /// Write a collection of rows of TRow to the given TextWriter asynchronously.
        /// 
        /// An optional Options object may be used, if not provided Options.Default
        ///   will be used.
        ///   
        /// Takes an optional context object which is made available
        ///   during certain operations as a member on WriteContext.
        ///   
        /// A CancellationToken may also be provided, CancellationToken.None will be used otherwise.
        /// </summary>
        public static ValueTask WriteAsync<TRow>(
            IAsyncEnumerable<TRow> rows,
            TextWriter writer,
            [NullableExposed("options will default to Options.Default")]
            Options? options = null,
            [NullableExposed("context is truly optional")]
            object? context = null,
            CancellationToken cancel = default
        )
        {
            Utils.CheckArgumentNull(rows, nameof(rows));
            Utils.CheckArgumentNull(writer, nameof(writer));

            var c = Configuration.For<TRow>(options ?? Options.Default);

            return WriteImplAsync(c, rows, writer, context, cancel);
        }

        /// <summary>
        /// Write a collection of dynamics rows to the given TextWriter asynchronously.
        /// 
        /// An optional Options object may be used, if not provided Options.Default
        ///   will be used.
        ///   
        /// Takes an optional context object which is made available
        ///   during certain operations as a member on WriteContext.
        ///   
        /// A CancellationToken may also be provided, CancellationToken.None will be used otherwise.
        /// </summary>
        public static ValueTask WriteDynamicAsync(
            IAsyncEnumerable<dynamic> rows,
            TextWriter writer,
            [NullableExposed("options will default to Options.Default")]
            Options? options = null,
            [NullableExposed("context is truly optional")]
            object? context = null,
            CancellationToken cancel = default
        )
        {
            Utils.CheckArgumentNull(rows, nameof(rows));
            Utils.CheckArgumentNull(writer, nameof(writer));

            var c = Configuration.ForDynamic(options ?? Options.DynamicDefault);

            return WriteImplAsync(c, rows, writer, context, cancel);
        }

        private static async ValueTask WriteImplAsync<T>(IBoundConfiguration<T> c, IAsyncEnumerable<T> rows, TextWriter writer, object? context, CancellationToken cancel)
        {
            await using (var csv = c.CreateAsyncWriter(writer, context))
            {
                await csv.WriteAllAsync(rows, cancel);
            }
        }

        /// <summary>
        /// Write a collection of rows of TRow to the given path asynchronously.
        /// 
        /// The file be created if it does not existing, overwritten if it does, and the encoding used will be utf-8.
        /// 
        /// An optional Options object may be used, if not provided Options.Default
        ///   will be used.
        ///   
        /// Takes an optional context object which is made available
        ///   during certain operations as a member on WriteContext.
        ///   
        /// A CancellationToken may also be provided, CancellationToken.None will be used otherwise.
        /// </summary>
        public static ValueTask WriteToFileAsync<TRow>(
            IEnumerable<TRow> rows,
            string path,
            [NullableExposed("options will default to Options.Default")]
            Options? options = null,
            [NullableExposed("context is truly optional")]
            object? context = null,
            CancellationToken cancel = default
        )
        {
            Utils.CheckArgumentNull(rows, nameof(rows));
            Utils.CheckArgumentNull(path, nameof(path));

            return WriteToFileAsync(new AsyncEnumerableAdapter<TRow>(rows), path, options, context, cancel);
        }

        /// <summary>
        /// Write a collection of dynamic rows to the given path asynchronously.
        /// 
        /// The file be created if it does not existing, overwritten if it does, and the encoding used will be utf-8.
        /// 
        /// An optional Options object may be used, if not provided Options.Default
        ///   will be used.
        ///   
        /// Takes an optional context object which is made available
        ///   during certain operations as a member on WriteContext.
        ///   
        /// A CancellationToken may also be provided, CancellationToken.None will be used otherwise.
        /// </summary>
        public static ValueTask WriteDynamicToFileAsync(
            IEnumerable<dynamic> rows,
            string path,
            [NullableExposed("options will default to Options.Default")]
            Options? options = null,
            [NullableExposed("context is truly optional")]
            object? context = null,
            CancellationToken cancel = default
        )
        {
            Utils.CheckArgumentNull(rows, nameof(rows));
            Utils.CheckArgumentNull(path, nameof(path));

            return WriteDynamicToFileAsync(new AsyncEnumerableAdapter<dynamic>(rows), path, options, context, cancel);
        }

        /// <summary>
        /// Write a collection of rows of TRow to the given path asynchronously.
        /// 
        /// The file be created if it does not existing, overwritten if it does, and the encoding used will be utf-8.
        /// 
        /// An optional Options object may be used, if not provided Options.Default
        ///   will be used.
        ///   
        /// Takes an optional context object which is made available
        ///   during certain operations as a member on WriteContext.
        ///   
        /// A CancellationToken may also be provided, CancellationToken.None will be used otherwise.
        /// </summary>
        public static ValueTask WriteToFileAsync<TRow>(
            IAsyncEnumerable<TRow> rows,
            string path,
            [NullableExposed("options will default to Options.Default")]
            Options? options = null,
            [NullableExposed("context is truly optional")]
            object? context = null,
            CancellationToken cancel = default
        )
        {
            Utils.CheckArgumentNull(rows, nameof(rows));
            Utils.CheckArgumentNull(path, nameof(path));

            return WriteToFileImplAsync(rows, path, options, context, cancel);

            static async ValueTask WriteToFileImplAsync(IAsyncEnumerable<TRow> rows, string path, Options? options, object? context, CancellationToken cancel)
            {
                using (var stream = File.Create(path))
                using (var writer = new StreamWriter(stream, Encoding.UTF8))
                {
                    await WriteAsync(rows, writer, options, context, cancel);
                }
            }
        }

        /// <summary>
        /// Write a collection of dynamic rows to the given path asynchronously.
        /// 
        /// The file be created if it does not existing, overwritten if it does, and the encoding used will be utf-8.
        /// 
        /// An optional Options object may be used, if not provided Options.Default
        ///   will be used.
        ///   
        /// Takes an optional context object which is made available
        ///   during certain operations as a member on WriteContext.
        ///   
        /// A CancellationToken may also be provided, CancellationToken.None will be used otherwise.
        /// </summary>
        public static ValueTask WriteDynamicToFileAsync(
            IAsyncEnumerable<dynamic> rows,
            string path,
            [NullableExposed("options will default to Options.Default")]
            Options? options = null,
            [NullableExposed("context is truly optional")]
            object? context = null,
            CancellationToken cancel = default
        )
        {
            Utils.CheckArgumentNull(rows, nameof(rows));
            Utils.CheckArgumentNull(path, nameof(path));

            return WriteToFileImplAsync(rows, path, options, context, cancel);

            static async ValueTask WriteToFileImplAsync(IAsyncEnumerable<dynamic> rows, string path, Options? options, object? context, CancellationToken cancel)
            {
                using (var stream = File.Create(path))
                using (var writer = new StreamWriter(stream, Encoding.UTF8))
                {
                    await WriteDynamicAsync(rows, writer, options, context, cancel);
                }
            }
        }
    }
}
