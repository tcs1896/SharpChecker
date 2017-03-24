using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpChecker.attributes
{
    [AttributeUsage(AttributeTargets.All, Inherited = true, AllowMultiple = true)]
    public class EncryptedAttribute : Attribute
    {
        //Currently this attribute accepts no arguments, but it may make sense in
        //the future to leverage this mechanism to establish attribute hierarchy.
        //string SubtypeOf;
        //public EncryptedAttribute(string myvalue)
        //{
        //    this.SubtypeOf = myvalue;
        //}
    }
}
