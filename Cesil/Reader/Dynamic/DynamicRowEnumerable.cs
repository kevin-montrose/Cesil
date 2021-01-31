﻿using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace Cesil
{
    internal sealed class DynamicRowEnumerable<T> : IEnumerable<T>
    {
        private readonly uint Generation;
        private readonly DynamicRow Row;
        private readonly ITestableDisposable DependsOn;
        private readonly int? Offset;
        private readonly int? Length;

        internal DynamicRowEnumerable(object row)
        {
            if (row is DynamicRow dynRow)
            {
                Row = dynRow;
                DependsOn = dynRow;
                Offset = Length = null;
            }
            else
            {
                var dynRowRange = Utils.NonNull(row as DynamicRowRange);

                Row = dynRowRange.Parent;
                DependsOn = dynRowRange;
                Offset = dynRowRange.Offset;
                Length = dynRowRange.Length;
            }

            Generation = Row.Generation;
        }

        public IEnumerator<T> GetEnumerator()
        {
            Row.AssertGenerationMatch(Generation);

            return new DynamicRowEnumerator<T>(Row, DependsOn, Offset, Length);
        }

        [ExcludeFromCoverage("Trivial, and covered by IEnumerable<T>.GetEnumerator()")]
        IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

        public override string ToString()
        => $"{nameof(DynamicRowEnumerable<T>)} bound to {Row}";
    }
}
