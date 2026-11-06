#if NET
using System;
using System.Threading;
using Microsoft.Data.SqlClient.PerformanceTests;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests
{
    /// <summary>Verifies workload-only observation and the post-workload grace period.</summary>
    public class ThreadPoolHealthObserverTests
    {
        /// <summary>
        /// Completion stops observation rather than collecting a recovery resource timeline.
        /// </summary>
        [Fact]
        public void Complete_GracePeriod_DoesNotCollectRecoverySamples()
        {
            // Arrange
            using var offered = new ManualResetEventSlim();
            using var observer = new ThreadPoolHealthObserver(
                new AsyncHealthOptions { CadenceMilliseconds = 1, RecoveryMilliseconds = 100 },
                callback =>
                {
                    callback();
                    offered.Set();
                });
            observer.Start();
            Assert.True(offered.Wait(TimeSpan.FromSeconds(30)));

            // Act
            AsyncHealthWindow result = observer.Complete();

            // Assert
            Assert.NotEmpty(result.Resources);
            Assert.DoesNotContain(result.Resources, sample => sample.Recovery);
            Assert.Equal(0, result.Probes.Outstanding);
            Assert.True(result.RecoveryEndMilliseconds >= result.WorkloadEndMilliseconds);
            Assert.Same(result, observer.Complete());
        }
    }
}
#endif