// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;

namespace Microsoft.Data.SqlClient.Extensions.Abstractions.Test;

/// <summary>Verifies optional assembly loading without a dependency on the Azure extension.</summary>
[Collection(AuthenticationFeatureSwitchTestCollection.Name)]
public class AzureExtensionLoaderTest
{
    /// <summary>A missing extension does not prevent explicit authentication registration.</summary>
    [Fact]
    public void MissingAzureExtensionReturnsNull()
    {
        Assert.Null(AzureExtensionLoader.Load(nameof(AzureExtensionLoaderTest)));
    }

    /// <summary>Assembly loading ignores recoverable binding errors but preserves fatal inner failures.</summary>
    [Theory]
    [InlineData("FileNotFound", false)]
    [InlineData("FileNotFound", true)]
    [InlineData("FileLoad", false)]
    [InlineData("FileLoad", true)]
    [InlineData("BadImageFormat", false)]
    [InlineData("BadImageFormat", true)]
    public void AssemblyResolutionPreservesFatalFailures(string failureType, bool fatal)
    {
        Exception inner = fatal
            ? new OutOfMemoryException("Test fatal assembly resolution failure")
            : new InvalidOperationException("Test recoverable assembly resolution failure");
        Exception failure = failureType switch
        {
            "FileNotFound" => new FileNotFoundException("Test assembly not found", inner),
            "FileLoad" => new FileLoadException("Test assembly load failure", inner),
            "BadImageFormat" => new BadImageFormatException("Test invalid assembly image", inner),
            _ => throw new ArgumentOutOfRangeException(nameof(failureType)),
        };
        bool loadAttempted = false;
        Assembly LoadAssembly(AssemblyName assemblyName)
        {
            Assert.Equal(AzureExtensionLoader.AssemblyName, assemblyName.Name);
            loadAttempted = true;
            throw failure;
        }

        if (fatal)
        {
            Exception actual = Assert.ThrowsAny<Exception>(() =>
                AzureExtensionLoader.Load(nameof(AzureExtensionLoaderTest), LoadAssembly));
            Assert.Same(inner, actual.GetBaseException());
        }
        else
        {
            Assert.Null(AzureExtensionLoader.Load(nameof(AzureExtensionLoaderTest), LoadAssembly));
        }

        Assert.True(loadAttempted);
    }
}
