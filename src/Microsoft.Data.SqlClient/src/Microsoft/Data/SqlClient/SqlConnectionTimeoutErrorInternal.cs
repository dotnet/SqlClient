// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Text;

namespace Microsoft.Data.SqlClient
{
    // VSTFDevDiv# 643319 - Improve timeout error message reported when SqlConnection.Open fails
    internal enum SqlConnectionTimeoutErrorPhase
    {
        Undefined = 0,
        PreLoginBegin,              // [PRE-LOGIN PHASE]        Start of the pre-login phase; Initialize global variables;
        InitializeConnection,       // [PRE-LOGIN PHASE]        Create and initialize socket.
        SendPreLoginHandshake,      // [PRE-LOGIN PHASE]        Make pre-login handshake request.
        ConsumePreLoginHandshake,   // [PRE-LOGIN PHASE]        Receive pre-login handshake response and consume it; Establish an SSL channel.
        LoginBegin,                 // [LOGIN PHASE]            End of the pre-login phase; Start of the login phase; 
        ProcessConnectionAuth,      // [LOGIN PHASE]            Process SSPI or SQL Authenticate.
        PostLogin,                  // [POST-LOGIN PHASE]       End of the login phase; And post-login phase;
        Complete,                   // Marker for the successful completion of the connection
        Count                       // ** This is to track the length of the enum. ** Do not add any phase after this. **
    }

    internal enum SqlConnectionInternalSourceType
    {
        Principle,
        Failover,
        RoutingDestination
    }

    // DEVNOTE: Class to capture the duration spent in each SqlConnectionTimeoutErrorPhase.
    internal class SqlConnectionTimeoutPhaseDuration
    {
        private Stopwatch _swDuration = new Stopwatch();

        internal void StartCapture()
        {
            Debug.Assert(_swDuration != null, "Time capture stopwatch cannot be null.");
            _swDuration.Start();
        }

        internal void StopCapture()
        {
            //Debug.Assert(swDuration.IsRunning == true, "The stop operation of the stopwatch cannot be called when it is not running.");
            if (_swDuration.IsRunning == true)
            {
                _swDuration.Stop();
            }
        }

        internal long GetMilliSecondDuration()
        {
            // DEVNOTE: In a phase fails in between a phase, the stop watch may still be running.
            // Hence the check to verify if the stop watch is running hasn't been added in.
            return _swDuration.ElapsedMilliseconds;
        }
    }

    internal class SqlConnectionTimeoutErrorInternal
    {
        private SqlConnectionTimeoutPhaseDuration[] _phaseDurations;
        private SqlConnectionTimeoutPhaseDuration[] _originalPhaseDurations;
        private SqlConnectionTimeoutErrorPhase _currentPhase;
        private SqlConnectionInternalSourceType _currentSourceType;
        private bool _isFailoverScenario;

        // TEMPORARY CI diagnostics context (see LoginTimeoutDiagnostics). Populated when the
        // owning SqlConnectionInternal sets up the login timer so the timeout message emission
        // can correlate the configured ConnectTimeout with the actual timer budget.
        private int _diagConnectTimeoutSeconds = -1;
        private long _diagLoginBudgetMs = -1;
        private bool _diagLoginTimerInfinite;
        private bool _diagOverallPoolWaitSwitch;
        private string _diagDataSource;

        internal SqlConnectionTimeoutErrorPhase CurrentPhase => _currentPhase;

        internal void SetDiagnosticContext(
            string dataSource,
            int connectTimeoutSeconds,
            long loginBudgetMs,
            bool loginTimerInfinite,
            bool overallPoolWaitSwitch)
        {
            _diagDataSource = dataSource;
            _diagConnectTimeoutSeconds = connectTimeoutSeconds;
            _diagLoginBudgetMs = loginBudgetMs;
            _diagLoginTimerInfinite = loginTimerInfinite;
            _diagOverallPoolWaitSwitch = overallPoolWaitSwitch;
        }

