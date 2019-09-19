using System;
using System.Data;
// <Snippet1>
using Microsoft.Data.SqlClient;
using System.Xml;
using System.Data.Common;
using System.Windows.Forms;

public class Form1 : Form
{
    protected DataSet categoriesDataSet;
    protected DataGrid dataGrid1;
    protected SqlDataAdapter categoriesAdapter;


    public void AddSqlParameters()
    {
        // ...
        // create categoriesDataSet and categoriesAdapter
        // ...

        categoriesAdapter.SelectCommand.Parameters.Add(
          "@CategoryName", SqlDbType.VarChar, 80).Value = "toasters";
        categoriesAdapter.SelectCommand.Parameters.Add(
          "@SerialNum", SqlDbType.Int).Value = 239;
        categoriesAdapter.Fill(categoriesDataSet);

    }
}
// </Snippet1>