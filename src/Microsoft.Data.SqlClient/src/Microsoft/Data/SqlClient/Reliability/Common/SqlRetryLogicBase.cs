// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryLogicBase.xml' path='docs/members[@name="SqlRetryLogicBase"]/SqlRetryLogicBase/*' />
    public abstract class SqlRetryLogicBase: ICloneable
    {
        /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryLogicBase.xml' path='docs/members[@name="SqlRetryLogicBase"]/NumberOfTries/*' />
        public int NumberOfTries { get; protected set; }

        /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryLogicBase.xml' path='docs/members[@name="SqlRetryLogicBase"]/Current/*' />
        public int Current { get; protected set; }

        /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryLogicBase.xml' path='docs/members[@name="SqlRetryLogicBase"]/RetryIntervalEnumerator/*' />
        public SqlRetryIntervalBaseEnumerator RetryIntervalEnumerator { get; protected set; }

        /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryLogicBase.xml' path='docs/members[@name="SqlRetryLogicBase"]/TransientPredicate/*' />
        public Predicate<Exception> TransientPredicate { get; protected set; }

        /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryLogicBase.xml' path='docs/members[@name="SqlRetryLogicBase"]/RetryCondition/*' />
        public virtual bool RetryCondition(object sender) => true;

        ///<include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryLogicBase.xml' path='docs/members[@name="SqlRetryLogicBase"]/TryNextInterval/*' />
        public abstract bool TryNextInterval(out TimeSpan intervalTime);

        /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryLogicBase.xml' path='docs/members[@name="SqlRetryLogicBase"]/Reset/*' />
        public abstract void Reset();

        /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryLogicBase.xml' path='docs/members[@name="SqlRetryLogicBase"]/Clone/*' />
        public virtual object Clone()
        {
            throw new NotImplementedException();
        }
    }
}
