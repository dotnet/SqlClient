// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.SqlClient.UnitTests.Utilities;

/// <summary>
/// Utility exception class that can be used when testing whether an exception is observed.
/// </summary>
/// <remarks>
/// Since testing whether an exception is observed requires hooking into the unobserved exception
/// handler (i.e., global state), there is a race condition with any other tests that may cause
/// unobserved exceptions. Thus, the best practice is to have the simulated unobserved exception
/// be a specific type with a specific identifier.
///
/// So, when testing unobserved exception behavior:
/// * Have the test method that throws an exception throw an exception of this type.
/// * Have the test unobserved exception handler check for this type and validate ID before
///   observing the arguments.
/// </remarks>
public class ObservableException : Exception
{
    /// <summary>
    /// Represents a utility exception designed for testing scenarios where exceptions must be observed.
    /// </summary>
    /// <param name="id">Identifier to uniquely identify the exception.</param>
    public ObservableException(Guid id)
    {
        Identifier = id;
    }

    /// <summary>
    /// Gets or sets the unique identifier associated with the exception instance.
    /// </summary>
    /// <remarks>
    /// This property allows tests to associate a specific, unique identifier with an instance of the
    /// <see cref="ObservableException"/> class. It is crucial when validating exception behavior in
    /// multi-threaded unit test scenarios where unobserved exceptions may occur.
    /// </remarks>
    public Guid Identifier { get; set; }
}
