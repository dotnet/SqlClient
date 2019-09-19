// <Snippet1>
using System;
using System.Xml;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Data.Common;
using System.Windows.Forms;

public class Form1 : Form
{
    protected DataSet DataSet1;
    protected DataGrid dataGrid1;


    public void CreateSqlCommand()
    {
        SqlCommand command = new SqlCommand();
        command.CommandTimeout = 15;
        command.CommandType = CommandType.Text;
    }
    // </Snippet1>
}
