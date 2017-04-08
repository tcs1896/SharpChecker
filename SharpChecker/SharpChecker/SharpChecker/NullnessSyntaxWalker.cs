using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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

        /// <summary>
        /// We need to check for enclosing blocks with conditional comparisons to null which allow us
        /// to refine the type of an identifier.
        /// </summary>
        /// <param name="expectedAttributes">The expected attributes</param>
        /// <param name="node">The syntax node</param>
        internal override void VerifyExpectedAttrsInSyntaxNode(List<string> expectedAttributes, SyntaxNode node)
        {
            //Look for a guard which checks for null and refine the type
            if (node is IdentifierNameSyntax ident)
            {
                if(expectedAttributes != null && expectedAttributes.Count() > 0)
                {
                    var surroundingIfs = node.Ancestors().OfType<IfStatementSyntax>();
                    foreach(var ifstmt in surroundingIfs)
                    {
                        var condition = ifstmt.Condition;
                        switch(condition.Kind())
                        {
                            case SyntaxKind.EqualsExpression:
                                //TODO: We might do something like explicitly checking for null and
                                //initializeing a value.  However, this won't be a containing block.
                                //We could also be in the else block of an equals comparison to null.
                                //Would that be considered a containing block?
                                break;
                            case SyntaxKind.NotEqualsExpression:
                                var notEqExpr = condition as BinaryExpressionSyntax;
                                ExpressionSyntax exprSyn = null;
                                if(notEqExpr.Right.Kind() == SyntaxKind.NullLiteralExpression)
                                {
                                    exprSyn = notEqExpr.Left;
                                }
                                else if (notEqExpr.Left.Kind() == SyntaxKind.NullLiteralExpression)
                                {
                                    exprSyn = notEqExpr.Right;
                                }

                                if(exprSyn != null)
                                {
                                    //lookup the symbol to see if it is the same as ident
                                    var identSymbol = context.SemanticModel.GetSymbolInfo(ident).Symbol;
                                    var conditionSymbol = context.SemanticModel.GetSymbolInfo(exprSyn).Symbol;

                                    if (identSymbol == conditionSymbol)
                                    {
                                        //Update the attribute
                                        if (AnnotationDictionary.ContainsKey(ident))
                                        {
                                            //TODO: We should really be replacing the MaybeNull attribute with NotNull instead of
                                            //replacing all attributes with this one.
                                            AnnotationDictionary[ident] = new List<List<string>>() { new List<string>() { "NonNull" } };
                                        }
                                    }
                                }
                                break;
                        }
                    }
                }
            }

            //Now perform the standard verification
            base.VerifyExpectedAttrsInSyntaxNode(expectedAttributes, node);
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
