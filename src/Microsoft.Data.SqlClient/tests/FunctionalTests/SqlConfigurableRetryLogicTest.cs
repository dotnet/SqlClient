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
        /// Regression test: triggering the configurable retry logic loader must not leave a
        /// process-wide <see cref="System.Runtime.Loader.AssemblyLoadContext.Default"/> resolving
        /// handler installed that probes the current working directory. The working directory is
        /// ambient process state unrelated to where the application's binaries live, so resolving
        /// assemblies from it can load code from an unintended location.
        /// </summary>
        [Fact]
        public void RetryLogicProviderDoesNotEnableCurrentDirectoryAssemblyProbing()
        {
            // Touch the default retry logic providers to force SqlConfigurableRetryLogicLoader
            // construction via its normal code path.
            Assert.NotNull(new SqlCommand().RetryLogicProvider);
            Assert.NotNull(new SqlConnection().RetryLogicProvider);

            string probeDirectory = Path.Combine(Path.GetTempPath(), "mds-crl-plant-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(probeDirectory);
            string originalCurrentDirectory = Environment.CurrentDirectory;

            try
            {
                // A file that is not a valid assembly. If the loader probes the current working
                // directory it will try to load this file and fail with BadImageFormatException.
                // With correct behavior the runtime never looks here and reports the assembly as
                // simply not found.
                string assemblySimpleName = "MdsProbeAssembly_" + Guid.NewGuid().ToString("N");
                File.WriteAllText(Path.Combine(probeDirectory, assemblySimpleName + ".dll"), "not an assembly");

                Environment.CurrentDirectory = probeDirectory;

                Assert.Throws<FileNotFoundException>(() => Assembly.Load(new AssemblyName(assemblySimpleName)));
            }
            finally
            {
                Environment.CurrentDirectory = originalCurrentDirectory;
                try
                {
                    Directory.Delete(probeDirectory, true);
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
