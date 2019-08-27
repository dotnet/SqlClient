// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data
{
    using System;
    using System.ComponentModel;

    [AttributeUsage(AttributeTargets.All)]
    internal sealed class ResCategoryAttribute : CategoryAttribute
    {

        public ResCategoryAttribute(string category) : base(category)
        {
        }

        protected override string GetLocalizedString(string value)
        {
            return StringsHelper.GetString(value);
        }
    }
}
