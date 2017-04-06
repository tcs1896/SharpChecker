using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpChecker
{
    struct Node
    {
        public string AttributeName { get; set; }
        public List<Node> Supertypes { get; set; }
    }
}
