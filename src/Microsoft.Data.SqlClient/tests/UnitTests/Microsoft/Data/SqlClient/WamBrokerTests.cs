// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.Microsoft.Data.SqlClient
{
    [Collection("SqlAuthenticationProvider")]
    public class WamBrokerTests
    {
        // The SqlClient first-party application client id that is hard-coded in the provider.
        private const string SqlClientApplicationId = "2fd908ad-0664-4344-b9be-cd3e8b574c38";

        // A fixed, deterministic stand-in for a caller-supplied application id. Hard-coded (instead
        // of Guid.NewGuid()) so test outcomes don't depend on RNG and so a single point asserts
        // that this value differs from the SqlClient first-party id.
        private const string TestCustomAppId = "11111111-2222-3333-4444-555555555555";

        // Reads the private _parentActivityOrWindowFunc field. Used to assert downstream effects
        // of SetParentActivityOrWindowFunc without triggering a live MSAL flow.
        private static Func<object> GetParentActivityOrWindowFunc(ActiveDirectoryAuthenticationProvider provider)
        {
            FieldInfo field = typeof(ActiveDirectoryAuthenticationProvider).GetField(
                "_parentActivityOrWindowFunc",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            return (Func<object>)field.GetValue(provider);
        }

        /// <summary>
        /// A <see langword="null"/> callback is treated as "clear any previously installed callback"
        /// and must not throw.
        /// </summary>
        [Fact]
        public void SetParentActivityOrWindowFunc_Null_ClearsCallback()
        {
            var provider = new ActiveDirectoryAuthenticationProvider();
            Func<object> first = () => IntPtr.Zero;
            provider.SetParentActivityOrWindowFunc(first);
            Assert.Same(first, GetParentActivityOrWindowFunc(provider));

            provider.SetParentActivityOrWindowFunc(null);
            Assert.Null(GetParentActivityOrWindowFunc(provider));
        }

        /// <summary>
        /// The single-string constructor with the SqlClient first-party application id always
        /// enables WAM broker mode.
        /// </summary>
        [Fact]
        public void Ctor_ApplicationClientId_EnablesWamBroker()
        {
            var provider = new ActiveDirectoryAuthenticationProvider(SqlClientApplicationId);
            Assert.Equal(SqlClientApplicationId, provider.ApplicationClientId);
            Assert.True(provider.UseWamBroker,
                "Constructor with SqlClient first-party application id must enable WAM broker.");
        }

        /// <summary>
        /// The parameterless constructor uses the SqlClient first-party application id, which always
        /// enables WAM broker mode regardless of any opt-in flag.
        /// </summary>
        [Fact]
        public void Ctor_Default_EnablesWamBroker()
        {
            var provider = new ActiveDirectoryAuthenticationProvider();
            Assert.Equal(SqlClientApplicationId, provider.ApplicationClientId);
            Assert.True(provider.UseWamBroker,
                "Default ctor must enable WAM broker (uses SqlClient first-party application id).");
        }

        /// <summary>A caller-supplied application id without explicit opt-in must NOT enable WAM broker.</summary>
        [Fact]
        public void Ctor_AppClientId_DefaultsUseWamBrokerToFalse()
        {
            var provider = new ActiveDirectoryAuthenticationProvider(TestCustomAppId);

            Assert.Equal(TestCustomAppId, provider.ApplicationClientId);
            Assert.False(provider.UseWamBroker,
                "Custom application id without useWamBroker=true must keep WAM broker disabled.");
        }

        /// <summary>
        /// The Options constructor with only ApplicationClientId set defaults UseWamBroker to false.
        /// </summary>
        [Fact]
        public void Ctor_Options_AppClientIdOnly_DefaultsUseWamBrokerToFalse()
        {
            var provider = new ActiveDirectoryAuthenticationProvider(
                new ActiveDirectoryAuthenticationProviderOptions
                {
                    ApplicationClientId = TestCustomAppId,
                });

            Assert.Equal(TestCustomAppId, provider.ApplicationClientId);
            Assert.False(provider.UseWamBroker,
                "Options ctor with ApplicationClientId set and UseWamBroker omitted must keep WAM broker disabled.");
        }

        /// <summary>A caller-supplied application id with explicit opt-in must enable WAM broker.</summary>
        [Fact]
        public void Ctor_AppClientId_UseWamBrokerTrue_EnablesWamBroker()
        {
            var provider = new ActiveDirectoryAuthenticationProvider(
                new ActiveDirectoryAuthenticationProviderOptions
                {
                    ApplicationClientId = TestCustomAppId,
                    UseWamBroker = true,
                });

            Assert.Equal(TestCustomAppId, provider.ApplicationClientId);
            Assert.True(provider.UseWamBroker,
                "Custom application id with UseWamBroker=true must enable WAM broker.");
        }

        /// <summary>A caller-supplied application id with explicit opt-out keeps WAM broker disabled.</summary>
        [Fact]
        public void Ctor_AppClientId_UseWamBrokerFalse_DisablesWamBroker()
        {
            var provider = new ActiveDirectoryAuthenticationProvider(
                new ActiveDirectoryAuthenticationProviderOptions
                {
                    ApplicationClientId = TestCustomAppId,
                    UseWamBroker = false,
                });

            Assert.Equal(TestCustomAppId, provider.ApplicationClientId);
            Assert.False(provider.UseWamBroker);
        }

        /// <summary>
        /// Even when the SqlClient first-party application id is passed explicitly with
        /// UseWamBroker=false, WAM broker mode must remain enabled because the first-party
        /// app id is hard-wired to the WAM broker redirect URI.
        /// </summary>
        [Fact]
        public void Ctor_SqlClientAppIdExplicit_UseWamBrokerFalse_StillEnablesWamBroker()
        {
            var provider = new ActiveDirectoryAuthenticationProvider(
                new ActiveDirectoryAuthenticationProviderOptions
                {
                    ApplicationClientId = SqlClientApplicationId,
                    UseWamBroker = false,
                });

            Assert.Equal(SqlClientApplicationId, provider.ApplicationClientId);
            Assert.True(provider.UseWamBroker,
                "SqlClient first-party application id must always enable WAM broker, regardless of the UseWamBroker option.");
        }

        [Fact]
        public void Ctor_WithDeviceCodeCallback_UseWamBrokerTrue_EnablesWamBroker()
        {
            var provider = new ActiveDirectoryAuthenticationProvider(
                new ActiveDirectoryAuthenticationProviderOptions
                {
                    DeviceCodeFlowCallback = _ => Task.CompletedTask,
                    ApplicationClientId = TestCustomAppId,
                    UseWamBroker = true,
                });

            Assert.Equal(TestCustomAppId, provider.ApplicationClientId);
            Assert.True(provider.UseWamBroker);
        }

        /// <summary>
        /// The two-arg device-code constructor (deviceCodeCallback, applicationClientId) must default
        /// useWamBroker to false for caller-supplied application ids.
        /// </summary>
        [Fact]
        public void Ctor_WithDeviceCodeCallback_AppClientIdOnly_DefaultsUseWamBrokerToFalse()
        {
            var provider = new ActiveDirectoryAuthenticationProvider(
                deviceCodeFlowCallbackMethod: _ => Task.CompletedTask,
                applicationClientId: TestCustomAppId);

            Assert.False(provider.UseWamBroker);
            Assert.NotEqual(SqlClientApplicationId, provider.ApplicationClientId);
        }

        /// <summary>
        /// When the device-code callback constructor is invoked without an application id, the
        /// provider falls back to the SqlClient first-party id and must enable WAM broker.
        /// </summary>
        [Fact]
        public void Ctor_WithDeviceCodeCallback_NoAppClientId_EnablesWamBroker()
        {
            var provider = new ActiveDirectoryAuthenticationProvider(
                deviceCodeFlowCallbackMethod: _ => Task.CompletedTask);

            Assert.True(provider.UseWamBroker);
            Assert.Equal(SqlClientApplicationId, provider.ApplicationClientId);
        }

        [Fact]
        public void Ctor_Options_CustomAppId_UseWamBrokerTrue_EnablesWamBroker()
        {
            var provider = new ActiveDirectoryAuthenticationProvider(
                new ActiveDirectoryAuthenticationProviderOptions
                {
                    ApplicationClientId = TestCustomAppId,
                    UseWamBroker = true,
                });

            Assert.Equal(TestCustomAppId, provider.ApplicationClientId);
            Assert.True(provider.UseWamBroker);
        }

        /// <summary>
        /// Options with ApplicationClientId = null falls back to the SqlClient first-party
        /// id, which always enables WAM broker, regardless of UseWamBroker.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Ctor_Options_NullAppId_AlwaysEnablesWamBroker(bool useWamBroker)
        {
            var provider = new ActiveDirectoryAuthenticationProvider(
                new ActiveDirectoryAuthenticationProviderOptions
                {
                    ApplicationClientId = null,
                    UseWamBroker = useWamBroker,
                });

            Assert.Equal(SqlClientApplicationId, provider.ApplicationClientId);
            Assert.True(provider.UseWamBroker);
        }

        /// <summary>
        /// The Options-based constructor must reject a null options instance with
        /// ArgumentNullException so misuse fails fast at construction.
        /// </summary>
        [Fact]
        public void Ctor_Options_Null_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(
                () => new ActiveDirectoryAuthenticationProvider((ActiveDirectoryAuthenticationProviderOptions)null));
        }

        /// <summary>
        /// Registering an instance via SqlAuthenticationProvider.SetProvider must not
        /// wrap or replace the instance, so its WAM broker setting survives registration.
        /// </summary>
        [Fact]
        public void Ctor_RegisteredAsProvider_PreservesUseWamBrokerSetting()
        {
            var provider = new ActiveDirectoryAuthenticationProvider(
                new ActiveDirectoryAuthenticationProviderOptions
                {
                    ApplicationClientId = TestCustomAppId,
                    UseWamBroker = true,
                });

            SqlAuthenticationProvider original =
                SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryInteractive);
            try
            {
                SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryInteractive, provider);

                var retrieved = SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryInteractive)
                    as ActiveDirectoryAuthenticationProvider;
                Assert.NotNull(retrieved);
                Assert.Same(provider, retrieved);
                Assert.Equal(TestCustomAppId, retrieved.ApplicationClientId);
                Assert.True(retrieved.UseWamBroker);
            }
            finally
            {
                if (original != null)
                {
                    SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryInteractive, original);
                }
            }
        }
    }
}
