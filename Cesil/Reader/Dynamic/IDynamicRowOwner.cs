namespace Cesil
{
    internal interface IDynamicRowOwner
    {
        object Context { get; }

        IIntrusiveLinkedList<DynamicRow> NotifyOnDispose { get; }
        void Remove(DynamicRow row);
    }
}
