using System;
using System.Buffers;

namespace Cesil
{
    internal sealed class Column
    {
        internal delegate bool SetterDelegate(ReadOnlySpan<char> text, object row);

        internal static readonly Column Ignored = new Column("", delegate { return true; }, delegate { return true; }, false);

        internal string Name { get; }
        internal SetterDelegate Set { get; }
        internal Func<object, IBufferWriter<char>, bool> Write { get; }
        internal bool IsRequired { get; }

        internal Column(
            string name, 
            SetterDelegate set,
            Func<object, IBufferWriter<char>, bool> write,
            bool isRequired
        )
        {
            Name = name;
            Set = set;
            Write = write;
            IsRequired = isRequired;
        }
    }
}
