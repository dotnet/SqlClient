// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.DotNet.XUnitExtensions
{
    public struct XunitConstants
    {
        internal const string NonFreeBSDTest = "nonfreebsdtests";
        internal const string NonLinuxTest = "nonlinuxtests";
        internal const string NonNetBSDTest = "nonnetbsdtests";
        internal const string NonOSXTest = "nonosxtests";
        internal const string NonWindowsTest = "nonwindowstests";

        internal static string NonNetcoreappTest = "nonnetcoreapptests";
        internal static string NonNetfxTest = "nonnetfxtests";
        internal static string NonUapTest = "nonuaptests";
        internal static string NonMonoTest = "nonmonotests";

        internal const string Failing = "failing";
        internal const string ActiveIssue = "activeissue";
        internal const string OuterLoop = "outerloop";

        public const string Category = "category";
        public const string IgnoreForCI = "ignoreforci";
        public const string RequiresElevation = "requireselevation";
    }
}
