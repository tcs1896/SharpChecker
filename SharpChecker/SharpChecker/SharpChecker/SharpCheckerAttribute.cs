using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpChecker
{
    [AttributeUsage(AttributeTargets.All, Inherited = true, AllowMultiple = true)]
    public class SharpCheckerAttribute : Attribute
    {
        //Instead of an attribute we really want an attribute type
        //Attribute SubtypeOf;
        //public SharpCheckerAttribute(Attribute myvalue)
        //{
        //    this.SubtypeOf = myvalue;
        //}
    }
}
