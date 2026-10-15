// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;
using System.Threading;

namespace Microsoft.Data.SqlClient.Extensions.Abstractions.Test
{
    /// <summary>Verifies Azure discovery handles ordinary constructor failures but propagates wrapped fatal errors.</summary>
    [Collection(AuthenticationFeatureSwitchTestCollection.Name)]
    public class AzureExtensionDiscoveryTest
    {
        /// <summary>Exercises the discovery catch filter with exceptions wrapped by reflective provider construction.</summary>
        [Theory]
        [InlineData(typeof(InvalidOperationException), true)]
        [InlineData(typeof(OutOfMemoryException), false)]
        [InlineData(typeof(StackOverflowException), false)]
        [InlineData(typeof(AccessViolationException), false)]
        [InlineData(typeof(ThreadAbortException), false)]
        public void ProviderConstructorFailureHonorsRecoverability(Type exceptionType, bool recoverable)
        {
            Exception failure = (Exception)Activator.CreateInstance(exceptionType, nonPublic: true)!;
            ThrowingProvider.ConstructorFailure = failure;
            try
            {
                var registry = new SqlAuthenticationProviderRegistry();
                if (recoverable)
                {
                    registry.LoadAzureExtension(() => typeof(ThrowingProvider).Assembly);
                }
                else
                {
                    TargetInvocationException constructor = Assert.Throws<TargetInvocationException>(
                        () => registry.LoadAzureExtension(() => typeof(ThrowingProvider).Assembly));
                    Assert.Same(failure, constructor.InnerException);
                }
            }
            finally
            {
                ThrowingProvider.ConstructorFailure = null;
            }
        }

        /// <summary>Assembly loading failures use the same catch policy as reflective provider construction.</summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void AssemblyLoaderFailureHonorsRecoverability(bool recoverable)
        {
            Exception failure = recoverable
                ? new FileNotFoundException("Test Azure assembly is absent.")
                : new TargetInvocationException(new OutOfMemoryException());
            var registry = new SqlAuthenticationProviderRegistry();
            if (recoverable)
            {
                registry.LoadAzureExtension(() => throw failure);
            }
            else
            {
                Assert.Same(failure, Assert.Throws<TargetInvocationException>(
                    () => registry.LoadAzureExtension(() => throw failure)));
            }
        }
    }

    /// <summary>Simulates Azure provider constructor failures without depending on the Azure package.</summary>
    public class ThrowingProvider : SqlAuthenticationProvider
    {
        internal static Exception? ConstructorFailure { get; set; }

        public ThrowingProvider()
        {
            if (ConstructorFailure is Exception failure)
            {
                throw failure;
            }
        }

        public override bool IsSupported(SqlAuthenticationMethod authenticationMethod) => true;

        public override Task<SqlAuthenticationToken> AcquireTokenAsync(SqlAuthenticationParameters parameters) =>
            throw new NotSupportedException();
    }
}

namespace Microsoft.Data.SqlClient
{
    /// <summary>Provides the exact type name required by discovery; test behavior resides in ThrowingProvider.</summary>
    public sealed class ActiveDirectoryAuthenticationProvider : Extensions.Abstractions.Test.ThrowingProvider
    {
    }
}
