using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpChecker.attributes
{
    //This is currently unused, but we may find a purpose in the future
    [Obsolete]
    [AttributeUsage(AttributeTargets.All, Inherited = true, AllowMultiple = true)]
    public class SharpCheckerAttribute : Attribute
    {
    }
}
