using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.Data.SqlClient;
using System.Linq;
//using System.Windows.Forms;

namespace KINT_Lib
{
    /// <summary>
    /// DBJob에 대한 요약 설명입니다.
    /// </summary>
    public class DBJob
    {
        private OleDbCommand sqlCommand = null;
        private OleDbConnection sqlConnection = null;
        private OleDbTransaction sqlTransaction = null;

        //        public static string sDBConnect = @"Provider=Microsoft.ACE.OLEDB.12.0;User ID=Admin;Data Source=" + Application.StartupPath + "\\DB\\PROD.mdb";

        // public static string sDBConnect = @"Provider=Microsoft.ACE.OLEDB.12.0;User ID=Admin;Data Source=" ;

        public static string sDBConnect = @"Provider=Microsoft.ACE.OLEDB.12.0;Data Source = ";

        public DBJob(string str_DB_Name)
        {
            
            //TEST
            this.sqlConnection = new System.Data.OleDb.OleDbConnection();
            this.sqlConnection.ConnectionString = sDBConnect + str_DB_Name;

            this.sqlCommand = new System.Data.OleDb.OleDbCommand();
            this.sqlCommand.Connection = this.sqlConnection;

            OpenConnection();
        }


        ~DBJob()
        {
            this.CloseConnection();
        }

        public bool OpenConnection()
        {
            try
            {
                if (this.sqlConnection.State.ToString().ToUpper() != "OPEN")
                {
                    this.sqlConnection.Open();
                }
                else
                {

                    return true;
                }
            }
            catch (Exception e)
            {
                //                throw new Exception(e.Message);
                return false;
            }

            return true;
        }

        public void CloseConnection()
        {
            try
            {
                this.sqlConnection.Close();
            }
            catch (Exception e)
            {
                //                throw new Exception(e.Message);
            }
        }

        public System.Data.OleDb.OleDbDataReader OpenDataReader(string commandText)
        {
            this.sqlCommand.CommandText = commandText;

            OleDbDataReader dataReader = null;
            try
            {
                dataReader = this.sqlCommand.ExecuteReader();
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
                //				return null;
            }

            return dataReader;
        }

        public void CloseDataReader(System.Data.OleDb.OleDbDataReader dataReader)
        {
            if (dataReader == null)
                return;

            try
            {
                dataReader.Close();
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }

        public System.Data.DataSet GetDataSet2(string sql)
        {
            try
            {
                OleDbDataAdapter da = new OleDbDataAdapter(sql, this.sqlConnection);
                DataSet ds = new DataSet();
                da.Fill(ds);
                return ds;
            }
            catch (Exception err)
            {
                Console.WriteLine(err.Message);
                throw new Exception(err.Message);
                
            }
        }

        public System.Data.DataTable GetDataSet(string sql)
        {
            try
            {
                OleDbDataAdapter da = new OleDbDataAdapter(sql, this.sqlConnection);
                DataTable dt = new DataTable();
                da.Fill(dt);
                return dt;
            }
            catch (Exception err)
            {
                return null;
            }
        }

        public bool ExecuteNonQuery(string commandText)
        {
            this.sqlCommand.CommandText = commandText;

            try
            {
                return (this.sqlCommand.ExecuteNonQuery() > 0);
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
                //				return false;
            }
        }

        public bool BeginTrans()
        {
            try
            {
                sqlTransaction = sqlConnection.BeginTransaction();

                sqlCommand.Transaction = sqlTransaction;
            }
            catch (Exception err)
            {
                Console.WriteLine("BeginTrans:" + err.Message);
                return false;
            }

            return true;
        }

        public bool CommitTrans()
        {
            try
            {
                sqlTransaction.Commit();
            }
            catch (Exception err)
            {
                Console.WriteLine("CommitTrans:" + err.Message);
                return false;
            }

            return true;
        }

        public bool RollbackTrans()
        {
            try
            {
                sqlTransaction.Rollback();
            }
            catch (Exception err)
            {
                Console.WriteLine("RollbackTrans:" + err.Message);
                return false;
            }

            return true;
        }



        public string GetSqlWithParameters(OleDbCommand command)
        {
            string sql = command.CommandText;

            foreach (OleDbParameter param in command.Parameters)
            {
                string paramValue;

                if (param.Value == DBNull.Value || param.Value == null)
                {
                    paramValue = "NULL";
                }
                else if (param.Value is string || param.Value is DateTime)
                {
                    paramValue = $"'{param.Value.ToString().Replace("'", "''")}'"; // 문자열/날짜는 따옴표 처리
                }
                else
                {
                    paramValue = param.Value.ToString(); // 숫자 등은 그대로
                }

                sql = sql.Replace(param.ParameterName, paramValue);
            }

            return sql;
        }

        // added by kdg. 250424
        // INSERT 쿼리 실행 함수
        public bool InsertData(string tableName, Dictionary<string, object> data)
        {
            try
            {
                string columns = string.Join(", ", data.Keys);
                string values = string.Join(", ", data.Keys.Select(k => "@" + k));

                string sql = $"INSERT INTO {tableName} ({columns}) VALUES ({values})";

                sqlCommand.CommandText = sql;
                sqlCommand.Parameters.Clear();

                foreach (var item in data)
                {
                    sqlCommand.Parameters.AddWithValue("@" + item.Key, item.Value ?? DBNull.Value);
                }

                foreach (var kvp in data)
                {
                    if (kvp.Value is string str)
                    {
                        Console.WriteLine($"{kvp.Key} : {str.Length} chars");
                    }
                }
                Console.WriteLine(GetSqlWithParameters(sqlCommand));
                return sqlCommand.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                throw new Exception("[InsertData] " + ex.Message);
            }
        }

        // UPDATE 쿼리 실행 함수
        public bool UpdateData(string tableName, Dictionary<string, object> data, string whereClause)
        {
            try
            {
                string setClause = string.Join(", ", data.Keys.Select(k => $"{k} = @{k}"));
                string sql = $"UPDATE {tableName} SET {setClause} WHERE {whereClause}";

                sqlCommand.CommandText = sql;
                sqlCommand.Parameters.Clear();

                foreach (var item in data)
                {
                    sqlCommand.Parameters.AddWithValue("@" + item.Key, item.Value ?? DBNull.Value);
                }
                return sqlCommand.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                throw new Exception("[UpdateData] " + ex.Message);
            }
        }
    }
}