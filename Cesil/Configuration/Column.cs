using System;
using System.Buffers;

namespace Cesil
{
    internal sealed class Column
    {
        internal delegate bool SetterDelegate(ReadOnlySpan<char> text, in ReadContext context, object row);
        internal delegate bool WriterDelegate(object row, in WriteContext context, IBufferWriter<char> writeTo);

        internal static readonly Column Ignored = new Column("", delegate { return true; }, delegate { return true; }, false);

        internal string Name { get; }
        internal SetterDelegate Set { get; }
        internal WriterDelegate Write { get; }
        internal bool IsRequired { get; }

        internal Column(
            string name, 
            SetterDelegate set,
            WriterDelegate write,
            bool isRequired
        )
        {
            Name = name;
            Set = set;
            Write = write;
            IsRequired = isRequired;
        }

        internal static SetterDelegate MakeDynamicSetter(string name, int ix)
        {
            return
                (ReadOnlySpan<char> text, in ReadContext _, object row) =>
                {
                    ((DynamicRow)row).SetValue(ix, text);
                    return true;
                };
        }
    }
}
