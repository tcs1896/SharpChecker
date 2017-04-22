using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpChecker
{
    /// <summary>
    /// This struct is used to capture the attributes to be analyzed and their hierarchical relationship.
    /// The primary reason for modelling the hierachy in this way, as opposed to specifying attribute
    /// classes as subtypes in C#, is that C# only supports single inheritence.  We want to support
    /// annotated types which have more than one supertype.
    /// </summary>
    struct Node
    {
        public string AttributeName { get; set; }
        public List<Node> Supertypes { get; set; }
    }
}
