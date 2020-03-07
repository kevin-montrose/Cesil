namespace Cesil
{
    internal static class DynamicRowTrackingHelper
    {
        public static void TryAllocateAndTrack<T>(T self, NonNull<string[]> columnNames, ref DynamicRow? head, ref dynamic row)
            where T: ReaderBase<object>, IDynamicRowOwner
        {
            // after this call row _WILL_ be a disposed non-null DynamicRow
            self.TryPreAllocateRow(ref row);

            var dynRow = Utils.NonNull(row as DynamicRow);

            var options = self.Configuration.Options;
            var needsTracking = options.DynamicRowDisposal == DynamicRowDisposal.OnReaderDispose;
            var isAttached = dynRow.Owner.HasValue;
            var isAttachedToSelf = isAttached && dynRow.Owner.Value == self;

            // possible states
            // ---------------
            // !needsTracking, !isAttached => do nothing
            // !needsTracking, isAttached => detach
            // needsTracking, !isAttached => attach
            // needsTracking, isAttached, !isAttachedToSelf => detach, attach
            // needsTracking, isAttached, isAttachedToSelf => do nothing

            var doDetach = (!needsTracking && isAttached) || (needsTracking && isAttached && !isAttachedToSelf);
            var doAttach = (needsTracking && !isAttached) || (needsTracking && isAttached && !isAttachedToSelf);

            if (doDetach)
            {
                dynRow.Owner.Value.Remove(dynRow);
            }

            if (doAttach)
            {
                head.AddHead(ref head, dynRow);
            }

            dynRow.Init(self, self.RowNumber, self.Context, options.TypeDescriber, columnNames, options.MemoryPool);
        }
    }
}
