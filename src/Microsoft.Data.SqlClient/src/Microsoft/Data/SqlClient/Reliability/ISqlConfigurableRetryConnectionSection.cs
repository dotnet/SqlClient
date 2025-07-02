// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.SqlClient
{
    internal interface ISqlConfigurableRetryConnectionSection
    {
        TimeSpan DeltaTime { get; set; }
        
        TimeSpan MaxTimeInterval { get; set; }
        
        TimeSpan MinTimeInterval { get; set; }
        
        int NumberOfTries { get; set; }
        
        string RetryLogicType { get; set; }
        
        string RetryMethod { get; set; }
        
        string TransientErrors { get; set; }
    }
}
