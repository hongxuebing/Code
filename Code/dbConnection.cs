using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;

namespace Code.Core
{
  public class DbConnection
  {
    private SqlDataAdapter myAdapter;
    private SqlConnection conn;

    /// <constructor>
    /// Initialise Connection
    /// </constructor>
    public DbConnection()
    {
      myAdapter = new SqlDataAdapter();
      conn = new SqlConnection(ConfigurationManager.ConnectionStrings["dbConnectionString"].ConnectionString);
    }

    /// <method>
    /// Open Database Connection if Closed or Broken
    /// </method>
    private SqlConnection OpenConnection()
    {
      if (conn.State == ConnectionState.Closed || conn.State ==
      ConnectionState.Broken)
      {
        conn.Open();
      }
      return conn;
    }

    /// <method>
    /// Select Query
    /// </method>
    public DataTable ExecuteSelectQuery(String _query, SqlParameter[] sqlParameter)
    {
      SqlCommand myCommand = new SqlCommand();
      DataTable dataTable = new DataTable();
      dataTable = null;
      DataSet ds = new DataSet();
      try
      {
        myCommand.Connection = OpenConnection();
        myCommand.CommandText = _query;
        myCommand.Parameters.AddRange(sqlParameter);
        myCommand.ExecuteNonQuery();
        myAdapter.SelectCommand = myCommand;
        myAdapter.Fill(ds);
        dataTable = ds.Tables[0];
      }
      catch (SqlException e)
      {
        Console.Write("Error - Connection.executeSelectQuery - Query: " + _query + " \nException: " + e.StackTrace.ToString());
        return null;
      }
      finally
      {

      }
      return dataTable;
    }

    /// <method>
    /// Insert Query
    /// </method>
    public bool ExecuteInsertQuery(String _query, SqlParameter[] sqlParameter)
    {
      SqlCommand myCommand = new SqlCommand();
      try
      {
        myCommand.Connection = OpenConnection();
        myCommand.CommandText = _query;
        myCommand.Parameters.AddRange(sqlParameter);
        myAdapter.InsertCommand = myCommand;
        myCommand.ExecuteNonQuery();
      }
      catch (SqlException e)
      {
        Console.Write("Error - Connection.executeInsertQuery - Query: " + _query + " \nException: \n" + e.StackTrace.ToString());
        return false;
      }
      finally
      {
      }
      return true;
    }

    /// <method>
    /// Update Query
    /// </method>
    public bool ExecuteUpdateQuery(String _query, SqlParameter[] sqlParameter)
    {
      SqlCommand myCommand = new SqlCommand();
      try
      {
        myCommand.Connection = OpenConnection();
        myCommand.CommandText = _query;
        myCommand.Parameters.AddRange(sqlParameter);
        myAdapter.UpdateCommand = myCommand;
        myCommand.ExecuteNonQuery();
      }
      catch (SqlException e)
      {
        Console.Write("Error - Connection.executeUpdateQuery - Query: " + _query + " \nException: " + e.StackTrace.ToString());
        return false;
      }
      finally
      {
      }
      return true;
    }
  }
}