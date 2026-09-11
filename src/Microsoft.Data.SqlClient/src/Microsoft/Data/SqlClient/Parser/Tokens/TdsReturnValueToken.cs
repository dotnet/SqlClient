// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Parser.Tokens;

/// <summary>
/// Represents a token that encapsulates information about a return value from a SQL Server stored
/// procedure or function. This represents the SQLRETURNVALUE token.
/// </summary>
/// <remarks>
/// This token is used internally by the TDS (Tabular Data Stream) parser to handle return values
/// associated with parameters in TDS responses. It contains metadata describing the parameter and
/// its value.
/// </remarks>
internal sealed class TdsReturnValueToken : TdsTypeInfo
{
    /// <summary>
    /// Gets or sets the name of the parameter associated with a returned value.
    /// </summary>
    // @TODO: This cannot be an auto property yet because this value is set via an out parameter in TryReadString.
    internal string parameter;

    /// <summary>
/// Constructs a new instance of TdsReturnValueToken with the value initialized to a new
    /// <see cref="SqlBuffer"/> instance.
    /// </summary>
    internal TdsReturnValueToken() : base()
    {
        Value = new SqlBuffer();
    }

    /// <summary>
    /// Gets the value associated with the return parameter token.
    /// </summary>
    internal SqlBuffer Value { get; }
}
