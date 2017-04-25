using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpChecker.Attributes
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
