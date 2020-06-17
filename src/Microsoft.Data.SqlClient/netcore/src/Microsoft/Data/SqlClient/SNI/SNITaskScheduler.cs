// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient.SNI
{
    internal sealed class SNITaskScheduler : TaskScheduler
    {
        private static SNITaskScheduler s_scheduler;

        public static SNITaskScheduler Instance
        {
            get
            {
                LazyInitializer.EnsureInitialized(ref s_scheduler);
                ;
                return s_scheduler;
            }
        }

        private BlockingCollection<Task> _tasks;
        private readonly Thread _thread;

        public SNITaskScheduler()
        {
            _tasks = new BlockingCollection<Task>();
            _thread = new Thread(new ThreadStart(ExecuteThreadStart));
            _thread.IsBackground = true;
            _thread.Name = typeof(SNITaskScheduler).FullName;
            _thread.Start();
        }

        private void ExecuteThreadStart()
        {
            foreach (Task task in _tasks.GetConsumingEnumerable())
            {
                TryExecuteTask(task);
            }
        }

        protected override void QueueTask(Task task)
        {
            if (task != null)
            {
                _tasks.Add(task);
            }
        }

        protected override IEnumerable<Task> GetScheduledTasks() => _tasks.ToArray();

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) => false;
    }
}
