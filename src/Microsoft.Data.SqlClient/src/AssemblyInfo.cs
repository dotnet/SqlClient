// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NETFRAMEWORK
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("System.Data.DataSetExtensions, PublicKey=" + Microsoft.Data.SqlClient.AssemblyRef.EcmaPublicKeyFull)] // DevDiv Bugs 92166
#endif
