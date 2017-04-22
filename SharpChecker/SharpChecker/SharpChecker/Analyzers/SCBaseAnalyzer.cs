using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SharpChecker.Attributes;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SharpChecker.Enums;

namespace SharpChecker
{
    class SCBaseAnalyzer
    {
        //This is the default diagnostic
        private const string DiagnosticId = "SharpChecker";
        private const string Title = "Error in attribute applications";
        private const string MessageFormat = "Attribute application error {0}";
        private const string Description = "There is a mismatch between the effective attribute and the one expected";
        private const string Category = "Syntax";
        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        //This is used to indicate when something has not yet been implemented
        private const string NIDiagnosticId = "NotImplementedId";
        private const string NITitle = "Error in SharpChecker implementation";
        private const string NIMessageFormat = "This use case has not been implemented. {0}";
        private const string NIDescription = "The checker applied doesn't handle this use case";
        private const string NICategory = "Syntax";
        private static DiagnosticDescriptor NIRule = new DiagnosticDescriptor(NIDiagnosticId, NITitle, NIMessageFormat, NICategory, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: NIDescription);

        //Maintain a reference to the ASTUtilities class so that we can leverage common functionality
        public ASTUtilities ASTUtil { get; set; }

        /// <summary>
        /// Get the rules associated with this analysis
        /// </summary>
        /// <returns>SharpChecker and NotImplemented</returns>
        [return:NonNull]
        public virtual Dictionary<string, DiagnosticDescriptor> GetRules()
        {
            var dict = new Dictionary<string, DiagnosticDescriptor>
            {
                { nameof(SharpCheckerAttribute).Replace("Attribute", ""), Rule },
                { AttributeType.NotImplemented.ToString(), NIRule }
            };
            Debug.Assert(dict != null, "dict:NonNull");
            return dict;
        }

        /// <summary>
        /// We are interested in InvocationExpressions because we need to check that the arguments passed to a method with annotated parameters
        /// have arguments with the same annotations.  We are interested in SimpleAssignmentExpressions because we only want to allow an annotated 
        /// to an annotated variable when we can ensure that the value is of the appropriate annotated type.  Finally, we are interested in return
        /// statements because we need to ensure that the value returned abides by the return attribute.
        /// </summary>
        /// <returns>The syntax kinds we are interested in analyzing</returns>
        public virtual SyntaxKind[] GetSyntaxKinds()
        {
            return new SyntaxKind[] { SyntaxKind.InvocationExpression, SyntaxKind.SimpleAssignmentExpression, SyntaxKind.ReturnStatement };
        }

        /// <summary>
        /// This method is called during the init phase of an analysis and should be used to
        /// register attributes for analysis.  This is also where type annotation hierarchy
        /// is established.  Each Node has a "Supertypes" collection of Nodes.  An instance
        /// of a subtype may be used where a supertype is expected.
        /// </summary>
        public virtual List<Node> GetAttributesToUseInAnalysis()
        {
            return new List<Node>() { new Node() { AttributeName = nameof(SharpCheckerAttribute) } };
        }

        /// <summary>
        /// This is the link between the Analyzer class and the SyntaxWalker class which will verify
        /// the associated attributes.  If you implement a SyntaxWalker class which is specific to your
        /// type system, then you should override this method to inform the framework of the appropriate
        /// SyntaxWalker class.
        /// </summary>
        /// <returns>The type SCBaseSyntaxWalker</returns>
        public virtual Type GetSyntaxWalkerType()
        {
            return typeof(SCBaseSyntaxWalker);
        }

