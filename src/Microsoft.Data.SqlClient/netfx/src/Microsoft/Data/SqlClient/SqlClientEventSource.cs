using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics.Tracing;

namespace SqlClient.Microsoft.Data.SqlClient
{
    /// <summary>
    /// 
    /// </summary>
    [EventSource(Name = "Microsoft.Data.SqlClient.Logger")]
    public class SqlClientEventSource : EventSource
    {
        /// <summary>
        /// 
        /// </summary>
        class Tasks
        {

        }
        /// <summary>
        /// 
        /// </summary>
        class Keywords
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="Message"></param>
        [Event(1, Message = "Scope Name: {0}", Opcode = EventOpcode.Start, Level = EventLevel.Informational)]
        public void ScopeEnter(string Message) { WriteEvent(1, Message); }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="Message"></param>
        [Event(2, Message = "", Level = EventLevel.Informational, Channel = EventChannel.Analytic)]
        public void Trace(string Message) { WriteEvent(2, Message); }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="Message"></param>
        [Event(3, Message = "", Level = EventLevel.Informational, Channel = EventChannel.Analytic)]
        public void CorrelationTrace(string Message) { WriteEvent(3, Message); }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="Message"></param>
        [Event(4, Message = "", Level = EventLevel.Informational, Channel = EventChannel.Analytic)]
        public void NotificationsTrace(string Message) { WriteEvent(4, Message); }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="Message"></param>
        [Event(5)]
        public void NotificationsScopeEnter(string Message) { WriteEvent(5, Message); }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="Message"></param>
        [Event(6)]
        public void PoolerScopeEnter(string Message) { WriteEvent(6, Message); }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="Message"></param>
        [Event(7)]
        public void PoolerTrace(string Message) { WriteEvent(7, Message); }

        /// <summary>
        /// 
        /// </summary>
        [Event(8, Message = "Scope Name: {0}")]
        public void ScopeLeave() { WriteEvent(8); }

        /// <summary>
        /// 
        /// </summary>
        public static SqlClientEventSource Log = new SqlClientEventSource();
    }
}
