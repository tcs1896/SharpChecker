using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpChecker.Attributes
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
    public class SubtypeOfAttribute : Attribute
    {
        //Used to establish attribute hierarchy
        string SubtypeOf;
        public SubtypeOfAttribute(string supertype)
        {
            this.SubtypeOf = supertype;
        }
    }
}
