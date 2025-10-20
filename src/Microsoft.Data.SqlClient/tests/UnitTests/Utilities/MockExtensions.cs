using System;
using Moq;

namespace Microsoft.Data.SqlClient.UnitTests.Utilities
{
    public static class MockExtensions
    {
        public static void SetupThrows<TException>(this Mock<Action> mock)
            where TException : Exception, new()
        {
            mock.Setup(action => action())
                .Throws<TException>();
        }

        public static void SetupThrows<T1, TException>(this Mock<Action<T1>> mock)
            where TException : Exception, new()
        {
            mock.Setup(action => action(It.IsAny<T1>()))
                .Throws<TException>();
        }

        public static void SetupThrows<T1, T2, TException>(this Mock<Action<T1, T2>> mock)
            where TException : Exception, new()
        {
            mock.Setup(action => action(It.IsAny<T1>(), It.IsAny<T2>()))
                .Throws<TException>();
        }

        public static void SetupThrows<T1, T2, T3, TException>(this Mock<Action<T1, T2, T3>> mock)
            where TException : Exception, new()
        {
            mock.Setup(action => action(It.IsAny<T1>(), It.IsAny<T2>(), It.IsAny<T3>()))
                .Throws<TException>();
        }

        public static void VerifyNeverCalled(this Mock<Action> mock) =>
            mock.Verify(action => action(), Times.Never);

        public static void VerifyNeverCalled<T1>(this Mock<Action<T1>> mock)
        {
            mock.Verify(
                action => action(It.IsAny<T1>()),
                Times.Never);
        }

        public static void VerifyNeverCalled<T1, T2>(this Mock<Action<T1, T2>> mock)
        {
            mock.Verify(
                action => action(It.IsAny<T1>(), It.IsAny<T2>()),
                Times.Never);
        }

        public static void VerifyNeverCalled<T1, T2, T3>(this Mock<Action<T1, T2, T3>> mock)
        {
            mock.Verify(
                action => action(It.IsAny<T1>(), It.IsAny<T2>(), It.IsAny<T3>()),
                Times.Never);
        }
    }
}
