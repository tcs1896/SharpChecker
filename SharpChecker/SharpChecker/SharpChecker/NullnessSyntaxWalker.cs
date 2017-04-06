using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
        public NullnessSyntaxWalker(Dictionary<string, DiagnosticDescriptor> rulesDict, ConcurrentDictionary<SyntaxNode, List<List<String>>> annotationDictionary, SemanticModelAnalysisContext context, List<Node> attributesOfInterest) :
            base(rulesDict, annotationDictionary, context, attributesOfInterest)
        { }

        internal override void VerifyInvocationExpr(InvocationExpressionSyntax invocationExpr)
        {
            //If the member being dereferenced may be null then present a diagnostic
            if (invocationExpr.Expression is MemberAccessExpressionSyntax memAccess)
            {
                List<List<String>> expectedAttributes = null;
                if (AnnotationDictionary.ContainsKey(memAccess.Expression))
                {
                    expectedAttributes = AnnotationDictionary[memAccess.Expression];
                    if(expectedAttributes[0].Contains("MaybeNull"))
                    {
                        ReportDiagsForEach(memAccess.Expression.GetLocation(), new List<string>() { "MaybeNull" }, new List<string>());
                    }
                }
            }

            //Now perform the standard verification
            base.VerifyInvocationExpr(invocationExpr);
        }

        internal override string GetDefaultForStringLiteral()
        {
            return "NonNull";
        }

        internal override string GetDefaultForNullLiteral()
        {
            return "MaybeNull";
        }
    }
}
