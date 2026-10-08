// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Tests.Common.Fixtures.DatabaseObjects;

/// <summary>
/// A transient system-versioned (temporal) table, created at the start of its scope and dropped —
/// together with its history table — when disposed.
/// </summary>
/// <remarks>
/// A temporal table cannot be dropped directly. System versioning has to be switched off first,
/// which severs the link to the history table and leaves that table behind as an ordinary one that
/// must then be dropped in its own right. That history table is created by the server as a side
/// effect, so it is <see cref="Table.AdoptExisting">adopted</see> rather than created: this type
/// owns it and drops it, but never issues a CREATE for it.
/// </remarks>
public sealed class TemporalTable : DatabaseObject
{
    /// <summary>
    /// The history table backing this temporal table. Dropped along with it.
    /// </summary>
    public Table HistoryTable { get; }

    /// <summary>
    /// Initializes a new system-versioned table and adopts the history table created alongside it.
    /// </summary>
    /// <param name="connection">The SQL connection used to interact with the database.</param>
    /// <param name="prefix">The prefix for the table name.</param>
    /// <param name="historyPrefix">
    /// The prefix for the history table name. SYSTEM_VERSIONING requires a schema-qualified history
    /// table, so the generated name is qualified with <c>[dbo]</c>.
    /// </param>
    /// <param name="columns">
    /// The column definitions, including the PERIOD FOR SYSTEM_TIME clause, in parentheses. The
    /// SYSTEM_VERSIONING option naming the history table is appended automatically.
    /// </param>
    public TemporalTable(SqlConnection connection, string prefix, string historyPrefix, string columns)
        : this(connection, GenerateLongName(prefix), $"[dbo].{GenerateLongName(historyPrefix)}", columns, NameIsVerbatim.Yes)
    {
    }

    private TemporalTable(SqlConnection connection, string name, string historyName, string columns, NameIsVerbatim _)
        : base(connection, name, $"{columns} WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = {historyName}))")
    {
        // Adopted only once the CREATE above has succeeded, since that statement is what brings the
        // history table into existence. If it throws, the base constructor disposes this instance,
        // and the null check in DropObject keeps that path safe.
        HistoryTable = Table.AdoptExisting(connection, historyName);
    }

    protected override void CreateObject(string definition)
    {
        using SqlCommand createCommand = new($"CREATE TABLE {Name} {definition}", Connection);

        createCommand.ExecuteNonQuery();
    }

    protected override void DropObject()
    {
        // NOTE: The name is passed to OBJECT_ID() as a parameter rather than being interpolated
        //   into a string literal, because it embeds Environment.UserName/MachineName (see
        //   DatabaseObject.GenerateLongName) and an apostrophe in either would break the batch.
        //   The identifier in ALTER/DROP TABLE is already bracket-quoted by GenerateLongName.
        //
        //   Ordering is load-bearing: SYSTEM_VERSIONING must be switched off before the period can
        //   be dropped, and the period before the table itself.
        using (SqlCommand dropCommand = new($"""
            IF (OBJECT_ID(@name) IS NOT NULL)
            BEGIN
                ALTER TABLE {Name} SET (SYSTEM_VERSIONING = OFF);
                ALTER TABLE {Name} DROP PERIOD FOR SYSTEM_TIME;
                DROP TABLE {Name};
            END
            """, Connection))
        {
            dropCommand.Parameters.AddWithValue("@name", Name);

            dropCommand.ExecuteNonQuery();
        }

        // Only reachable once versioning is off, which is what turns the history table back into an
        // ordinary droppable one. Null both during the pre-emptive drop the base constructor runs
        // before CREATE, and while it unwinds a failed CREATE — in neither case is there an adopted
        // history table to drop.
        HistoryTable?.Dispose();
    }
}
