using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SharpChecker
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class SharpCheckerAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "SharpCheckerMethodParams";
        internal const string Title = "Error in attribute applications";
        internal const string MessageFormat = "Attribute application error {0}";
        internal const string Description = "There is a mismatch between the attribute of the formal parameter and that of the argument";
        internal const string Category = "Syntax";
        private const string attributeName = "EncryptedSandbox.EncryptedAttribute";
        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            //We are interested in InvocationExpressions because we need to check that the arguments passed to a method with annotated parameters
            //have arguments with the same annotations.  We are interested in SimpleAssignmentExpressions because we only want to allow an annotated 
            //to an annotated variable when we can ensure that the value is of the appropriate annotated type.
            context.RegisterSyntaxNodeAction<SyntaxKind>(AnalyzeNode, SyntaxKind.InvocationExpression, SyntaxKind.SimpleAssignmentExpression);
        }

        private void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            //Determine if we are dealing with an InvocationExpression or a SimpleAssignment
            var invocationExpr = context.Node as InvocationExpressionSyntax;
            if (invocationExpr != null)
            {
                AnalyzeInvocationExpr(context, invocationExpr);
            }
            else
            {
                //We could safely cast right now, but if the filter above changes in the future we will not be able to do so
                var assignmentExpression = context.Node as AssignmentExpressionSyntax;
                if (assignmentExpression != null)
                {
                    AnalyzeAssignmentExpression(context, assignmentExpression);
                }
            }
        }

        /// <summary>
        /// If the variable to which we are assigning a value has an annotation, then we need to verify that the
        /// expression to which it is assigned with yeild a value with the appropriate annoation
        /// </summary>
        /// <param name="context"></param>
        /// <param name="assignmentExpression"></param>
        private void AnalyzeAssignmentExpression(SyntaxNodeAnalysisContext context, AssignmentExpressionSyntax assignmentExpression)
        {
            // First check the variable to which we are assigning
            var identifierName = assignmentExpression.Left as IdentifierNameSyntax;

            // Get the associated symbol
            SymbolInfo info = context.SemanticModel.GetSymbolInfo(identifierName);
            ISymbol symbol = info.Symbol;
            if (symbol != null)
            {
                // Check if there are attributes associated with this symbol
                var argAttrs = symbol.GetAttributes();

                foreach (var argAttr in argAttrs)
                {
                    if (argAttr.AttributeClass.ToString() == attributeName)
                    {
                        // We have found an attribute, so now we verify the RHS
                        var invocationExpr = assignmentExpression.Right as InvocationExpressionSyntax;
                        if (invocationExpr != null)
                        {
                            // Used to store the method symbol associated with the invocation expression
                            IMethodSymbol memberSymbol = null;

                            var identifierNameExpr = invocationExpr.Expression as IdentifierNameSyntax;
                            if (identifierNameExpr != null)
                            {
                                memberSymbol = context.SemanticModel.GetSymbolInfo(identifierNameExpr).Symbol as IMethodSymbol;
                            }
                            else
                            {
                                //If we don't have a local method invocation, we may have a static or instance method invocation
                                var memberAccessExpr = invocationExpr.Expression as MemberAccessExpressionSyntax;
                                if (memberAccessExpr != null)
                                {
                                    memberSymbol = context.SemanticModel.GetSymbolInfo(memberAccessExpr).Symbol as IMethodSymbol;
                                }
                            }

                            if (memberSymbol != null)
                            {
                                // Now we check the return type to see if there is an attribute assigned
                                bool foundMatch = false;
                                var returnTypeAttrs = memberSymbol.GetReturnTypeAttributes();
                                foreach (var retAttr in returnTypeAttrs)
                                {
                                    if (retAttr.AttributeClass.ToString() == attributeName)
                                    {
                                        foundMatch = true;
                                    }
                                }

                                //If we haven't found a match then present a diagnotic error
                                if (!foundMatch)
                                {
                                    var diagnostic = Diagnostic.Create(Rule, invocationExpr.GetLocation(), Description);
                                    //Now we register this diagnostic with visual studio
                                    context.ReportDiagnostic(diagnostic);
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// If we are invoking a method which has attributes on its formal parameters, then we need to verify that
        /// the arguments passed abide by these annotations
        /// </summary>
        /// <param name="context"></param>
        /// <param name="invocationExpr"></param>
        private void AnalyzeInvocationExpr(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocationExpr)
        {
            var identifierNameExpr = invocationExpr.Expression as IdentifierNameSyntax;
            if (identifierNameExpr == null) return;

            //This will lookup the method associated with the invocation expression
            var memberSymbol = context.SemanticModel.GetSymbolInfo(identifierNameExpr).Symbol as IMethodSymbol;
            //If we failed to lookup the symbol then bail
            if (memberSymbol == null) return;


            //Check to see if any of the formal parameters of the method being invoked have associated attributes
            //In order to do this, we need to lookup the appropriate method signature.  
            ImmutableArray<IParameterSymbol> paramSymbols = memberSymbol.Parameters;
            //Iterate over the parameters with an explicit index so we can compare the appropriate argument below
            for (int i = 0; i < paramSymbols.Count(); i++)
            {
                //Get the formal parameter
                var param = paramSymbols[i];

                var attributes = param.GetAttributes();

                foreach (var attr in attributes)
                {
                    //var myName = $"The attr {attr.AttributeClass}";
                    if (attr.AttributeClass.ToString() == attributeName)
                    {
                        //Locate the argument which corresponds to the one with the attribute and 
                        //ensure that it also has the attribute
                        bool foundMatch = false;

                        //Grab the argument list so we can interrogate it
                        var argumentList = invocationExpr.ArgumentList as ArgumentListSyntax;
                        //It may seem unnecessary to check that i is valid, since providing the incorrect number of arguments should not
                        //type check.  However, there is some candidate analysis while the code is incomplete.
                        if (i < argumentList.Arguments.Count())
                        {
                            //Here we are handling the case where the argument is an identifier
                            var argI = argumentList.Arguments[i].Expression as IdentifierNameSyntax;
                            //var argSymbol = context.SemanticModel.GetDeclaredSymbol(argumentList.Arguments[0]); //GetSymbolInfo(argumentList.Arguments[0]).Symbol as IPropertySymbol;

                            if (argI != null)
                            {
                                SymbolInfo info = context.SemanticModel.GetSymbolInfo(argI);
                                ISymbol symbol = info.Symbol;
                                if (symbol != null)
                                {
                                    var argAttrs = symbol.GetAttributes();

                                    foreach (var argAttr in argAttrs)
                                    {
                                        if (argAttr.AttributeClass.ToString() == attributeName)
                                        {
                                            foundMatch = true;
                                        }
                                    }

                                    //If we haven't found a match then present a diagnotic error
                                    if (!foundMatch)
                                    {
                                        var diagnostic = Diagnostic.Create(Rule, argI.GetLocation(), Description);
                                        //Now we register this diagnostic with visual studio
                                        context.ReportDiagnostic(diagnostic);
                                    }
                                }
                            }
                            else
                            {
                                //We are probably dealing with a literal - which cannot have the associated attribute
                                var argLit = argumentList.Arguments[i].Expression as LiteralExpressionSyntax;
                                if (argLit != null)
                                {
                                    var diagnostic = Diagnostic.Create(Rule, argLit.GetLocation(), Description);
                                    //Now we register this diagnostic with visual studio
                                    context.ReportDiagnostic(diagnostic);
                                }
                                else
                                {
                                    //TODO: Need to be able to lookup properties
                                    //var argMemAccess = argumentList.Arguments[i].Expression as MemberAccessExpressionSyntax;
                                    //if(argMemAccess != null)
                                    //{
                                    //    memberSymbol = context.SemanticModel.GetSymbolInfo(memberAccessExpr).Symbol as IPropertySymbol;

                                    //}
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
