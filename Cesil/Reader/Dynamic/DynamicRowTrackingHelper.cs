namespace Cesil
{
    internal static class DynamicRowTrackingHelper
    {
        internal static void TryAllocateAndTrack<T>(T self, NonNull<string[]> columnNames, ref DynamicRow? head, bool checkRow, ref dynamic row)
            where T : ReaderBase<object>, IDynamicRowOwner
        {
            // after this call row _WILL_ be a disposed non-null DynamicRow
            self.TryPreAllocateRow(checkRow, ref row);

            var dynRow = Utils.NonNull(row as DynamicRow);

            var options = self.Configuration.Options;
            var needsTracking = options.DynamicRowDisposal == DynamicRowDisposal.OnReaderDispose;
            var isAttached = dynRow.HasOwner;
            var isAttachedToSelf = isAttached && dynRow.Owner == self;

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
                dynRow.Owner.Remove(dynRow);
                dynRow.Owner = EmptyDynamicRowOwner.Singleton;
                dynRow.HasOwner = false;
            }

            if (doAttach)
            {
                head.AddHead(ref head, dynRow);
            }

            var hasNames = columnNames.HasValue;
            var names = hasNames ? columnNames.Value : null;

            dynRow.Init(self, self.RowNumber, self.Context, options.TypeDescriber, hasNames, names, 0, self.Configuration.MemoryPool);
        }

        internal static void FreePreAllocatedOnEnd(IRowConstructor<object> builder)
        {
            var dyn = (DynamicRowConstructor)builder;

            if (dyn.PreAlloced != null)
            {
                dyn.PreAlloced.Dispose();
            }
            else if (dyn.CurrentRow != null)
            {
                dyn.CurrentRow.Dispose();
            }
        }
    }
}
