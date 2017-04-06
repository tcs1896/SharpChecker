using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpChecker.attributes
{
    [SubtypeOf("MaybeNull")]
    [AttributeUsage(AttributeTargets.All, Inherited = true, AllowMultiple = true)]
    public class NonNullAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.All, Inherited = true, AllowMultiple = true)]
    public class MaybeNullAttribute : Attribute
    {
    }
}
