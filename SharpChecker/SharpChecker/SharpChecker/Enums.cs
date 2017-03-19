using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpChecker
{
    class Enums
    {
        /// <summary>
        /// During a discussion with Professor Fluet on 2/23/17 he indicated that it would
        /// be good to have a mechanism for differentiating between responses from a general
        /// purpose method intended to analyze a syntax node and return the attribute.
        /// Below I have specified some of the possibilities.
        /// </summary>
        public enum AttributeType
        {
            //This will be the default for scenarios which haven't been covered
            NotImplemented = 0,
            //Indicates that an annotation is present
            HasAnnotation = 1,
            //Indidates that we analyzed the node successfully and found no annotation
            NoAnnotation = 2,
            //Like NoAnnotation, but we can use context or special knowledge to assign default 
            //(possibly with arbitrary user specified code)
            IsDefaultable = 3,
            //You shouldn't be asking for an annotation on this type of element
            Invalid = 4
        }
    }
}
