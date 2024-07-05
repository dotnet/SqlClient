// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClientX.IO
{
    /// <summary>
    /// Provides buffer management for reader/writer implementations
    /// </summary>
    internal class TdsBufferManager
    {
        private readonly ArrayPool<byte> _bufferPool = ArrayPool<byte>.Shared;

        private byte[] RentBuffer(int size) => _bufferPool.Rent(size);

        private void ReturnBuffer(byte[] buffer) => _bufferPool.Return(buffer, clearArray: true);

        #region Public APIs

        /// <summary>
        /// Operation that consumes rented buffer and executes synchronously, returns result for pass-through.
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="buffer"></param>
        /// <returns></returns>
        public delegate TResult Operation<TResult>(byte[] buffer);

        /// <summary>
        /// Operation that consumes rented buffer and executes asynchronously.
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns></returns>
        public delegate ValueTask OperationAsync(byte[] buffer);

        /// <summary>
        /// Operation that consumes rented buffer and executes asynchronously.
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns></returns>
        public delegate ValueTask<TResult> OperationAsync<TResult>(byte[] buffer);

        /// <summary>
        /// Performs <paramref name="operation"/> asynchronously with rented buffer.
        /// </summary>
        /// <param name="size"></param>
        /// <param name="operation"></param>
        /// <returns></returns>
        public ValueTask DoWithRentedBuffer(int size, OperationAsync operation)
        {
            byte[] buffer = RentBuffer(size);
            try
            {
                return operation(buffer);
            }
            finally
            {
                ReturnBuffer(buffer);
            }
        }

        /// <summary>
        /// Performs <paramref name="operation"/> asynchronously with rented buffer.
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="size"></param>
        /// <param name="operation"></param>
        /// <returns></returns>
        public ValueTask<TResult> DoWithRentedBuffer<TResult>(int size, OperationAsync<TResult> operation)
        {
            byte[] buffer = RentBuffer(size);
            try
            {
                return operation(buffer);
            }
            finally
            {
                ReturnBuffer(buffer);
            }
        }

        /// <summary>
        /// Performs <paramref name="operation"/> synchronously with rented buffer.
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="size"></param>
        /// <param name="operation"></param>
        /// <returns></returns>
        public TResult ReturnWithRentedBuffer<TResult>(int size, Operation<TResult> operation)
        {
            byte[] buffer = RentBuffer(size);
            try
            {
                return operation(buffer);
            }
            finally
            {
                ReturnBuffer(buffer);
            }
        }
        #endregion
    }
}
