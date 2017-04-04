using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpChecker
{
    class NullnessSyntaxWalker : SCBaseSyntaxWalker
    {
        /// <summary>
        /// Pass the arguments along to the SCBaseSyntaxWalker constructor
        /// </summary>
        /// <param name="rulesDict"></param>
        /// <param name="annotationDictionary"></param>
        /// <param name="context"></param>
        /// <param name="attributesOfInterest"></param>
        public NullnessSyntaxWalker(Dictionary<string, DiagnosticDescriptor> rulesDict, ConcurrentDictionary<SyntaxNode, List<List<String>>> annotationDictionary, SemanticModelAnalysisContext context, List<string> attributesOfInterest) :
            base(rulesDict, annotationDictionary, context, attributesOfInterest)
        { }

        internal override string GetDefaultForStringLiteral()
        {
            return "NonNull";
        }

        internal override string GetDefaultForNullLiteral()
        {
            return "Nullable";
        }
    }
}
