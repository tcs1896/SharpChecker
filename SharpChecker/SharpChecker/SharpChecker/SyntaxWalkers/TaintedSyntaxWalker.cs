using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SharpChecker.Attributes;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpChecker
{
    class TaintedSyntaxWalker : SCBaseSyntaxWalker
    {
        /// <summary>
        /// Pass the arguments along to the SCBaseSyntaxWalker constructor
        /// </summary>
        /// <param name="rulesDict">A dictionary which maps strings used as attributes to their associated rules</param>
        /// <param name="annotationDictionary">The global symbol table which maps syntax nodes to the associated attributes</param>
        /// <param name="context">The analysis context which Roslyn provides</param>
        /// <param name="attributesOfInterest">The attributes which have been registered for analysis</param>
        public TaintedSyntaxWalker(Dictionary<string, DiagnosticDescriptor> rulesDict, ConcurrentDictionary<SyntaxNode, List<List<String>>> annotationDictionary, 
            SemanticModelAnalysisContext context, List<Node> attributesOfInterest) :
            base(rulesDict, annotationDictionary, context, attributesOfInterest)
        { }

        /// <summary>
        /// Get the default attribute which should be applied to string literal expressions
        /// </summary>
        /// <returns>Tainted</returns>
        internal override string GetDefaultForStringLiteral()
        {
            return nameof(TaintedAttribute).Replace("Attribute", "");
        }

        /// <summary>
        /// Get the default attribute which should be applied to null literal expressions
        /// </summary>
        /// <returns>Tainted</returns>
        internal override string GetDefaultForNullLiteral()
        {
            return nameof(TaintedAttribute).Replace("Attribute", "");
        }
    }
}
