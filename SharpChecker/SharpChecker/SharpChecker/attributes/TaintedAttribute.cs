using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpChecker.attributes
{
    [SubtypeOf("Tainted")]
    [AttributeUsage(AttributeTargets.All, Inherited = true, AllowMultiple = true)]
    public class UntaintedAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.All, Inherited = true, AllowMultiple = true)]
    public class TaintedAttribute : Attribute
    {
    }
}
