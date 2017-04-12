using SharpChecker.attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaintedSandbox
{
    class Program
    {
        [Untainted]
        private string GetCustomers = "Select * from dbo.Customers";

        static void Main(string[] args)
        {
            var prog = new Program();
            var dbAccess = new DatabaseAccess();
            dbAccess.ExecuteNonQuery(prog.GetCustomers);

            //TODO: Why doesn't this present an error
            dbAccess.ExecuteNonQuery(ReadUserInput());
            var userInput = ReadUserInput();
            dbAccess.ExecuteNonQuery(userInput);
        }

        [return:Tainted]
        public static string ReadUserInput()
        {
            string userInput = "' and drop Customer;";
            return userInput;
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
