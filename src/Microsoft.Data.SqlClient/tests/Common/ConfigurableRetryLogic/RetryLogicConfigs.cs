// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Tests.Common.ConfigurableRetryLogic;

/// <summary>
/// Transport object for retry logic configuration parameters. This is used to pass
/// values which would ordinarily be part of the SqlConfigurableRetryLogicConnection
/// or SqlConfigurableRetryLogicCommand section to <see cref="RetryLogicLoaderHelper.CreateLoader"/>
/// </summary>
public sealed class RetryLogicConfigs
{
    /// <summary>
    /// Equivalent of the deltaTime value in an app.config file.
    /// </summary>
    public TimeSpan DeltaTime { get; set; }

    /// <summary>
    /// Equivalent of the maxTime value in an app.config file.
    /// </summary>
    public TimeSpan MaxTimeInterval { get; set; }

    /// <summary>
    /// Equivalent of the minTime value in an app.config file.
    /// </summary>
    public TimeSpan MinTimeInterval { get; set; }

    /// <summary>
    /// Equivalent of the numberOfTries value in an app.config file.
    /// </summary>
    public int NumberOfTries { get; set; }

    /// <summary>
    /// Equivalent of the transientErrors value in an app.config file.
    /// </summary>
    public string? TransientErrors { get; set; }

    /// <summary>
    /// Equivalent of the authorizedSqlCondition value in an app.config file.
    /// </summary>
    public string? AuthorizedSqlCondition { get; set; }

    /// <summary>
    /// Equivalent of the retryLogicType value in an app.config file.
    /// </summary>
    public string? RetryLogicType { get; set; }

    /// <summary>
    /// Equivalent of the retryMethod value in an app.config file.
    /// </summary>
    public string? RetryMethodName { get; set; }
}
