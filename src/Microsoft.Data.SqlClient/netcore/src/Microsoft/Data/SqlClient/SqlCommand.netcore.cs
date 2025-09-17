// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Common;
using Microsoft.Data.SqlClient.Diagnostics;

// NOTE: The current Microsoft.VSDesigner editor attributes are implemented for System.Data.SqlClient, and are not publicly available.
// New attributes that are designed to work with Microsoft.Data.SqlClient and are publicly documented should be included in future.
namespace Microsoft.Data.SqlClient
{
    // TODO: Add designer attribute when Microsoft.VSDesigner.Data.VS.SqlCommandDesigner uses Microsoft.Data.SqlClient
    public sealed partial class SqlCommand : DbCommand, ICloneable
    {
    }
}
