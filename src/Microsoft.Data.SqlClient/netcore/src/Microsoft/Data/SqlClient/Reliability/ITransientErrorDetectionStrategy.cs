// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.Data.SqlClient.Reliability
{
    /// <summary>
    /// Defines an interface that must be implemented by custom components responsible for detecting specific transient conditions.
    /// </summary>
    public interface ITransientErrorDetectionStrategy
    {
        /// <summary>
        /// Determines whether the specified exception represents a transient failure that can be compensated by a retry.
        /// </summary>
        /// <param name="ex">The exception object to be verified.</param>
        /// <returns>true if the specified exception is considered as transient; otherwise, false.</returns>
        bool IsTransient(Exception ex);

        /// <summary>
        /// Determines whether the specified exception represents a transient failure that can be compensated by a retry.
        /// </summary>
        /// <returns>true if the specified exception is considered as transient; otherwise, false.</returns>
        List<int> RetriableErrors
        {
            get;
        }
    }
}
