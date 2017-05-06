using SharpChecker.Attributes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaintedSandbox
{
    class Program
    {
        [Untainted]
        private static string GetCustomers = "Select * from dbo.Customers";
        static void Main(string[] args)
        {
            var dbAccess = new DatabaseAccess();
            dbAccess.ExecuteNonQuery(GetCustomers);
            dbAccess.ExecuteNonQuery(ReadUserInput());
        }
        [return: Tainted]
        public static string ReadUserInput()
        {
            string userInput = "' and drop Customer;";
            Debug.Assert(true, "userInput:Tainted");
            return userInput;
        }


        public static void second()
        {
            var dbAccess = new DatabaseAccess();
            var userInput = ReadUserInput();
            dbAccess.ExecuteNonQuery(userInput);

            //prog.GetCustomers = await GetSlowString();
        }


        public async static Task<string> getAsync()
        {
            var rtn = await GetSlowString();
            return rtn;
        }


        [return: Untainted]
        public static Task<String> GetSlowString()
        {
            var myTask = new Task<string>(() => "slow string");
            Debug.Assert(myTask != null, "myTask:Untainted");
            return myTask;
        }

    }

    class DatabaseAccess
    {
        public string ConnectionString { get; set; }
        public void ExecuteNonQuery([Untainted] string SQL)
        {
            var connection = OpenConnection();
            if(connection != null)
            {
                ExecuteNonQuery(SQL, connection);
            }
        }

        [return:Untainted]
        public string SanatizeQuery(string query)
        {
            string noInjection = String.Empty;
            Debug.Assert(true, "noInjection:Untainted");
            return noInjection;
        }

        public void ExecuteNonQuery([Untainted] string SQL, Connection connection)
        {
            //Execute the query
        }

        public Connection OpenConnection()
        {
            var connection = new Connection();
            if(connection.OpenConnection())
            {
                return connection;
            }
            else
            {
                //log an error
                return null;
            }
        }
    }

    class Connection
    {
        public bool IsOpen { get; set; }
        public bool OpenConnection()
        {
            return true;
        }
        public bool CloseConnection()
        {
            return true;
        }
    }
}
