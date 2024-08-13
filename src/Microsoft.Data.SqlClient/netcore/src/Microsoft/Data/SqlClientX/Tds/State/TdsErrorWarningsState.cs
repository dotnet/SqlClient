// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Data.SqlClient;

namespace Microsoft.Data.SqlClientX.Tds.State
{
    /// <summary>
    /// Tds Errors and Warnings state information
    /// </summary>
    internal class TdsErrorWarningsState
    {
        /// <summary>
        /// Local exceptions to cache errors
        /// </summary>
        internal SqlErrorCollection _errors;

        /// <summary>
        /// Local exceptions to cache warnings
        /// </summary>
        internal SqlErrorCollection _warnings;

        /// <summary>
        /// Local exceptions to cache errors that occurred prior to sending attention
        /// </summary>
        internal SqlErrorCollection _preAttentionErrors;

        /// <summary>
        /// Local exceptions to cache warnings that occurred prior to sending attention
        /// </summary>
        internal SqlErrorCollection _preAttentionWarnings;

        /// <summary>
        /// Whether or not parser has received an error or a warning.
        /// </summary>
        internal bool _hasErrorOrWarning;

        /// <summary>
        /// TRUE - accumulate info messages during TdsParser operations, 
        /// FALSE - fire them
        /// </summary>
        internal bool _accumulateInfoEvents;

        /// <summary>
        /// List of pending info events.
        /// </summary>
        internal List<SqlError> _pendingInfoEvents;

        /// <summary>
        /// True if there is at least one error or warning (not counting the pre-attention errors\warnings)
        /// </summary>
        public bool HasErrorOrWarning => _hasErrorOrWarning;

        /// <summary>
        /// Adds an error to the error collection
        /// </summary>
        /// <param name="error"></param>
        internal void AddError(SqlError error)
        {
            Debug.Assert(error != null, "Trying to add a null error");

            _hasErrorOrWarning = true;
            _errors ??= new SqlErrorCollection();
            _errors.Add(error);
        }

        /// <summary>
        /// Gets the number of errors currently in the error collection
        /// </summary>
        internal int ErrorCount
        {
            get
            {
                int count = 0;
                if (_errors != null)
                {
                    count = _errors.Count;
                }
                return count;
            }
        }

        /// <summary>
        /// Adds an warning to the warning collection
        /// </summary>
        /// <param name="error"></param>
        internal void AddWarning(SqlError error)
        {
            Debug.Assert(error != null, "Trying to add a null error");

            _hasErrorOrWarning = true;
            _warnings ??= new SqlErrorCollection();
            _warnings.Add(error);
        }

        /// <summary>
        /// Gets the number of warnings currently in the warning collection
        /// </summary>
        internal int WarningCount
        {
            get
            {
                int count = 0;
                if (_warnings != null)
                {
                    count = _warnings.Count;
                }
                return count;
            }
        }

        /// <summary>
        /// Gets the full list of errors and warnings (including the pre-attention ones), then wipes all error and warning lists
        /// </summary>
        /// <param name="broken">If true, the connection should be broken</param>
        /// <returns>An array containing all of the errors and warnings</returns>
        internal SqlErrorCollection GetFullErrorAndWarningCollection(out bool broken)
        {
            SqlErrorCollection allErrors = new SqlErrorCollection();
            broken = false;

            _hasErrorOrWarning = false;

            // Merge all error lists, then reset them
            AddErrorsToCollection(_errors, ref allErrors, ref broken);
            AddErrorsToCollection(_warnings, ref allErrors, ref broken);
            _errors = null;
            _warnings = null;

            // We also process the pre-attention error lists here since, if we are here and they are populated, then an error occurred while sending attention so we should show the errors now (otherwise they'd be lost)
            AddErrorsToCollection(_preAttentionErrors, ref allErrors, ref broken);
            AddErrorsToCollection(_preAttentionWarnings, ref allErrors, ref broken);
            _preAttentionErrors = null;
            _preAttentionWarnings = null;

            return allErrors;
        }

        private void AddErrorsToCollection(SqlErrorCollection inCollection, ref SqlErrorCollection collectionToAddTo, ref bool broken)
        {
            if (inCollection != null)
            {
                foreach (SqlError error in inCollection)
                {
                    collectionToAddTo.Add(error);
                    broken |= (error.Class >= TdsEnums.FATAL_ERROR_CLASS);
                }
            }
        }
    }
}
