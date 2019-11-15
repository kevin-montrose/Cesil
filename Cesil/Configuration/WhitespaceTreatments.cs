using System;

namespace Cesil
{
    // todo: implement whitespace treatment and test!

    /// <summary>
    /// How to handle whitespace when encountered during parsing.
    /// 
    /// Allows for automatic removal of leading or trailing whitespace in or outside of
    ///   values or headers, depending on whether in an escaped sequence.
    /// </summary>
    [Flags]
    public enum WhitespaceTreatments : byte
    {
        /// <summary>
        /// Leave whitespace untouched, if in a value or header
        ///   it will be preserved, if before or after an escaped
        ///   value or header it will result in an error.
        /// </summary>
        Preserve = 0,

        /// <summary>
        /// Removes whitespace that is at the start of a value or header.
        /// 
        /// For unescaped values this behaves the same as TrimBeforeValues.
        /// 
        /// For escaped values this removed leading whitespace.
        /// </summary>
        TrimLeadingInValues = 1,
        /// <summary>
        /// Removes whitespace that follows a value or header.
        /// 
        /// For unescaped values this behaves the same as TrimAfterValues.
        /// 
        /// Leading whitespace in escaped values and headers will be
        ///   removed.
        /// </summary>
        TrimTrailingInValues = 2,
        /// <summary>
        /// Combines TrimLeadingInValues and TrimTrailingInValues.
        /// </summary>
        TrimInValues = TrimLeadingInValues | TrimTrailingInValues,

        /// <summary>
        /// Removes whitespace before a value or header.
        /// 
        /// For unescaped values this behaves the same as TrimLeadingInValues.
        /// 
        /// For escaped values, only whitespace before the start of escaped value will be
        ///   removed.
        /// </summary>
        TrimBeforeValues = 4,
        /// <summary>
        /// Removes whitespace after a value or header.
        /// 
        /// For unescaped values this behaves the same as TrimTrailingInValues.
        /// 
        /// For escaped values, only whitespace after the end of escaped value will be
        ///   removed.
        /// </summary>
        TrimAfterValues = 8,
        /// <summary>
        /// Combines TrimBeforeValues and TrimAfterValues.
        /// </summary>
        TrimBetweenValues = TrimBeforeValues | TrimAfterValues,

        /// <summary>
        /// Combines TrimInValues and TrimBetweenValues.
        /// </summary>
        Trim = TrimInValues | TrimBetweenValues
    }
}
