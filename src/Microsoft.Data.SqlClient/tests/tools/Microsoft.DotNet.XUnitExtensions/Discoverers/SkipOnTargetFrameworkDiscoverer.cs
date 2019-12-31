// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Xunit.Abstractions;
using Xunit.Sdk;
using Xunit;

namespace Microsoft.DotNet.XUnitExtensions
{
    /// <summary>
    /// This class discovers all of the tests and test classes that have
    /// applied the TestOnTargetFrameworkDiscoverer attribute
    /// </summary>
    public class SkipOnTargetFrameworkDiscoverer : ITraitDiscoverer
    {
        /// <summary>
        /// Gets the trait values from the Category attribute.
        /// </summary>
        /// <param name="traitAttribute">The trait attribute containing the trait values.</param>
        /// <returns>The trait values.</returns>
        public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
        {
            TargetFrameworkMonikers frameworks = (TargetFrameworkMonikers)traitAttribute.GetConstructorArguments().First();
            if (frameworks.HasFlag(TargetFrameworkMonikers.Netcoreapp))
                yield return new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.NonNetcoreappTest);
            if (frameworks.HasFlag(TargetFrameworkMonikers.NetFramework))
                yield return new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.NonNetfxTest);
            if (frameworks.HasFlag(TargetFrameworkMonikers.Uap))
                yield return new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.NonUapTest);
            if (frameworks.HasFlag(TargetFrameworkMonikers.Mono))
                yield return new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.NonMonoTest);
        }
    }
}
