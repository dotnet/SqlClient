// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClientX.Tds.Tokens.EnvChange
{
    /// <summary>
    /// Environment change token.
    /// </summary>
    /// <typeparam name="T">Type of value.</typeparam>
    internal abstract class EnvChangeToken<T> : Token
    {

        /// <summary>
        /// Token type.
        /// </summary>
        public override TokenType Type => TokenType.EnvChange;

        /// <summary>
        /// Token sub type.
        /// </summary>
        public abstract EnvChangeTokenSubType SubType { get; }

        /// <summary>
        /// Old value.
        /// </summary>
        public T OldValue { get; protected set; }

        /// <summary>
        /// New value.
        /// </summary>
        public T NewValue { get; protected set; }

        /// <summary>
        /// Create a new token.
        /// </summary>
        protected EnvChangeToken()
        {
        }

        /// <summary>
        /// Create a new token.
        /// </summary>
        /// <param name="oldValue">Old value.</param>
        /// <param name="newValue">New value.</param>
        protected EnvChangeToken(T oldValue, T newValue)
        {
            OldValue = oldValue;
            NewValue = newValue;
        }

        /// <summary>
        /// Gets a human readable string representation of this token.
        /// </summary>
        /// <returns>Human readable string representation.</returns>
        public override string ToString()
        {
            return $"EnvChangeToken[SubType={SubType}, NewValue={NewValue}, OldValue={OldValue}]";
        }

    }
}
