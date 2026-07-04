// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Moq;

namespace Microsoft.Data.SqlClient.UnitTests.Utilities
{
    /// <summary>
    /// Provides extension methods for working with mocked instances of Action delegates in unit tests.
    /// </summary>
    public static class MockExtensions
    {
        /// <summary>
        /// Configures the specified mock to throw an exception of the specified type
        /// when its associated action is invoked.
        /// </summary>
        /// <typeparam name="TException">The type of exception to throw.</typeparam>
        /// <param name="mock">The mock object to configure.</param>
        public static void SetupThrows<TException>(this Mock<Action> mock)
            where TException : Exception, new()
        {
            mock.Setup(action => action())
                .Throws<TException>();
        }

        /// <summary>
        /// Configures the specified mock of an Action delegate with one parameter to throw
        /// an exception of the specified type when it is invoked with any argument.
        /// </summary>
        /// <typeparam name="T1">The type of the parameter of the Action delegate.</typeparam>
        /// <typeparam name="TException">The type of exception to throw.</typeparam>
        /// <param name="mock">The mock object of the Action delegate to configure.</param>
        public static void SetupThrows<T1, TException>(this Mock<Action<T1>> mock)
            where TException : Exception, new()
        {
            mock.Setup(action => action(It.IsAny<T1>()))
                .Throws<TException>();
        }

        /// <summary>
        /// Configures the specified mock to throw an exception of the specified type
        /// when the mocked Action with two parameters is invoked.
        /// </summary>
        /// <typeparam name="T1">The type of the first parameter of the mocked Action.</typeparam>
        /// <typeparam name="T2">The type of the second parameter of the mocked Action.</typeparam>
        /// <typeparam name="TException">The type of exception to throw.</typeparam>
        /// <param name="mock">The mock object to configure.</param>
        public static void SetupThrows<T1, T2, TException>(this Mock<Action<T1, T2>> mock)
            where TException : Exception, new()
        {
            mock.Setup(action => action(It.IsAny<T1>(), It.IsAny<T2>()))
                .Throws<TException>();
        }

        /// <summary>
        /// Configures the specified mock to throw an exception of the specified type
        /// when its associated action is invoked with three parameters of the specified types.
        /// </summary>
        /// <typeparam name="T1">The type of the first parameter of the action.</typeparam>
        /// <typeparam name="T2">The type of the second parameter of the action.</typeparam>
        /// <typeparam name="T3">The type of the third parameter of the action.</typeparam>
        /// <typeparam name="TException">The type of exception to throw.</typeparam>
        /// <param name="mock">The mock object to configure.</param>
        public static void SetupThrows<T1, T2, T3, TException>(this Mock<Action<T1, T2, T3>> mock)
            where TException : Exception, new()
        {
            mock.Setup(action => action(It.IsAny<T1>(), It.IsAny<T2>(), It.IsAny<T3>()))
                .Throws<TException>();
        }

        /// <summary>
        /// Verifies that the mocked action has never been invoked.
        /// </summary>
        /// <param name="mock">The mock object for which to verify that the action was never called.</param>
        public static void VerifyNeverCalled(this Mock<Action> mock) =>
            mock.Verify(action => action(), Times.Never);

        /// <summary>
        /// Verifies that the specified mock action was never invoked.
        /// </summary>
        /// <typeparam name="T1">The type of the parameter expected by the mocked action.</typeparam>
        /// <param name="mock">The mocked action to verify.</param>
        public static void VerifyNeverCalled<T1>(this Mock<Action<T1>> mock)
        {
            mock.Verify(
                action => action(It.IsAny<T1>()),
                Times.Never);
        }

        /// <summary>
        /// Verifies that the specified mock action was never called during the test execution.
        /// </summary>
        /// <typeparam name="T1">The type of the first parameter of the action.</typeparam>
        /// <typeparam name="T2">The type of the second parameter of the action.</typeparam>
        /// <param name="mock">The mock object representing the action to verify.</param>
        public static void VerifyNeverCalled<T1, T2>(this Mock<Action<T1, T2>> mock)
        {
            mock.Verify(
                action => action(It.IsAny<T1>(), It.IsAny<T2>()),
                Times.Never);
        }

        /// <summary>
        /// Verifies that the specified mock of an <see cref="Action"/> was never called
        /// with any arguments during the test execution.
        /// </summary>
        /// <typeparam name="T1">The type of the first parameter of the action.</typeparam>
        /// <typeparam name="T2">The type of the second parameter of the action.</typeparam>
        /// <typeparam name="T3">The type of the third parameter of the action.</typeparam>
        /// <param name="mock">The mock object to verify.</param>
        public static void VerifyNeverCalled<T1, T2, T3>(this Mock<Action<T1, T2, T3>> mock)
        {
            mock.Verify(
                action => action(It.IsAny<T1>(), It.IsAny<T2>(), It.IsAny<T3>()),
                Times.Never);
        }
    }
}