        public SqlConnectionTimeoutErrorInternal()
        {
            _phaseDurations = new SqlConnectionTimeoutPhaseDuration[(int)SqlConnectionTimeoutErrorPhase.Count];
            for (int i = 0; i < _phaseDurations.Length; i++)
            {
                _phaseDurations[i] = null;
            }
        }

        public void SetFailoverScenario(bool useFailoverServer)
        {
            _isFailoverScenario = useFailoverServer;
        }

        public void SetInternalSourceType(SqlConnectionInternalSourceType sourceType)
        {
            _currentSourceType = sourceType;

            if (_currentSourceType == SqlConnectionInternalSourceType.RoutingDestination)
            {
                // When we get routed, save the current phase durations so that we can use them in the error message later
                Debug.Assert(_currentPhase == SqlConnectionTimeoutErrorPhase.PostLogin, "Should not be switching to the routing destination until Post Login is completed");
                _originalPhaseDurations = _phaseDurations;
                _phaseDurations = new SqlConnectionTimeoutPhaseDuration[(int)SqlConnectionTimeoutErrorPhase.Count];
                SetAndBeginPhase(SqlConnectionTimeoutErrorPhase.PreLoginBegin);
            }
        }

        internal void ResetAndRestartPhase()
        {
            _currentPhase = SqlConnectionTimeoutErrorPhase.PreLoginBegin;
            for (int i = 0; i < _phaseDurations.Length; i++)
            {
                _phaseDurations[i] = null;
            }
        }

        internal void SetAndBeginPhase(SqlConnectionTimeoutErrorPhase timeoutErrorPhase)
        {
            _currentPhase = timeoutErrorPhase;
            if (_phaseDurations[(int)timeoutErrorPhase] == null)
            {
                _phaseDurations[(int)timeoutErrorPhase] = new SqlConnectionTimeoutPhaseDuration();
            }
            _phaseDurations[(int)timeoutErrorPhase].StartCapture();
        }

        internal void EndPhase(SqlConnectionTimeoutErrorPhase timeoutErrorPhase)
        {
            Debug.Assert(_phaseDurations[(int)timeoutErrorPhase] != null, "End phase capture cannot be invoked when the phase duration object is a null.");
            _phaseDurations[(int)timeoutErrorPhase].StopCapture();
        }

        internal void SetAllCompleteMarker()
        {
            _currentPhase = SqlConnectionTimeoutErrorPhase.Complete;
        }

