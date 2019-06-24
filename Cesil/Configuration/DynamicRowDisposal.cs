namespace Cesil
{
    /// <summary>
    /// When a dynamic row returned by a IReader or IAsyncReader 
    ///    will be disposed.
    /// </summary>
    public enum DynamicRowDisposal : byte
    {
        /// <summary>
        /// Dynamic rows will be automatically disposed when the
        ///   reader that returned them is disposed.
        /// </summary>
        OnReaderDispose = 1,

        /// <summary>
        /// Dynamic rows will only be disposed when Dispose() is
        ///   explicitly called on the row.
        /// </summary>
        OnExplicitDispose = 2
    }
}
