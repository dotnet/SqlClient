// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient;

/// <include file='../doc/SqlAuthenticationProvider.xml' path='docs/members[@name="SqlAuthenticationProvider"]/SqlAuthenticationProvider/*'/>
public abstract partial class SqlAuthenticationProvider
{
    /// <include file='../doc/SqlAuthenticationProvider.xml' path='docs/members[@name="SqlAuthenticationProvider"]/BeforeLoad/*'/>
    public virtual void BeforeLoad(SqlAuthenticationMethod authenticationMethod) { }

    /// <include file='../doc/SqlAuthenticationProvider.xml' path='docs/members[@name="SqlAuthenticationProvider"]/BeforeUnload/*'/>
    public virtual void BeforeUnload(SqlAuthenticationMethod authenticationMethod) { }

    /// <include file='../doc/SqlAuthenticationProvider.xml' path='docs/members[@name="SqlAuthenticationProvider"]/IsSupported/*'/>
    public abstract bool IsSupported(SqlAuthenticationMethod authenticationMethod);

    /// <include file='../doc/SqlAuthenticationProvider.xml' path='docs/members[@name="SqlAuthenticationProvider"]/AcquireTokenAsync/*'/>
    public abstract Task<SqlAuthenticationToken> AcquireTokenAsync(SqlAuthenticationParameters parameters);


    /// <include file='../doc/SqlAuthenticationProvider.xml' path='docs/members[@name="SqlAuthenticationProvider"]/GetProvider/*'/>
    //
    // We would like to deprecate this method in favour of
    // SqlAuthenticationProviderManager.GetProvider().  See:
    //
    // https://microsoft.sharepoint-df.com/:fl:/g/contentstorage/CSP_e68c6b62-34b4-4eaa-b836-82e9cdaa0149/IQCyPmTP5HlYSpafY3DJ-8sQAbY4Ajjn2ztRZrM_eQZkyJQ?e=k1nHJd&nav=cz0lMkZjb250ZW50c3RvcmFnZSUyRkNTUF9lNjhjNmI2Mi0zNGI0LTRlYWEtYjgzNi04MmU5Y2RhYTAxNDkmZD1iJTIxWW11TTVyUTBxazY0Tm9McHphb0JTYXhVNHFkaEY5ZE9yS0ZkWTR0cDY3WU5rRUhKaHM0R1JJTjhQanNwcGliSyZmPTAxWklZTVRaNVNIWlNNN1pEWkxCRkpOSDNET0RFN1hTWVEmYz0lMkYmYT1Mb29wQXBwJnA9JTQwZmx1aWR4JTJGbG9vcC1wYWdlLWNvbnRhaW5lciZ4PSU3QiUyMnclMjIlM0ElMjJUMFJUVUh4dGFXTnliM052Wm5RdWMyaGhjbVZ3YjJsdWRDMWtaaTVqYjIxOFlpRlpiWFZOTlhKUk1IRnJOalJPYjB4d2VtRnZRbE5oZUZVMGNXUm9SamxrVDNKTFJtUlpOSFJ3TmpkWlRtdEZTRXBvY3pSSFVrbE9PRkJxYzNCd2FXSkxmREF4V2tsWlRWUmFXbE5DTlVVMFJrMVFSemRhUlROWlV6Vk9SVkZDTmxkRE1rRSUzRCUyMiUyQyUyMmklMjIlM0ElMjI1YzA2ZTE4OS03NWExLTRkNDktYjQyYi1iOTk2YmM4MDc4ZjklMjIlN0Q%3D
    //
    public static SqlAuthenticationProvider? GetProvider(
        SqlAuthenticationMethod authenticationMethod)
    {
        return Internal.GetProvider(authenticationMethod);
    }
    
    /// <include file='../doc/SqlAuthenticationProvider.xml' path='docs/members[@name="SqlAuthenticationProvider"]/SetProvider/*'/>
    //
    // We would like to deprecate this method in favour of
    // SqlAuthenticationProviderManager.SetProvider().  See:
    //
    // https://microsoft.sharepoint-df.com/:fl:/g/contentstorage/CSP_e68c6b62-34b4-4eaa-b836-82e9cdaa0149/IQCyPmTP5HlYSpafY3DJ-8sQAbY4Ajjn2ztRZrM_eQZkyJQ?e=k1nHJd&nav=cz0lMkZjb250ZW50c3RvcmFnZSUyRkNTUF9lNjhjNmI2Mi0zNGI0LTRlYWEtYjgzNi04MmU5Y2RhYTAxNDkmZD1iJTIxWW11TTVyUTBxazY0Tm9McHphb0JTYXhVNHFkaEY5ZE9yS0ZkWTR0cDY3WU5rRUhKaHM0R1JJTjhQanNwcGliSyZmPTAxWklZTVRaNVNIWlNNN1pEWkxCRkpOSDNET0RFN1hTWVEmYz0lMkYmYT1Mb29wQXBwJnA9JTQwZmx1aWR4JTJGbG9vcC1wYWdlLWNvbnRhaW5lciZ4PSU3QiUyMnclMjIlM0ElMjJUMFJUVUh4dGFXTnliM052Wm5RdWMyaGhjbVZ3YjJsdWRDMWtaaTVqYjIxOFlpRlpiWFZOTlhKUk1IRnJOalJPYjB4d2VtRnZRbE5oZUZVMGNXUm9SamxrVDNKTFJtUlpOSFJ3TmpkWlRtdEZTRXBvY3pSSFVrbE9PRkJxYzNCd2FXSkxmREF4V2tsWlRWUmFXbE5DTlVVMFJrMVFSemRhUlROWlV6Vk9SVkZDTmxkRE1rRSUzRCUyMiUyQyUyMmklMjIlM0ElMjI1YzA2ZTE4OS03NWExLTRkNDktYjQyYi1iOTk2YmM4MDc4ZjklMjIlN0Q%3D
    //
    public static bool SetProvider(
        SqlAuthenticationMethod authenticationMethod,
        SqlAuthenticationProvider provider)
    {
        return Internal.SetProvider(authenticationMethod, provider);
    }
}
