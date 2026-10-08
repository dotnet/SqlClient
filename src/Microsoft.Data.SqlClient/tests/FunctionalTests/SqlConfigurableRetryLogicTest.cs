// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    public class SqlConfigurableRetryLogicTest
    {
        [Fact]
        public async Task InvalidExecute()
        {
            SqlRetryLogicOption option = new SqlRetryLogicOption()
            {
                NumberOfTries = 5,
                DeltaTime = TimeSpan.FromSeconds(10),
                MinTimeInterval = TimeSpan.Zero,
                MaxTimeInterval = TimeSpan.FromSeconds(120)
            };

            SqlRetryLogicBaseProvider retryLogicProvider = SqlConfigurableRetryFactory.CreateFixedRetryProvider(option);

            Assert.Throws<ArgumentNullException>(() => retryLogicProvider.Execute<int>(null, null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => retryLogicProvider.ExecuteAsync(null, null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => retryLogicProvider.ExecuteAsync<int>(null, null));
        }

        [Fact]
        public void InvalidCRLFactoryCreation()
        {
            Assert.Throws<ArgumentNullException>(() => SqlConfigurableRetryFactory.CreateFixedRetryProvider(null));
            Assert.Throws<ArgumentNullException>(() => SqlConfigurableRetryFactory.CreateIncrementalRetryProvider(null));
            Assert.Throws<ArgumentNullException>(() => SqlConfigurableRetryFactory.CreateExponentialRetryProvider(null));
        }

        [Fact]
        public void ValidateRetryParameters()
        {
            var option = new SqlRetryLogicOption()
            {
                NumberOfTries = 10, // 1-60
                MinTimeInterval = TimeSpan.FromMinutes(0), // 0-120
                MaxTimeInterval = TimeSpan.FromSeconds(120), // 0-120
                DeltaTime = TimeSpan.FromSeconds(1) // 0-120
            };

            option.NumberOfTries = 0;
            Assert.Throws<ArgumentOutOfRangeException>(() => SqlConfigurableRetryFactory.CreateFixedRetryProvider(option));
            option.NumberOfTries = 61;
            Assert.Throws<ArgumentOutOfRangeException>(() => SqlConfigurableRetryFactory.CreateFixedRetryProvider(option));
            option.NumberOfTries = 10;

            option.DeltaTime = TimeSpan.FromSeconds(-1);
            Assert.Throws<ArgumentOutOfRangeException>(() => SqlConfigurableRetryFactory.CreateFixedRetryProvider(option));
            option.DeltaTime = TimeSpan.FromSeconds(121);
            Assert.Throws<ArgumentOutOfRangeException>(() => SqlConfigurableRetryFactory.CreateFixedRetryProvider(option));
            option.DeltaTime = TimeSpan.FromSeconds(1);

            option.MinTimeInterval = TimeSpan.FromSeconds(-1);
            Assert.Throws<ArgumentOutOfRangeException>(() => SqlConfigurableRetryFactory.CreateIncrementalRetryProvider(option));
            option.MinTimeInterval = TimeSpan.FromSeconds(121);
            Assert.Throws<ArgumentOutOfRangeException>(() => SqlConfigurableRetryFactory.CreateIncrementalRetryProvider(option));
            option.MinTimeInterval = TimeSpan.FromSeconds(0);

            option.MaxTimeInterval = TimeSpan.FromSeconds(-1);
            Assert.Throws<ArgumentOutOfRangeException>(() => SqlConfigurableRetryFactory.CreateIncrementalRetryProvider(option));
            option.MaxTimeInterval = TimeSpan.FromSeconds(121);
            Assert.Throws<ArgumentOutOfRangeException>(() => SqlConfigurableRetryFactory.CreateIncrementalRetryProvider(option));

            option.MinTimeInterval = TimeSpan.FromSeconds(50);
            option.MaxTimeInterval = TimeSpan.FromSeconds(40);
            Assert.Throws<ArgumentOutOfRangeException>(() => SqlConfigurableRetryFactory.CreateIncrementalRetryProvider(option));

            option.MinTimeInterval = TimeSpan.FromSeconds(0);
            option.MaxTimeInterval = TimeSpan.FromSeconds(120);

            option.AuthorizedSqlCondition = null;
            SqlConfigurableRetryFactory.CreateIncrementalRetryProvider(option);
        }

#if NET
        /// <summary>
        /// Regression test: triggering the configurable retry logic loader through its normal
        /// entry points must not leave a process-wide
        /// <see cref="System.Runtime.Loader.AssemblyLoadContext.Default"/> resolving handler
        /// installed. Such a handler participates in resolution of every assembly the host
        /// application fails to find, and serves them out of this component's probing directory,
        /// which can load code from an unintended location.
        /// </summary>
        [Fact]
        public void RetryLogicProviderDoesNotLeaveAssemblyProbingEnabled()
        {
            // Touch the default retry logic providers to force SqlConfigurableRetryLogicLoader
            // construction via its normal code path.
            Assert.NotNull(new SqlCommand().RetryLogicProvider);
            Assert.NotNull(new SqlConnection().RetryLogicProvider);

            // A file that is not a valid assembly, planted in the loader's probing directory
            // under a name no other component could be asking for. If a handler is still
            // subscribed it finds this file and fails with BadImageFormatException. With correct
            // behavior the runtime never looks here and reports the assembly as simply not found.
            string assemblySimpleName = "MdsProbeAssembly_" + Guid.NewGuid().ToString("N");
            string plantedFile = Path.Combine(AppContext.BaseDirectory, assemblySimpleName + ".dll");

            File.WriteAllText(plantedFile, "not an assembly");
            try
            {
                Assert.Throws<FileNotFoundException>(
                    () => Assembly.Load(new AssemblyName(assemblySimpleName)));
            }
            finally
            {
                try
                {
                    File.Delete(plantedFile);
                }
                catch (IOException)
                {
                    // Best effort cleanup.
                }
            }
        }
#endif
    }
}
