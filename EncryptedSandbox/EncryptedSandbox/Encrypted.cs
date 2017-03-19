using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpChecker;

namespace EncryptedSandbox
{
    [SharpChecker]
    [AttributeUsage(AttributeTargets.All, Inherited = true, AllowMultiple = true)]
    class EncryptedAttribute : Attribute
    {
        //private bool isEncrypted;
        //public EncryptedAttribute(bool myvalue)
        //{
        //    this.isEncrypted = myvalue;
        //}
    }

    [AttributeUsage(AttributeTargets.All, Inherited = true, AllowMultiple = true)]
    class NotEncryptedAttribute : EncryptedAttribute
    {
    }
}