        /// <summary>
        /// Descend into the syntax tree as far as necessary to determine the associated attributes which are expected.
        /// Called recursively when appropriate below as we descend into the tree.
        /// </summary>
        /// <param name="context">The analysis context</param>
        public void AnalyzeExpression(SyntaxNodeAnalysisContext context, SyntaxNode node)
        {
            //Determine the type of sytax node with which we are dealing
            switch (node.Kind())
            {
                case SyntaxKind.InvocationExpression:
                    var invocationExpr = node as InvocationExpressionSyntax;
                    AnalyzeInvocationExpr(context, invocationExpr);
                    break;
                case SyntaxKind.SimpleAssignmentExpression:
                    var assignmentExpression = node as AssignmentExpressionSyntax;
                    AnalyzeAssignmentExpression(context, assignmentExpression);
                    break;
                case SyntaxKind.ReturnStatement:
                    var returnStmt = node as ReturnStatementSyntax;
                    AnalyzeReturnStatement(context, returnStmt);
                    break;
            }
        }

        /// <summary>
        /// Collect the attributes associated with an identifier or expression present
        /// to the right of a return keyword (i.e. the value being returned)
        /// </summary>
        /// <param name="context"></param>
        /// <param name="returnStmt"></param>
        private void AnalyzeReturnStatement(SyntaxNodeAnalysisContext context, ReturnStatementSyntax returnStmt)
        {
            AnalyzeSubexpression(context, returnStmt.Expression);
        }

        /// <summary>
        /// Get the annotated types of the formal parameters of a method and the return type.
        /// If the invocation occurs in the void context then the return type will not be significant,
        /// but if the invocation occurs within the context of an argument list to another invocation
        /// then we will need to know the annotated type which is expected to result.
        /// </summary>
        /// <param name="context">The analysis context</param>
        /// <param name="invocationExpr">A syntax node</param>
        internal virtual void AnalyzeInvocationExpr(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocationExpr)
        {
            //This will store the method associated with the invocation expression
            IMethodSymbol memberSymbol = null;

            if (invocationExpr.Expression is IdentifierNameSyntax identifierNameExpr)
            {
                memberSymbol = context.SemanticModel.GetSymbolInfo(identifierNameExpr).Symbol as IMethodSymbol;
            }
            else if (invocationExpr.Expression is MemberAccessExpressionSyntax memAccessExpr)
            {
                memberSymbol = context.SemanticModel.GetSymbolInfo(memAccessExpr).Symbol as IMethodSymbol;
            }
            else if (invocationExpr.Expression is MemberBindingExpressionSyntax memBindExpr)
            {
                //This was necessary to support the null propogating dot operator
                if (memBindExpr.Name is IdentifierNameSyntax identNameSyn)
                {
                    memberSymbol = context.SemanticModel.GetSymbolInfo(identNameSyn).Symbol as IMethodSymbol;
                }
            }

            //If we failed to lookup the symbol then short circuit 
            if (memberSymbol == null) { return; }

            //Grab any attributes associated with the return type of the method
            var returnTypeAttrs = memberSymbol.GetReturnTypeAttributes();
            if (returnTypeAttrs.Count() > 0)
            {
                var retAttrStrings = ASTUtil.GetSharpCheckerAttributeStrings(returnTypeAttrs);
                if (!ASTUtil.AnnotationDictionary.ContainsKey(invocationExpr.Expression))
                {
                    ASTUtil.AnnotationDictionary.TryAdd(invocationExpr.Expression, new List<List<String>>() { retAttrStrings });
                }
            }

            //Grab the argument list so we can interrogate it
            var argumentList = invocationExpr.ArgumentList as ArgumentListSyntax;

            for (int i = 0; i < argumentList.Arguments.Count; i++)
            {
                //Here we are handling the case where the argument is an identifier
                if (argumentList.Arguments[i].Expression is IdentifierNameSyntax argI)
                {
                    var symbol = context.SemanticModel.GetSymbolInfo(argI).Symbol;
                    if (symbol != null)
                    {
                        ASTUtil.AddSymbolAttributes(argI, symbol);
                    }
                }
                else
                {
                    //If this is another invocation expression then we should recurse
                    if (argumentList.Arguments[i].Expression is InvocationExpressionSyntax argInvExpr)
                    {
                        AnalyzeInvocationExpr(context, argInvExpr);
                    }
                    else if (argumentList.Arguments[i].Expression is ConditionalExpressionSyntax conditional)
                    {
                        //We are dealing with a ternary operator, and need to know the annotated type of each branch
                        AnalyzeExpression(context, conditional.WhenTrue);
                        AnalyzeExpression(context, conditional.WhenFalse);
                    }
                    else
                    {
                        if (context.SemanticModel.GetSymbolInfo(argumentList.Arguments[i].Expression).Symbol is IFieldSymbol fieldSymbol)
                        {
                            Debug.Assert(fieldSymbol != null, "fieldSymbol:NonNull");
                            ASTUtil.AddSymbolAttributes(argumentList.Arguments[i].Expression, fieldSymbol);
                        }
                        else if (context.SemanticModel.GetSymbolInfo(argumentList.Arguments[i].Expression).Symbol is IPropertySymbol propertySymbol)
                        {
                            Debug.Assert(propertySymbol != null, "propertySymbol:NonNull");
                            ASTUtil.AddSymbolAttributes(argumentList.Arguments[i].Expression, propertySymbol);
                        }
                    }
                }
            }

            //Check to see if any of the formal parameters of the method being invoked have associated attributes
            //In order to do this, we need to lookup the appropriate method signature.  
            ImmutableArray<IParameterSymbol> paramSymbols = memberSymbol.Parameters;
            //Create the lists of lists which will hold the attributes associated with each of the attributes in the attribute list
            List<List<string>> attrListParams = new List<List<string>>();
            bool hasAttrs = false;

            //Iterate over the parameters with an explicit index so we can compare the appropriate argument below
            for (int i = 0; i < paramSymbols.Count(); i++)
            {
                //Create a new list to hold the attributes
                List<string> paramAttrs = new List<string>();

                //Get the formal parameter
                var param = paramSymbols[i];
                //Get the attributes associated with this parameter
                var attributes = param.GetAttributes();
                var attributeStrings = ASTUtil.GetSharpCheckerAttributeStrings(attributes);

                for (int j = 0; j < attributeStrings.Count(); j++)
                {
                    var attr = attributeStrings[j];
                    paramAttrs.Add(attr);
                    hasAttrs = true;
                }

                attrListParams.Add(paramAttrs);
            }

            //If we didn't find any annotations then we return the appropriate enum value indicating as much
            if (hasAttrs)
            {
                //Add the expected attributes of the arguments to our collection
                ASTUtil.AnnotationDictionary.TryAdd(argumentList, attrListParams);
            }
        }

