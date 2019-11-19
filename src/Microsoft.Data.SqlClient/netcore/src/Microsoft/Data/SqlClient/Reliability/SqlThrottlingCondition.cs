// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.Data.SqlClient.Reliability.Data
{
    /// <summary>
    /// Defines the possible throttling modes in SQL Database.
    /// </summary>
    public enum ThrottlingMode
    {
        /// <summary>
        /// Corresponds to the "No Throttling" throttling mode, in which all SQL statements can be processed.
        /// </summary>
        NoThrottling = 0,

        /// <summary>
        /// Corresponds to the "Reject Update / Insert" throttling mode, in which SQL statements such as INSERT, UPDATE, CREATE TABLE, and CREATE INDEX are rejected.
        /// </summary>
        RejectUpdateInsert = 1,

        /// <summary>
        /// Corresponds to the "Reject All Writes" throttling mode, in which SQL statements such as INSERT, UPDATE, DELETE, CREATE, and DROP are rejected.
        /// </summary>
        RejectAllWrites = 2,

        /// <summary>
        /// Corresponds to the "Reject All" throttling mode, in which all SQL statements are rejected.
        /// </summary>
        RejectAll = 3,

        /// <summary>
        /// Corresponds to an unknown throttling mode whereby throttling mode cannot be determined with certainty.
        /// </summary>
        Unknown = -1
    }

    /// <summary>
    /// Defines the possible throttling types in SQL Database.
    /// </summary>
    public enum ThrottlingType
    {
        /// <summary>
        /// Indicates that no throttling was applied to a given resource.
        /// </summary>
        None = 0,

        /// <summary>
        /// Corresponds to a soft throttling type. Soft throttling is applied when machine resources such as, CPU, I/O, storage, and worker threads exceed 
        /// predefined safety thresholds despite the load balancer’s best efforts. 
        /// </summary>
        Soft = 1,

        /// <summary>
        /// Corresponds to a hard throttling type. Hard throttling is applied when the machine is out of resources, for example storage space.
        /// With hard throttling, no new connections are allowed to the databases hosted on the machine until resources are freed up.
        /// </summary>
        Hard = 2,

        /// <summary>
        /// Corresponds to an unknown throttling type in the event that the throttling type cannot be determined with certainty.
        /// </summary>
        Unknown = 3
    }

    /// <summary>
    /// Defines the types of resources in SQL Database that may be subject to throttling conditions.
    /// </summary>
    public enum ThrottledResourceType
    {
        /// <summary>
        /// Corresponds to the "Physical Database Space" resource, which may be subject to throttling.
        /// </summary>
        PhysicalDatabaseSpace = 0,

        /// <summary>
        /// Corresponds to the "Physical Log File Space" resource, which may be subject to throttling.
        /// </summary>
        PhysicalLogSpace = 1,

        /// <summary>
        /// Corresponds to the "Transaction Log Write IO Delay" resource, which may be subject to throttling.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "Io", Justification = "As designed")]
        LogWriteIoDelay = 2,

        /// <summary>
        /// Corresponds to the "Database Read IO Delay" resource, which may be subject to throttling.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "Io", Justification = "As designed")]
        DataReadIoDelay = 3,

        /// <summary>
        /// Corresponds to the "CPU" resource, which may be subject to throttling.
        /// </summary>
        Cpu = 4,

        /// <summary>
        /// Corresponds to the "Database Size" resource, which may be subject to throttling.
        /// </summary>
        DatabaseSize = 5,

        /// <summary>
        /// Corresponds to the "SQL Worker Thread Pool" resource, which may be subject to throttling.
        /// </summary>
        WorkerThreads = 7,

        /// <summary>
        /// Corresponds to an internal resource that may be subject to throttling.
        /// </summary>
        Internal = 6,

        /// <summary>
        /// Corresponds to an unknown resource type in the event that the actual resource cannot be determined with certainty.
        /// </summary>
        Unknown = -1
    }

    /// <summary>
    /// Implements an object that holds the decoded reason code returned from SQL Database when throttling conditions are encountered.
    /// </summary>
    [Serializable]
    public class SqlThrottlingCondition
    {
        /// <summary>
        /// Gets the error number that corresponds to the throttling conditions reported by SQL Database.
        /// </summary>
        public const int ThrottlingErrorNumber = 40501;

        /// <summary>
        /// Maintains a collection of key/value pairs where a key is the resource type and a value is the type of throttling applied to the given resource type.
        /// </summary>
        private readonly IList<Tuple<ThrottledResourceType, ThrottlingType>> throttledResources = new List<Tuple<ThrottledResourceType, ThrottlingType>>(9);

        /// <summary>
        /// Provides a compiled regular expression used to extract the reason code from the error message.
        /// </summary>
        private static readonly Regex sqlErrorCodeRegEx = new Regex(@"Code:\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Gets an unknown throttling condition in the event that the actual throttling condition cannot be determined.
        /// </summary>
        public static SqlThrottlingCondition Unknown
        {
            get
            {
                var unknownCondition = new SqlThrottlingCondition { ThrottlingMode = ThrottlingMode.Unknown };
                unknownCondition.throttledResources.Add(Tuple.Create(ThrottledResourceType.Unknown, ThrottlingType.Unknown));

                return unknownCondition;
            }
        }

        /// <summary>
        /// Gets the value that reflects the throttling mode in SQL Database.
        /// </summary>
        public ThrottlingMode ThrottlingMode { get; private set; }

        /// <summary>
        /// Gets a list of the resources in the SQL Database that were subject to throttling conditions.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures", Justification = "As designed")]
        public IEnumerable<Tuple<ThrottledResourceType, ThrottlingType>> ThrottledResources
        {
            get { return this.throttledResources; }
        }

        /// <summary>
        /// Gets a value that indicates whether physical data file space throttling was reported by SQL Database.
        /// </summary>
        public bool IsThrottledOnDataSpace
        {
            get { return this.throttledResources.Where(x => x.Item1 == ThrottledResourceType.PhysicalDatabaseSpace).Any(); }
        }

        /// <summary>
        /// Gets a value that indicates whether physical log space throttling was reported by SQL Database.
        /// </summary>
        public bool IsThrottledOnLogSpace
        {
            get { return this.throttledResources.Where(x => x.Item1 == ThrottledResourceType.PhysicalLogSpace).Any(); }
        }

        /// <summary>
        /// Gets a value that indicates whether transaction activity throttling was reported by SQL Database.
        /// </summary>
        public bool IsThrottledOnLogWrite
        {
            get { return this.throttledResources.Where(x => x.Item1 == ThrottledResourceType.LogWriteIoDelay).Any(); }
        }

        /// <summary>
        /// Gets a value that indicates whether data read activity throttling was reported by SQL Database.
        /// </summary>
        public bool IsThrottledOnDataRead
        {
            get { return this.throttledResources.Where(x => x.Item1 == ThrottledResourceType.DataReadIoDelay).Any(); }
        }

        /// <summary>
        /// Gets a value that indicates whether CPU throttling was reported by SQL Database.
        /// </summary>
        public bool IsThrottledOnCpu
        {
            get { return this.throttledResources.Where(x => x.Item1 == ThrottledResourceType.Cpu).Any(); }
        }

        /// <summary>
        /// Gets a value that indicates whether database size throttling was reported by SQL Database.
        /// </summary>
        public bool IsThrottledOnDatabaseSize
        {
            get { return this.throttledResources.Where(x => x.Item1 == ThrottledResourceType.DatabaseSize).Any(); }
        }

        /// <summary>
        /// Gets a value that indicates whether concurrent requests throttling was reported by SQL Database.
        /// </summary>
        public bool IsThrottledOnWorkerThreads
        {
            get { return this.throttledResources.Where(x => x.Item1 == ThrottledResourceType.WorkerThreads).Any(); }
        }

        /// <summary>
        /// Gets a value that indicates whether throttling conditions were not determined with certainty.
        /// </summary>
        public bool IsUnknown
        {
            get { return this.ThrottlingMode == ThrottlingMode.Unknown; }
        }

        /// <summary>
        /// Determines throttling conditions from the specified SQL exception.
        /// </summary>
        /// <param name="ex">The <see cref="SqlException"/> object that contains information relevant to an error returned by SQL Server when throttling conditions were encountered.</param>
        /// <returns>An instance of the object that holds the decoded reason codes returned from SQL Database when throttling conditions were encountered.</returns>
        public static SqlThrottlingCondition FromException(SqlException ex)
        {
            if (ex != null)
            {
                foreach (SqlError error in ex.Errors)
                {
                    if (error.Number == ThrottlingErrorNumber)
                    {
                        return FromError(error);
                    }
                }
            }

            return Unknown;
        }

        /// <summary>
        /// Determines the throttling conditions from the specified SQL error.
        /// </summary>
        /// <param name="error">The <see cref="SqlError"/> object that contains information relevant to a warning or error returned by SQL Server.</param>
        /// <returns>An instance of the object that holds the decoded reason codes returned from SQL Database when throttling conditions were encountered.</returns>
        public static SqlThrottlingCondition FromError(SqlError error)
        {
            if (error != null)
            {
                var match = sqlErrorCodeRegEx.Match(error.Message);
                int reasonCode;

                if (match.Success && int.TryParse(match.Groups[1].Value, out reasonCode))
                {
                    return FromReasonCode(reasonCode);
                }
            }

            return Unknown;
        }

        /// <summary>
        /// Determines the throttling conditions from the specified reason code.
        /// </summary>
        /// <param name="reasonCode">The reason code returned by SQL Database that contains the throttling mode and the exceeded resource types.</param>
        /// <returns>An instance of the object holding the decoded reason codes returned from SQL Database when encountering throttling conditions.</returns>
        public static SqlThrottlingCondition FromReasonCode(int reasonCode)
        {
            if (reasonCode > 0)
            {
                // Decode throttling mode from the last 2 bits.
                var throttlingMode = (ThrottlingMode)(reasonCode & 3);

                var condition = new SqlThrottlingCondition { ThrottlingMode = throttlingMode };

                // Shift 8 bits to truncate throttling mode.
                var groupCode = reasonCode >> 8;

                // Determine throttling type for all well-known resources that may be subject to throttling conditions.
                condition.throttledResources.Add(Tuple.Create(ThrottledResourceType.PhysicalDatabaseSpace, (ThrottlingType)(groupCode & 3)));
                condition.throttledResources.Add(Tuple.Create(ThrottledResourceType.PhysicalLogSpace, (ThrottlingType)((groupCode = groupCode >> 2) & 3)));
                condition.throttledResources.Add(Tuple.Create(ThrottledResourceType.LogWriteIoDelay, (ThrottlingType)((groupCode = groupCode >> 2) & 3)));
                condition.throttledResources.Add(Tuple.Create(ThrottledResourceType.DataReadIoDelay, (ThrottlingType)((groupCode = groupCode >> 2) & 3)));
                condition.throttledResources.Add(Tuple.Create(ThrottledResourceType.Cpu, (ThrottlingType)((groupCode = groupCode >> 2) & 3)));
                condition.throttledResources.Add(Tuple.Create(ThrottledResourceType.DatabaseSize, (ThrottlingType)((groupCode = groupCode >> 2) & 3)));
                condition.throttledResources.Add(Tuple.Create(ThrottledResourceType.Internal, (ThrottlingType)((groupCode = groupCode >> 2) & 3)));
                condition.throttledResources.Add(Tuple.Create(ThrottledResourceType.WorkerThreads, (ThrottlingType)((groupCode = groupCode >> 2) & 3)));
                condition.throttledResources.Add(Tuple.Create(ThrottledResourceType.Internal, (ThrottlingType)((groupCode >> 2) & 3)));

                return condition;
            }

            return Unknown;
        }

        /// <summary>
        ///  Returns a textual representation of the current ThrottlingCondition object, including the information held with respect to throttled resources.
        /// </summary>
        /// <returns>A string that represents the current ThrottlingCondition object.</returns>
        public override string ToString()
        {
            var result = new StringBuilder();

            result.AppendFormat(CultureInfo.CurrentCulture, "Mode: {0} | ", this.ThrottlingMode);

            var resources =
                this.throttledResources
                    .Where(x => x.Item1 != ThrottledResourceType.Internal)
                    .Select(x => string.Format(CultureInfo.CurrentCulture, "{0}: {1}", x.Item1, x.Item2))
                    .OrderBy(x => x).ToArray();

            result.Append(string.Join(", ", resources));

            return result.ToString();
        }
    }
}
