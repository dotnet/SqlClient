// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NETFRAMEWORK || WINDOWS

using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests
{
    public class TdsParserStateObjectNativeTests
    {
        [Theory]
        [InlineData(null, true, "")]           // Integrated + null -> empty (generate SPN)
        [InlineData("", true, "")]             // Integrated + empty -> empty (generate SPN)
        [InlineData("MSSQLSvc/host", true, "MSSQLSvc/host")] // Integrated + provided -> use it
        [InlineData(null, false, null)]        // SQL Auth + null -> null (no generation)
        [InlineData("", false, null)]          // SQL Auth + empty -> null (no generation)
        [InlineData("MSSQLSvc/host", false, "MSSQLSvc/host")] // SQL Auth + provided -> use it
        [PlatformSpecific(TestPlatforms.Windows)]
        public void NormalizeServerSpn_ReturnsExpectedValue(
            string? inputSpn,
            bool isIntegratedSecurity,
            string? expectedSpn)
        {
            string? result = TdsParserStateObjectNative.NormalizeServerSpn(inputSpn, isIntegratedSecurity);
            Assert.Equal(expectedSpn, result);
        }
    }
}

#endif
