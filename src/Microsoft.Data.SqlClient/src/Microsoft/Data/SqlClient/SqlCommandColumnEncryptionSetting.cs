// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient;

/// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandColumnEncryptionSetting.xml' path='docs/members[@name="SqlCommandColumnEncryptionSetting"]/SqlCommandColumnEncryptionSetting/*' />
public enum SqlCommandColumnEncryptionSetting
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandColumnEncryptionSetting.xml' path='docs/members[@name="SqlCommandColumnEncryptionSetting"]/UseConnectionSetting/*' />
    UseConnectionSetting = 0,

    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandColumnEncryptionSetting.xml' path='docs/members[@name="SqlCommandColumnEncryptionSetting"]/Enabled/*' />
    Enabled,

    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandColumnEncryptionSetting.xml' path='docs/members[@name="SqlCommandColumnEncryptionSetting"]/ResultSetOnly/*' />
    ResultSetOnly,

    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandColumnEncryptionSetting.xml' path='docs/members[@name="SqlCommandColumnEncryptionSetting"]/Disabled/*' />
    Disabled,
}
