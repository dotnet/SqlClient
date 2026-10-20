// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;

namespace Microsoft.Data.SqlClient.Extensions.Abstractions.Test
{
    /// <summary>Verifies Azure discovery handles ordinary constructor failures but propagates fatal errors.</summary>
    [Collection(AuthenticationFeatureSwitchTestCollection.Name)]
    public class AzureExtensionDiscoveryTest
    {
        /// <summary>Exercises the discovery catch filter with direct or reflection-wrapped constructor exceptions.</summary>
        [Theory]
        [InlineData(typeof(InvalidOperationException), true)]
        [InlineData(typeof(OutOfMemoryException), false)]
        [InlineData(typeof(StackOverflowException), false)]
        [InlineData(typeof(AccessViolationException), false)]
        [InlineData(typeof(ThreadAbortException), false)]
        public void ProviderConstructorFailureHonorsRecoverability(Type exceptionType, bool recoverable)
        {
            // Throw a known exception from the simulated Azure provider's reflective constructor.
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
                    Exception? constructor = Record.Exception(
                        () => registry.LoadAzureExtension(() => typeof(ThrowingProvider).Assembly));
                    // .NET Framework propagates some fatal constructor exceptions without a reflection wrapper.
                    Assert.Same(failure, constructor is TargetInvocationException invocation
                        ? invocation.InnerException
                        : constructor);
                }
            }
            finally
            {
                // Keep constructor failure injection from affecting subsequent discovery tests.
                ThrowingProvider.ConstructorFailure = null;
            }
        }

        /// <summary>Assembly loading failures use the same catch policy as reflective provider construction.</summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void AssemblyLoaderFailureHonorsRecoverability(bool recoverable)
        {
            // Missing optional assemblies are recoverable; reflection-wrapped fatal errors are not.
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

        /// <summary>Recoverable loading failures are handled even when they are not reflection or binding errors.</summary>
        [Theory]
        [InlineData(typeof(InvalidOperationException))]
        [InlineData(typeof(IOException))]
        [InlineData(typeof(UnauthorizedAccessException))]
        public void RecoverableAssemblyLoaderFailureIsHandled(Type exceptionType)
        {
            // Optional discovery must tolerate ordinary loader errors beyond binding exceptions.
            Exception failure = (Exception)Activator.CreateInstance(exceptionType)!;
            var registry = new SqlAuthenticationProviderRegistry();

            registry.LoadAzureExtension(() => throw failure);
        }
    }

    /// <summary>Simulates Azure provider constructor failures without depending on the Azure package.</summary>
    public class ThrowingProvider : SqlAuthenticationProvider
    {
        internal static Exception? ConstructorFailure { get; set; }
        internal static Action<ActiveDirectoryAuthenticationProviderOptions>? OnConstructed { get; set; }

        public ThrowingProvider(ActiveDirectoryAuthenticationProviderOptions options)
        {
            if (ConstructorFailure is Exception failure)
            {
                throw failure;
            }
            OnConstructed?.Invoke(options);
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
        public ActiveDirectoryAuthenticationProvider(ActiveDirectoryAuthenticationProviderOptions options)
            : base(options)
        {
        }
    }

    /// <summary>Provides the options contract required by reflective Azure discovery.</summary>
    public sealed class ActiveDirectoryAuthenticationProviderOptions
    {
        public string? ApplicationClientId { get; set; }
        public bool UseWamBroker { get; set; }
    }
}
