// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Tests.Common.ConfigurableRetryLogic;

public sealed class RetryLogicConfigs
{
    public TimeSpan DeltaTime { get; set; }
    public TimeSpan MaxTimeInterval { get; set; }
    public TimeSpan MinTimeInterval { get; set; }
    public int NumberOfTries { get; set; }
    public string? TransientErrors { get; set; }
    public string? AuthorizedSqlCondition { get; set; }
    public string? RetryLogicType { get; set; }
    public string? RetryMethodName { get; set; }
}
