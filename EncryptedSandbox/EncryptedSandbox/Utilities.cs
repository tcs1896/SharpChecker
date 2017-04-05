using SharpChecker.attributes;
using System;

namespace EncryptedSandbox
{
    public class Utilities
    {
        [Encrypted]
        public Object MyProperty { get; set; }

        public static int ExecuteQuery(string sql)
        {
            //Execute the query against the database
            return 1;
        }

        public void WriteToDisk([Encrypted] string encVal)
        {
            //Serialize the value, and write it to disk
        }
    }
}
