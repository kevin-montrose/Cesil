﻿namespace Cesil
{
    internal interface IDynamicRowOwner
    {
        Options Options { get; }

        object? Context { get; }

        int MinimumExpectedColumns { get; }

        void Remove(DynamicRow row);

        NameLookup AcquireNameLookup();
        void ReleaseNameLookup();
    }
}