        internal string GetErrorMessage()
        {
            StringBuilder errorBuilder;
            string durationString;
            switch (_currentPhase)
            {
                case SqlConnectionTimeoutErrorPhase.PreLoginBegin:
                    errorBuilder = new StringBuilder(SQLMessage.Timeout_PreLogin_Begin());
                    durationString = SQLMessage.Duration_PreLogin_Begin(
                        _phaseDurations[(int)SqlConnectionTimeoutErrorPhase.PreLoginBegin].GetMilliSecondDuration()
                    );
                    break;
                case SqlConnectionTimeoutErrorPhase.InitializeConnection:
                    errorBuilder = new StringBuilder(SQLMessage.Timeout_PreLogin_InitializeConnection());
                    durationString = SQLMessage.Duration_PreLogin_Begin(
                        _phaseDurations[(int)SqlConnectionTimeoutErrorPhase.PreLoginBegin].GetMilliSecondDuration() +
                        _phaseDurations[(int)SqlConnectionTimeoutErrorPhase.InitializeConnection].GetMilliSecondDuration()
                    );
                    break;
                case SqlConnectionTimeoutErrorPhase.SendPreLoginHandshake:
                    errorBuilder = new StringBuilder(SQLMessage.Timeout_PreLogin_SendHandshake());
                    durationString = SQLMessage.Duration_PreLoginHandshake(
                        _phaseDurations[(int)SqlConnectionTimeoutErrorPhase.PreLoginBegin].GetMilliSecondDuration() +
                        _phaseDurations[(int)SqlConnectionTimeoutErrorPhase.InitializeConnection].GetMilliSecondDuration(),
                        _phaseDurations[(int)SqlConnectionTimeoutErrorPhase.SendPreLoginHandshake].GetMilliSecondDuration()
                    );
                    break;
                case SqlConnectionTimeoutErrorPhase.ConsumePreLoginHandshake:
                    errorBuilder = new StringBuilder(SQLMessage.Timeout_PreLogin_ConsumeHandshake());
                    durationString = SQLMessage.Duration_PreLoginHandshake(
                        _phaseDurations[(int)SqlConnectionTimeoutErrorPhase.PreLoginBegin].GetMilliSecondDuration() +
                        _phaseDurations[(int)SqlConnectionTimeoutErrorPhase.InitializeConnection].GetMilliSecondDuration(),
                        _phaseDurations[(int)SqlConnectionTimeoutErrorPhase.SendPreLoginHandshake].GetMilliSecondDuration() +
                        _phaseDurations[(int)SqlConnectionTimeoutErrorPhase.ConsumePreLoginHandshake].GetMilliSecondDuration()
                    );
                    break;
                case SqlConnectionTimeoutErrorPhase.LoginBegin:
                    errorBuilder = new StringBuilder(SQLMessage.Timeout_Login_Begin());
                    durationString = SQLMessage.Duration_Login_Begin(
                        _phaseDurations[(int)SqlConnectionTimeoutErrorPhase.PreLoginBegin].GetMilliSecondDuration() +
                        _phaseDurations[(int)SqlConnectionTimeoutErrorPhase.InitializeConnection].GetMilliSecondDuration(),
                        _phaseDurations[(int)SqlConnectionTimeoutErrorPhase.SendPreLoginHandshake].GetMilliSecondDuration() +
                        _phaseDurations[(int)SqlConnectionTimeoutErrorPhase.ConsumePreLoginHandshake].GetMilliSecondDuration(),
                        _phaseDurations[(int)SqlConnectionTimeoutErrorPhase.LoginBegin].GetMilliSecondDuration()
                    );
                    break;
                case SqlConnectionTimeoutErrorPhase.ProcessConnectionAuth:
                    errorBuilder = new StringBuilder(SQLMessage.Timeout_Login_ProcessConnectionAuth());
                    durationString = SQLMessage.Duration_Login_ProcessConnectionAuth(
                        _phaseDurations[(int)SqlConnectionTimeoutErrorPhase.PreLoginBegin].GetMilliSecondDuration() +
                        _phaseDurations[(int)SqlConnectionTimeoutErrorPhase.InitializeConnection].GetMilliSecondDuration(),
                        _phaseDurations[(int)SqlConnectionTimeoutErrorPhase.SendPreLoginHandshake].GetMilliSecondDuration() +
                        _phaseDurations[(int)SqlConnectionTimeoutErrorPhase.ConsumePreLoginHandshake].GetMilliSecondDuration(),
                        _phaseDurations[(int)SqlConnectionTimeoutErrorPhase.LoginBegin].GetMilliSecondDuration(),
                        _phaseDurations[(int)SqlConnectionTimeoutErrorPhase.ProcessConnectionAuth].GetMilliSecondDuration()
                    );
                    break;
                case SqlConnectionTimeoutErrorPhase.PostLogin:
                    errorBuilder = new StringBuilder(SQLMessage.Timeout_PostLogin());
                    durationString = SQLMessage.Duration_PostLogin(
                        _phaseDurations[(int)SqlConnectionTimeoutErrorPhase.PreLoginBegin].GetMilliSecondDuration() +
                        _phaseDurations[(int)SqlConnectionTimeoutErrorPhase.InitializeConnection].GetMilliSecondDuration(),
                        _phaseDurations[(int)SqlConnectionTimeoutErrorPhase.SendPreLoginHandshake].GetMilliSecondDuration() +
                        _phaseDurations[(int)SqlConnectionTimeoutErrorPhase.ConsumePreLoginHandshake].GetMilliSecondDuration(),
                        _phaseDurations[(int)SqlConnectionTimeoutErrorPhase.LoginBegin].GetMilliSecondDuration(),
                        _phaseDurations[(int)SqlConnectionTimeoutErrorPhase.ProcessConnectionAuth].GetMilliSecondDuration(),
                        _phaseDurations[(int)SqlConnectionTimeoutErrorPhase.PostLogin].GetMilliSecondDuration()
                    );
                    break;
                default:
                    errorBuilder = new StringBuilder(SQLMessage.Timeout());
                    durationString = null;
                    break;
            }

            // This message is to be added only when within the various stages of a connection. 
            // In all other cases, it will default to the original error message.
            if ((_currentPhase != SqlConnectionTimeoutErrorPhase.Undefined) && (_currentPhase != SqlConnectionTimeoutErrorPhase.Complete))
            {
                // NOTE: In case of a failover scenario, add a string that this failure occurred as part of the primary or secondary server
                if (_isFailoverScenario)
                {
                    errorBuilder.Append("  ");
                    errorBuilder.AppendFormat((IFormatProvider)null, SQLMessage.Timeout_FailoverInfo(), _currentSourceType);
                }
                else if (_currentSourceType == SqlConnectionInternalSourceType.RoutingDestination)
                {
                    errorBuilder.Append("  ");
                    errorBuilder.AppendFormat((IFormatProvider)null, SQLMessage.Timeout_RoutingDestination(),
                        _originalPhaseDurations[(int)SqlConnectionTimeoutErrorPhase.PreLoginBegin].GetMilliSecondDuration() +
                        _originalPhaseDurations[(int)SqlConnectionTimeoutErrorPhase.InitializeConnection].GetMilliSecondDuration(),
                        _originalPhaseDurations[(int)SqlConnectionTimeoutErrorPhase.SendPreLoginHandshake].GetMilliSecondDuration() +
                        _originalPhaseDurations[(int)SqlConnectionTimeoutErrorPhase.ConsumePreLoginHandshake].GetMilliSecondDuration(),
                        _originalPhaseDurations[(int)SqlConnectionTimeoutErrorPhase.LoginBegin].GetMilliSecondDuration(),
                        _originalPhaseDurations[(int)SqlConnectionTimeoutErrorPhase.ProcessConnectionAuth].GetMilliSecondDuration(),
                        _originalPhaseDurations[(int)SqlConnectionTimeoutErrorPhase.PostLogin].GetMilliSecondDuration()
                    );
                }
            }

            // NOTE: To display duration in each phase.
            if (durationString != null)
            {
                errorBuilder.Append("  ");
                errorBuilder.Append(durationString);
            }

            if (LoginTimeoutDiagnostics.Enabled)
            {
                long postLoginMs = _phaseDurations[(int)SqlConnectionTimeoutErrorPhase.PostLogin]?.GetMilliSecondDuration() ?? -1;
                long preLoginMs = _phaseDurations[(int)SqlConnectionTimeoutErrorPhase.PreLoginBegin]?.GetMilliSecondDuration() ?? -1;
                string diag = string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "TimeoutMessageBuilt: phase={0}; failover={1}; sourceType={2}; " +
                    "DataSource='{3}'; ConnectTimeoutConfig={4}s; LoginBudgetMs={5}; " +
                    "LoginTimerInfinite={6}; OverallPoolWaitSwitch={7}; " +
                    "PreLoginPhaseMs={8}; PostLoginPhaseMs={9}",
                    _currentPhase,
                    _isFailoverScenario,
                    _currentSourceType,
                    _diagDataSource,
                    _diagConnectTimeoutSeconds,
                    _diagLoginBudgetMs,
                    _diagLoginTimerInfinite,
                    _diagOverallPoolWaitSwitch,
                    preLoginMs,
                    postLoginMs);

                LoginTimeoutDiagnostics.Log(diag);

                // Also append a compact, greppable suffix directly to the timeout
                // exception message so it is captured in the test failure output
                // (xunit surfaces exception messages even when it does not surface
                // driver stderr). Env-gated, append-only, timeout-path-only.
                errorBuilder.Append("  [MDS-TIMEOUT-DIAG ").Append(diag).Append("]");
            }

            return errorBuilder.ToString();
        }
    }
}