        /// <summary>
        /// Get the annotated type associated with the LHS of an assignment (maybe called the receiver)
        /// </summary>
        /// <param name="context">The analysis context</param>
        /// <param name="assignmentExpression">A syntax node</param>
        public void AnalyzeAssignmentExpression(SyntaxNodeAnalysisContext context, AssignmentExpressionSyntax assignmentExpression)
        {
            AnalyzeSubexpression(context, assignmentExpression.Left);
            AnalyzeSubexpression(context, assignmentExpression.Right);
        }

        /// <summary>
        /// Analyze the expression and add the associated attributes to the global symbol table
        /// </summary>
        /// <param name="context">The analysis context</param>
        /// <param name="expr">An expression</param>
        private void AnalyzeSubexpression(SyntaxNodeAnalysisContext context, ExpressionSyntax expr)
        {
            // First check the variable to which we are assigning
            if (expr is IdentifierNameSyntax identifierName)
            {
                List<string> attrs = ASTUtil.GetAttributes(context, identifierName);
                if (attrs.Count() > 0)
                {
                    //Add the list of expected attributes to the dictionary
                    ASTUtil.AnnotationDictionary.TryAdd(identifierName, new List<List<string>>() { attrs });
                }
            }
            else if (expr is MemberAccessExpressionSyntax memAccess)
            {
                List<string> memAttrs = ASTUtil.GetAttributes(context, memAccess);
                if (memAttrs.Count() > 0)
                {
                    //Add the list of expected attributes to the dictionary
                    ASTUtil.AnnotationDictionary.TryAdd(memAccess, new List<List<string>>() { memAttrs });
                }
            }
        }
    }
}
