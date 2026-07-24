// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// -----------------------------------------------------------------------------
// Global using aliases that let the linked test files (which use bare
// Microsoft.Data.SqlClient type names) bind to the LEGACY System.Data.SqlClient
// driver instead.
//
// How this works: the linked files live in namespaces under
// Microsoft.Data.SqlClient.*, so an unqualified name like "SqlConnection" is
// resolved by walking the enclosing namespaces. Because this assembly does NOT
// reference Microsoft.Data.SqlClient, none of those namespaces define these
// types, so name resolution falls through to these global aliases.
//
// SqlConnectionStringBuilder and SqlConnectionEncryptOption are aliased to shim
// types (see Compat.cs) because the legacy builder's Encrypt property is a bool,
// whereas the tests assign the Microsoft.Data-only SqlConnectionEncryptOption
// enum. The shim reconciles that difference.
// -----------------------------------------------------------------------------

global using SqlConnection = System.Data.SqlClient.SqlConnection;
global using SqlCommand = System.Data.SqlClient.SqlCommand;
global using SqlDataReader = System.Data.SqlClient.SqlDataReader;
global using SqlConnectionStringBuilder = SniCloseLegacyRepro.Compat.ShimSqlConnectionStringBuilder;
global using SqlConnectionEncryptOption = SniCloseLegacyRepro.Compat.SqlConnectionEncryptOption;
