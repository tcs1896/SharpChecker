using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SharpChecker.attributes;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

        public ASTUtilities ASTUtil { get; set; }

        public virtual Dictionary<string, DiagnosticDescriptor> GetRules()
        {
            var dict = new Dictionary<string, DiagnosticDescriptor>
            {
                { nameof(SharpCheckerAttribute).Replace("Attribute", ""), Rule }
            };
            return dict;
        }

        public virtual SyntaxKind[] GetSyntaxKinds()
        {
            //We are interested in InvocationExpressions because we need to check that the arguments passed to a method with annotated parameters
            //have arguments with the same annotations.  We are interested in SimpleAssignmentExpressions because we only want to allow an annotated 
            //to an annotated variable when we can ensure that the value is of the appropriate annotated type.
            return new SyntaxKind[] { SyntaxKind.InvocationExpression, SyntaxKind.SimpleAssignmentExpression };
        }

        /// <summary>
        /// This class is called during the init phase of an analysis and should be used to
        /// register attributes for analysis.  Within the method you should use
        /// <sref>AddAttributeClassToAnalysis</sref> to register each attribute
        /// </summary>
        public virtual List<String> GetAttributesToUseInAnalysis()
        {
            return new List<String>() { nameof(SharpCheckerAttribute) };
        }

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
            //Determine what type of sytax node we are dealing with
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
            }
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
            var identifierNameExpr = invocationExpr.Expression as IdentifierNameSyntax;

            if (identifierNameExpr != null)
            {
                memberSymbol = context.SemanticModel.GetSymbolInfo(identifierNameExpr).Symbol as IMethodSymbol;
            }
            else
            { 
                var memAccessExpr = invocationExpr.Expression as MemberAccessExpressionSyntax;
                memberSymbol = context.SemanticModel.GetSymbolInfo(memAccessExpr).Symbol as IMethodSymbol;
            }

            //If we failed to lookup the symbol then bail
            if (memberSymbol == null)
            {
                return;
            }

            //Grab any attributes associated with the return type of the method
            //Should we identify the attributes associated with the return type positionally?
            //These always occur in the first list, then the attributes associated with the 
            //arguments are tracked in subsequent ones?
            var returnTypeAttrs = memberSymbol.GetReturnTypeAttributes();
            if (returnTypeAttrs.Count() > 0)
            {
                var retAttrStrings = ASTUtil.GetSharpCheckerAttributeStrings(returnTypeAttrs);
                //An exception was generated here because we were attempting to add the same name twice.
                //This leads me to believe that the same identifier occurring in different locations in
                //the source text may not be distinguished.  We could perhaps introduce a composite key
                //involving "span" so that we could distinguish uses at different locations in the source text.
                if (!ASTUtil.AnnotationDictionary.ContainsKey(identifierNameExpr))
                {
                    ASTUtil.AnnotationDictionary.TryAdd(identifierNameExpr, new List<List<String>>() { retAttrStrings });
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
                    ASTUtil.AddSymbolAttributes(argI, symbol);
                }
                else
                {
                    //If this is another invocation expression then we should recurse
                    //This is an important pattern which should be replicated elsewhere.
                    //As an example: If we have a binary expression we should recursively 
                    //analyze the right and left then combine the result - like type checking
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
                            ASTUtil.AddSymbolAttributes(argumentList.Arguments[i].Expression, fieldSymbol);
                        }
                        else
                        {
                            var propertySymbol = context.SemanticModel.GetSymbolInfo(argumentList.Arguments[i].Expression).Symbol as IPropertySymbol;
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
            // First check the variable to which we are assigning
            if (assignmentExpression.Left is IdentifierNameSyntax identifierName)
            {
                List<string> attrs = ASTUtil.GetAttributes(context, identifierName);

                //If we didn't find any annotations then we return the appropriate enum value indicating as much
                if (attrs.Count() > 0)
                {
                    //Add the list of expected attributes to the dictionary
                    ASTUtil.AnnotationDictionary.TryAdd(identifierName, new List<List<string>>() { attrs });
                }
            }
            else
            {
                if (assignmentExpression.Left is MemberAccessExpressionSyntax memAccess)
                {
                    List<string> memAttrs = ASTUtil.GetAttributes(context, memAccess);

                    //If we didn't find any annotations then we return the appropriate enum value indicating as much
                    if (memAttrs.Count() > 0)
                    {
                        //Add the list of expected attributes to the dictionary
                        ASTUtil.AnnotationDictionary.TryAdd(memAccess, new List<List<string>>() { memAttrs });
                    }
                }
            }
        }
    }
}
