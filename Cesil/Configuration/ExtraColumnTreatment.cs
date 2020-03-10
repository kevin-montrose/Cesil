namespace Cesil
{
    /// <summary>
    /// How to handle extra columns when reading a row.
    /// 
    /// A column is "extra" is if it's index is beyond 
    /// those found in the header (or first row, if no header is
    /// present) of the CSV being read.
    /// </summary>
    public enum ExtraColumnTreatment : byte
    {
        /// <summary>
        /// Ignore any columns beyond those present in a
        /// header (or first row, if no header is present).
        /// </summary>
        Ignore = 1,
        /// <summary>
        /// Include columns beyond those in the header (or first row,
        /// if no header is present) when reading dynamic rows.  Extra
        /// columns will be accessible via index, as no column name will
        /// be available.
        /// 
        /// This is equivalent to Ignore when reading statically typed rows.
        /// </summary>
        IncludeDynamic = 2,
        /// <summary>
        /// Throw an exception when a column beyond those in the header (
        /// or first row, if no header is present) is encountered.
        /// </summary>
        ThrowException = 3
    }
}
