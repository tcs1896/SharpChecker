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
    class NullnessSyntaxWalker : SCBaseSyntaxWalker
    {
        /// <summary>
        /// Pass the arguments along to the SCBaseSyntaxWalker constructor
        /// </summary>
        /// <param name="rulesDict">A dictionary which maps strings used as attributes to their associated rules</param>
        /// <param name="annotationDictionary">The global symbol table which maps syntax nodes to the associated attributes</param>
        /// <param name="context">The analysis context which Roslyn provides</param>
        /// <param name="attributesOfInterest">The attributes which have been registered for analysis</param>
        public NullnessSyntaxWalker(Dictionary<string, DiagnosticDescriptor> rulesDict, ConcurrentDictionary<SyntaxNode, List<List<String>>> annotationDictionary, SemanticModelAnalysisContext context, List<Node> attributesOfInterest) :
            base(rulesDict, annotationDictionary, context, attributesOfInterest)
        { }

        /// <summary>
        /// Override the verification of invocation expressions to ensure that when a variable is dereferenced
        /// it has the appropriate annotated type.  This allows us to present an error when a possibly
        /// null value is unsafely dereferenced.  We still want to exercise the functionality present in the
        /// base class, so we conclude by calling the method which we are overridding here.
        /// </summary>
        /// <param name="invocationExpr">The invocation expression</param>
        internal override void VerifyInvocationExpr(InvocationExpressionSyntax invocationExpr)
        {
            //If the member being dereferenced may be null then present a diagnostic
            if (invocationExpr.Expression is MemberAccessExpressionSyntax memAccess)
            {
                List<List<String>> expectedAttributes = null;
                if (AnnotationDictionary.ContainsKey(memAccess.Expression))
                {
                    string maybeNull = nameof(MaybeNullAttribute).Replace("Attribute", "");
                    expectedAttributes = AnnotationDictionary[memAccess.Expression];
                    if(expectedAttributes[0].Contains(maybeNull))
                    {
                        ReportDiagsForEach(memAccess.Expression.GetLocation(), new List<string>() { maybeNull }, new List<string>());
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
        internal override void VerifyExpectedAttrsInSyntaxNode([NonNull] List<string> expectedAttributes, [NonNull] SyntaxNode node)
        {
            //Look for a guard which checks for null and refine the type
            ExpressionSyntax ident = null;
            if(node is IdentifierNameSyntax)
            {
                ident = node as IdentifierNameSyntax;
            }
            else if(node is MemberAccessExpressionSyntax)
            {
                ident = node as MemberAccessExpressionSyntax;
            }

            if (ident != null)
            {
                if(expectedAttributes != null && expectedAttributes.Count() > 0)
                {
                    var surroundingIfs = node.Ancestors().OfType<IfStatementSyntax>();
                    foreach(var ifstmt in surroundingIfs)
                    {
                        var condition = ifstmt.Condition;
                        switch(condition.Kind())
                        {
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

        /// <summary>
        /// Get the default attribute which should be applied to string literal expressions
        /// </summary>
        /// <returns>NonNull</returns>
        internal override string GetDefaultForStringLiteral()
        {
            return "NonNull";
        }

        /// <summary>
        /// Get the default attribute which should be applied to null literal expressions
        /// </summary>
        /// <returns>NonNull</returns>
        internal override string GetDefaultForNullLiteral()
        {
            return "MaybeNull";
        }
    }
}
