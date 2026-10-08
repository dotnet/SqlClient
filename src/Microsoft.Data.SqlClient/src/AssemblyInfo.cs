// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NETFRAMEWORK
using System.Runtime.CompilerServices;

// Legacy grant carried over in the original source import (#166), preserved during relocation.
// For the investigation of its historical System.Data dependency, see:
// https://github.com/dotnet/SqlClient/issues/3029#issuecomment-4963370482
[assembly: InternalsVisibleTo("System.Data.DataSetExtensions, PublicKey=" + Microsoft.Data.SqlClient.AssemblyRef.EcmaPublicKeyFull)] // DevDiv Bugs 92166
#endif
