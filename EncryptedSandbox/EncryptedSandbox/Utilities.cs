using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EncryptedSandbox
{
    public class Utilities
    {
        private Object myProperty;

        public Object GetMyProperty()
        {
            return myProperty;
        }

        public static int ExecuteQuery(string sql)
        {
            //Execute the query against the database
            return 1;
        }
    }
}
