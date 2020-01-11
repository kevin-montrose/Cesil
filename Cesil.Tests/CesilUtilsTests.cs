using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Cesil.Tests
{
    public class CesilUtilsTests
    {
        private sealed class _Enumerate
        {
            public string Foo { get; set; }
            public string Bar { get; set; }
        }

        [Fact]
        public void Enumerate()
        {
            using (var reader = new StringReader("Foo,Bar\r\nHello,World"))
            {
                var e = CesilUtils.Enumerate<_Enumerate>(reader);
                Assert.Collection(
                    e,
                    row =>
                    {
                        Assert.Equal("Hello", row.Foo);
                        Assert.Equal("World", row.Bar);
                    }
                );
            }

            // errors
            Assert.Throws<ArgumentNullException>(() => CesilUtils.Enumerate<_Enumerate>(default));
        }

        [Fact]
        public void EnumerateDynamic()
        {
            using (var reader = new StringReader("Foo,Bar\r\nHello,World"))
            {
                var e = CesilUtils.EnumerateDynamic(reader);
                var ix = 0;
                foreach(var row in e)
                {
                    switch (ix)
                    {
                        case 0:
                            Assert.Equal("Hello", (string)row.Foo);
                            Assert.Equal("World", (string)row.Bar);
                            break;
                        default: throw new Exception();
                    }

                    ix++;
                }

                Assert.Equal(1, ix);
            }

            // errors
            Assert.Throws<ArgumentNullException>(() => CesilUtils.EnumerateDynamic(default));
        }

        [Fact]
        public void EnumerateFromFile()
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, "Foo,Bar\r\nHello,World");

                var e = CesilUtils.EnumerateFromFile<_Enumerate>(tempFile);
                Assert.Collection(
                    e,
                    row =>
                    {
                        Assert.Equal("Hello", row.Foo);
                        Assert.Equal("World", row.Bar);
                    }
                );
            }
            finally
            {
                File.Delete(tempFile);
            }

            // errors
            Assert.Throws<ArgumentNullException>(() => CesilUtils.EnumerateFromFile<_Enumerate>(default));
        }

        [Fact]
        public void EnumerateDynamicFromFile()
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, "Foo,Bar\r\nHello,World");

                var e = CesilUtils.EnumerateDynamicFromFile(tempFile);
                var ix = 0;
                foreach (var row in e)
                {
                    switch (ix)
                    {
                        case 0:
                            Assert.Equal("Hello", (string)row.Foo);
                            Assert.Equal("World", (string)row.Bar);
                            break;
                        default: throw new Exception();
                    }

                    ix++;
                }

                Assert.Equal(1, ix);
            }
            finally
            {
                File.Delete(tempFile);
            }

            // errors
            Assert.Throws<ArgumentNullException>(() => CesilUtils.EnumerateDynamicFromFile(default));
        }

        [Fact]
        public async Task EnumerateAsync()
        {
            using (var reader = new StringReader("Foo,Bar\r\nHello,World"))
            {
                var rows = new List<_Enumerate>();
                await foreach (var row in CesilUtils.EnumerateAsync<_Enumerate>(reader))
                {
                    rows.Add(row);
                }

                Assert.Collection(
                    rows,
                    row =>
                    {
                        Assert.Equal("Hello", row.Foo);
                        Assert.Equal("World", row.Bar);
                    }
                );
            }

            // errors
            Assert.Throws<ArgumentNullException>(() => CesilUtils.EnumerateAsync<_Enumerate>(default));
        }

        [Fact]
        public async Task EnumerateDynamicAsync()
        {
            using (var reader = new StringReader("Foo,Bar\r\nHello,World"))
            {
                var ix = 0;
                await foreach (var row in CesilUtils.EnumerateDynamicAsync(reader))
                {
                    switch (ix)
                    {
                        case 0:
                            Assert.Equal("Hello", (string)row.Foo);
                            Assert.Equal("World", (string)row.Bar);
                            break;
                        default: throw new Exception();
                    }

                    ix++;
                }

                Assert.Equal(1, ix);
            }

            // errors
            Assert.Throws<ArgumentNullException>(() => CesilUtils.EnumerateDynamicAsync(default));
        }

        [Fact]
        public async Task EnumerateFromFileAsync()
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, "Foo,Bar\r\nHello,World");

                var rows = new List<_Enumerate>();
                await foreach (var row in CesilUtils.EnumerateFromFileAsync<_Enumerate>(tempFile))
                {
                    rows.Add(row);
                }

                Assert.Collection(
                    rows,
                    row =>
                    {
                        Assert.Equal("Hello", row.Foo);
                        Assert.Equal("World", row.Bar);
                    }
                );
            }
            finally
            {
                File.Delete(tempFile);
            }

            // errors
            Assert.Throws<ArgumentNullException>(() => CesilUtils.EnumerateFromFileAsync<_Enumerate>(default));
        }

        [Fact]
        public async Task EnumerateDynamicFromFileAsync()
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, "Foo,Bar\r\nHello,World");

                var ix = 0;
                await foreach (var row in CesilUtils.EnumerateDynamicFromFileAsync(tempFile))
                {
                    switch (ix)
                    {
                        case 0:
                            Assert.Equal("Hello", (string)row.Foo);
                            Assert.Equal("World", (string)row.Bar);
                            break;
                        default: throw new Exception();
                    }

                    ix++;
                }
                Assert.Equal(1, ix);
            }
            finally
            {
                File.Delete(tempFile);
            }

            // errors
            Assert.Throws<ArgumentNullException>(() => CesilUtils.EnumerateDynamicFromFileAsync(default));
        }

        private sealed class _Write
        {
            public string Foo { get; set; }
            public string Bar { get; set; }
        }

        [Fact]
        public void Write()
        {
            using (var writer = new StringWriter()) {

                CesilUtils.Write(new[] { new _Write { Foo = "hello", Bar = "world" } }, writer);

                var txt = writer.ToString();
                Assert.Equal("Foo,Bar\r\nhello,world", txt);
            }

            // errors
            Assert.Throws<ArgumentNullException>(() => CesilUtils.Write(new _Write[0], default));
            Assert.Throws<ArgumentNullException>(() => CesilUtils.Write<_Write>(default, TextWriter.Null));
        }

        [Fact]
        public void WriteDynamic()
        {
            using (var writer = new StringWriter())
            {

                CesilUtils.WriteDynamic(new object[] { new _Write { Foo = "hello", Bar = "world" } }, writer);

                var txt = writer.ToString();
                Assert.Equal("Foo,Bar\r\nhello,world", txt);
            }

            // errors
            Assert.Throws<ArgumentNullException>(() => CesilUtils.WriteDynamic(new object[0], default));
            Assert.Throws<ArgumentNullException>(() => CesilUtils.WriteDynamic(default, TextWriter.Null));
        }

        [Fact]
        public void WriteToFile()
        {
            var tempFile = Path.GetTempFileName();
            File.Delete(tempFile);
            try
            {
                CesilUtils.WriteToFile(new[] { new _Write { Foo = "hello", Bar = "world" } }, tempFile);

                var txt = File.ReadAllText(tempFile);
                Assert.Equal("Foo,Bar\r\nhello,world", txt);
            }
            catch
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }

            // errors
            Assert.Throws<ArgumentNullException>(() => CesilUtils.WriteToFile(new _Write[0], default));
            Assert.Throws<ArgumentNullException>(() => CesilUtils.WriteToFile<_Write>(default, tempFile));
        }

        [Fact]
        public void WriteDynamicToFile()
        {
            var tempFile = Path.GetTempFileName();
            File.Delete(tempFile);
            try
            {
                CesilUtils.WriteDynamicToFile(new object[] { new _Write { Foo = "hello", Bar = "world" } }, tempFile);

                var txt = File.ReadAllText(tempFile);
                Assert.Equal("Foo,Bar\r\nhello,world", txt);
            }
            catch
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }

            // errors
            Assert.Throws<ArgumentNullException>(() => CesilUtils.WriteDynamicToFile(new object[0], default));
            Assert.Throws<ArgumentNullException>(() => CesilUtils.WriteDynamicToFile(default, tempFile));
        }

        private static async IAsyncEnumerable<T> MakeAsync<T>(IEnumerable<T> rows)
        {
            foreach(var r in rows)
            {
                await Task.Yield();
                yield return r;
            }
        }

        [Fact]
        public async Task WriteAsync()
        {
            // IEnumerable
            using (var writer = new StringWriter())
            {
                await CesilUtils.WriteAsync(new[] { new _Write { Foo = "hello", Bar = "world" } }, writer);

                var txt = writer.ToString();
                Assert.Equal("Foo,Bar\r\nhello,world", txt);
            }

            // errors
            Assert.Throws<ArgumentNullException>(() => CesilUtils.WriteAsync(new _Write[0], default));
            Assert.Throws<ArgumentNullException>(() => CesilUtils.WriteAsync(default(IEnumerable<_Write>), TextWriter.Null));

            // IAsyncEnumerable
            using (var writer = new StringWriter())
            {
                await CesilUtils.WriteAsync(MakeAsync(new[] { new _Write { Foo = "hello", Bar = "world" } }), writer);

                var txt = writer.ToString();
                Assert.Equal("Foo,Bar\r\nhello,world", txt);
            }

            Assert.Throws<ArgumentNullException>(() => CesilUtils.WriteAsync(MakeAsync(new _Write[0]), default));
            Assert.Throws<ArgumentNullException>(() => CesilUtils.WriteAsync(default(IAsyncEnumerable<_Write>), TextWriter.Null));
        }

        [Fact]
        public async Task WriteDynamicAsync()
        {
            // IEnumerable
            using (var writer = new StringWriter())
            {
                await CesilUtils.WriteDynamicAsync(new object[] { new _Write { Foo = "hello", Bar = "world" } }, writer);

                var txt = writer.ToString();
                Assert.Equal("Foo,Bar\r\nhello,world", txt);
            }

            // errors
            Assert.Throws<ArgumentNullException>(() => CesilUtils.WriteDynamicAsync(new object[0], default));
            Assert.Throws<ArgumentNullException>(() => CesilUtils.WriteDynamicAsync(default(IEnumerable<dynamic>), TextWriter.Null));

            // IAsyncEnumerable
            using (var writer = new StringWriter())
            {
                await CesilUtils.WriteDynamicAsync(MakeAsync(new object[] { new _Write { Foo = "hello", Bar = "world" } }), writer);

                var txt = writer.ToString();
                Assert.Equal("Foo,Bar\r\nhello,world", txt);
            }

            Assert.Throws<ArgumentNullException>(() => CesilUtils.WriteDynamicAsync(MakeAsync(new object[0]), default));
            Assert.Throws<ArgumentNullException>(() => CesilUtils.WriteDynamicAsync(default(IAsyncEnumerable<object>), TextWriter.Null));
        }

        [Fact]
        public async Task WriteToFileAsync()
        {
            var tempFile = Path.GetTempFileName();
            File.Delete(tempFile);

            // IEnumerable
            try
            {
                await CesilUtils.WriteToFileAsync(new[] { new _Write { Foo = "hello", Bar = "world" } }, tempFile);

                var txt = File.ReadAllText(tempFile);
                Assert.Equal("Foo,Bar\r\nhello,world", txt);
            }
            catch
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }

            // errors
            Assert.Throws<ArgumentNullException>(() => CesilUtils.WriteToFileAsync(new _Write[0], default));
            Assert.Throws<ArgumentNullException>(() => CesilUtils.WriteToFileAsync(default(IEnumerable<_Write>), tempFile));

            // IAsyncEnumerable
            try
            {
                await CesilUtils.WriteToFileAsync(MakeAsync(new[] { new _Write { Foo = "hello", Bar = "world" } }), tempFile);

                var txt = File.ReadAllText(tempFile);
                Assert.Equal("Foo,Bar\r\nhello,world", txt);
            }
            catch
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }

            // errors
            Assert.Throws<ArgumentNullException>(() => CesilUtils.WriteToFileAsync(MakeAsync(new _Write[0]), default));
            Assert.Throws<ArgumentNullException>(() => CesilUtils.WriteToFileAsync(default(IAsyncEnumerable<_Write>), tempFile));
        }

        [Fact]
        public async Task WriteDynamicToFileAsync()
        {
            var tempFile = Path.GetTempFileName();
            File.Delete(tempFile);

            // IEnumerable
            try
            {
                await CesilUtils.WriteDynamicToFileAsync(new object[] { new _Write { Foo = "hello", Bar = "world" } }, tempFile);

                var txt = File.ReadAllText(tempFile);
                Assert.Equal("Foo,Bar\r\nhello,world", txt);
            }
            catch
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }

            // errors
            Assert.Throws<ArgumentNullException>(() => CesilUtils.WriteDynamicToFileAsync(new object[0], default));
            Assert.Throws<ArgumentNullException>(() => CesilUtils.WriteDynamicToFileAsync(default(IEnumerable<dynamic>), tempFile));

            // IAsyncEnumerable
            try
            {
                await CesilUtils.WriteDynamicToFileAsync(MakeAsync(new object[] { new _Write { Foo = "hello", Bar = "world" } }), tempFile);

                var txt = File.ReadAllText(tempFile);
                Assert.Equal("Foo,Bar\r\nhello,world", txt);
            }
            catch
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }

            // errors
            Assert.Throws<ArgumentNullException>(() => CesilUtils.WriteDynamicToFileAsync(MakeAsync(new object[0]), default));
            Assert.Throws<ArgumentNullException>(() => CesilUtils.WriteDynamicToFileAsync(default(IAsyncEnumerable<dynamic>), tempFile));
        }
    }
}
