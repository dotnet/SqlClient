// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Data.SqlClient.Connection;

namespace Microsoft.Data.SqlClient.Connection
{
     internal sealed class SessionData
    {
        // @TODO: Rename to match guidelines
        internal const int _maxNumberOfSessionStates = 256;

        #region Fields
        // @TODO: Use properties!

        #if DEBUG
        internal bool _debugReconnectDataApplied;
        #endif

        internal SqlCollation _collation;
        internal string _database;
        internal SessionStateRecord[] _delta = new SessionStateRecord[_maxNumberOfSessionStates];
        internal bool _deltaDirty = false;
        internal bool _encrypted;
        internal SqlCollation _initialCollation;
        internal string _initialLanguage;
        internal string _initialDatabase;
        internal byte[][] _initialState = new byte[_maxNumberOfSessionStates][];
        internal string _language;

        // @TODO: Introduce record/struct type to replace the tuple.
        internal Dictionary<string, Tuple<string, string>> _resolvedAliases;

        internal uint _tdsVersion;
        internal byte _unrecoverableStatesCount = 0;

        #endregion

        #region Constructors

        public SessionData()
        {
            _resolvedAliases = new Dictionary<string, Tuple<string, string>>(2);
        }

        public SessionData(SessionData recoveryData)
        {
            _initialDatabase = recoveryData._initialDatabase;
            _initialCollation = recoveryData._initialCollation;
            _initialLanguage = recoveryData._initialLanguage;
            _resolvedAliases = recoveryData._resolvedAliases;

            for (int i = 0; i < _maxNumberOfSessionStates; i++)
            {
                if (recoveryData._initialState[i] != null)
                {
                    _initialState[i] = (byte[])recoveryData._initialState[i].Clone();
                }
            }
        }

        #endregion

        #region Methods

        [Conditional("DEBUG")]
        public void AssertUnrecoverableStateCountIsCorrect()
        {
            byte unrecoverableCount = 0;
            foreach (var state in _delta)
            {
                if (state != null && !state._recoverable)
                {
                    unrecoverableCount++;
                }
            }

            Debug.Assert(unrecoverableCount == _unrecoverableStatesCount,
                "Unrecoverable count does not match");
        }

        public void Reset()
        {
            _database = null;
            _collation = null;
            _language = null;
            if (_deltaDirty)
            {
                Array.Clear(_delta, 0, _delta.Length);
                _deltaDirty = false;
            }

            _unrecoverableStatesCount = 0;
        }

        #endregion
    }
}
