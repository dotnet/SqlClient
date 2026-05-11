namespace SqlCommand_ExecuteXmlReader;

// <Snippet1>
using System;
using System.Data;
using Microsoft.Data.SqlClient;

public class Sample
{
    static void CreateXMLReader(string queryString, string connectionString)
    {
        using (SqlConnection connection = new SqlConnection(
                   connectionString))
        {
            connection.Open();
            SqlCommand command = new SqlCommand(queryString, connection);
            System.Xml.XmlReader reader = command.ExecuteXmlReader();
        }
    }
}
// </Snippet1>
