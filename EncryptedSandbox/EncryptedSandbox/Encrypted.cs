using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EncryptedSandbox
{
    [AttributeUsage(AttributeTargets.All, Inherited = true, AllowMultiple = true)]
    class EncryptedAttribute : Attribute
    {
        //private bool isEncrypted;
        //public EncryptedAttribute(bool myvalue)
        //{
        //    this.isEncrypted = myvalue;
        //}
    }
}
