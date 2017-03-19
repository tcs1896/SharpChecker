using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpChecker
{
    [AttributeUsage(AttributeTargets.All, Inherited = true, AllowMultiple = true)]
    class SharpCheckerAttribute : Attribute
    {
        Attribute SubtypeOf;
        public SharpCheckerAttribute(Attribute myvalue)
        {
            this.SubtypeOf = myvalue;
        }
    }
}
