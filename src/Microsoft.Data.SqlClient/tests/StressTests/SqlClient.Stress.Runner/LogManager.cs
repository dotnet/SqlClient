// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;

namespace DPStressHarness
{
    public class LogManager: IDisposable
    {
        private static readonly LogManager s_instance = new LogManager();
        private readonly ConcurrentDictionary<string, TextWriter> _logs = new ConcurrentDictionary<string, TextWriter>();
        private DirectoryInfo _directoryInfo;

        private LogManager()
        {
            try
            {
                _directoryInfo = Directory.CreateDirectory("../../../logs");
            }
            catch (Exception e)
            {
                Console.WriteLine($"The process failed: {e}");
            }
        }

        public static LogManager Instance => s_instance;

        public void Dispose()
        {
            _logs.ToList().ForEach(l => l.Value.Close());
        }

        public TextWriter GetLog(string name)
        {
            if (!_logs.TryGetValue(name, out TextWriter log))
            {
                Console.WriteLine($"{_directoryInfo.FullName}/{name}.log log file created!");
                log = new StreamWriter($"{_directoryInfo.FullName}/{name}.log", false, Encoding.UTF8) { AutoFlush = true } ;
                _logs.TryAdd(name, log);
            }
            return log;
        }
    }
}
