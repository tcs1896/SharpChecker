﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace SharpChecker
{
    class ASTUtilities
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
            //You shouldn't be asking for a annotation on this type of element
            Invalid = 4
        }

        /// <summary>
        /// Descend into the syntax node as far as necessary to determine the associated attributes which are expected
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public Tuple<AttributeType, List<List<string>>> GetAttributes(SyntaxNodeAnalysisContext context, SyntaxNode node)
        {
            //Determine if we are dealing with an InvocationExpression or a SimpleAssignment
            var invocationExpr = node as InvocationExpressionSyntax;
            if (invocationExpr != null)
            {
                return AnalyzeInvocationExpr(context, invocationExpr);
            }
            else {
                var assignmentExpression = context.Node as AssignmentExpressionSyntax;
                if (assignmentExpression != null)
                {
                    var assnExprAttrs = AnalyzeAssignmentExpression(context, assignmentExpression);
                    List<List<String>> asmtAttrs = new List<List<string>>();
                    asmtAttrs.Add(assnExprAttrs.Item2);
                    return new Tuple<AttributeType, List<List<string>>>(assnExprAttrs.Item1, asmtAttrs);
                }
            }

            return new Tuple<AttributeType, List<List<string>>>(AttributeType.NotImplemented, null);
        }

        /// <summary>
        /// Verify that the attributes which are expected are actually present
        /// </summary>
        /// <param name="context"></param>
        /// <param name="node"></param>
        /// <param name="expectedAttributes"></param>
        public void VerifyAttributes(SyntaxNodeAnalysisContext context, SyntaxNode node, List<List<string>> expectedAttributes, DiagnosticDescriptor rule, string description)
        {
            var invocationExpr = node as InvocationExpressionSyntax;
            if (invocationExpr != null)
            {
                VerifyInvocationExpr(context, invocationExpr, expectedAttributes, rule, description);
            }
            else
            {
                var assignmentExpression = context.Node as AssignmentExpressionSyntax;
                if (assignmentExpression != null)
                {
                    VerifyAssignmentExpression(context, assignmentExpression, expectedAttributes, rule, description);
                }
            }
        }

        /// <summary>
        /// Get the annotated types of the formal parameters of a method
        /// </summary>
        /// <param name="context"></param>
        /// <param name="invocationExpr"></param>
        /// <returns></returns>
        private Tuple<AttributeType, List<List<string>>> AnalyzeInvocationExpr(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocationExpr)
        {
            var identifierNameExpr = invocationExpr.Expression as IdentifierNameSyntax;
            if (identifierNameExpr == null)
            {
                return new Tuple<AttributeType, List<List<string>>>(AttributeType.NotImplemented, null);
            }

            //This will lookup the method associated with the invocation expression
            var memberSymbol = context.SemanticModel.GetSymbolInfo(identifierNameExpr).Symbol as IMethodSymbol;
            //If we failed to lookup the symbol then bail
            if (memberSymbol == null)
            {
                return new Tuple<AttributeType, List<List<string>>>(AttributeType.NotImplemented, null);
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
                //TODO: Allow arbitrary expressions as parameters...call a method to handle a single expression of
                //arbitrary complexity for each argument
                var attributes = param.GetAttributes();

                foreach (var attr in attributes)
                {
                    paramAttrs.Add(attr.AttributeClass.ToString());
                    hasAttrs = true;
                }

                attrListParams.Add(paramAttrs);
            }

            //If we didn't find any annotations then we return the appropriate enum value indicating as much
            if (hasAttrs)
            {
                return new Tuple<AttributeType, List<List<string>>>(AttributeType.HasAnnotation, attrListParams);
            }
            else
            {
                return new Tuple<AttributeType, List<List<string>>>(AttributeType.NoAnnotation, null);
            }
        }

        /// <summary>
        /// Get the annotated type associated with the LHS of an assignment (maybe called the receiver)
        /// </summary>
        /// <param name="context"></param>
        /// <param name="assignmentExpression"></param>
        /// <returns></returns>
        private Tuple<AttributeType, List<string>> AnalyzeAssignmentExpression(SyntaxNodeAnalysisContext context, AssignmentExpressionSyntax assignmentExpression)
        {
            // First check the variable to which we are assigning
            var identifierName = assignmentExpression.Left as IdentifierNameSyntax;

            if (identifierName != null)
            {
                return GetAttributes(context, identifierName);
            }

            return new Tuple<AttributeType, List<string>>(AttributeType.NotImplemented, null);
        }

        /// <summary>
        /// Get a list of attributes associated with a identifier
        /// </summary>
        /// <param name="context"></param>
        /// <param name="identifierName"></param>
        /// <returns></returns>
        public Tuple<AttributeType, List<string>> GetAttributes(SyntaxNodeAnalysisContext context, IdentifierNameSyntax identifierName)
        {
            List<string> attrs = new List<string>();

            // Get the associated symbol
            SymbolInfo info = context.SemanticModel.GetSymbolInfo(identifierName);
            ISymbol symbol = info.Symbol;
            if (symbol != null)
            {
                // Check if there are attributes associated with this symbol
                var argAttrs = symbol.GetAttributes();
                foreach (var argAttr in argAttrs)
                {
                    attrs.Add(argAttr.AttributeClass.ToString());
                }
            }
            else
            {
                return new Tuple<AttributeType, List<string>>(AttributeType.NotImplemented, null);
            }


            //If we didn't find any annotations then we return the appropriate enum value indicating as much
            if (attrs.Count() > 0)
            {
                return new Tuple<AttributeType, List<string>>(AttributeType.HasAnnotation, attrs);
            }
            else
            {
                return new Tuple<AttributeType, List<string>>(AttributeType.NoAnnotation, null);
            }
        }


        /// <summary>
        /// If we are invoking a method which has attributes on its formal parameters, then we need to verify that
        /// the arguments passed abide by these annotations
        /// </summary>
        private void VerifyInvocationExpr(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocationExpr, List<List<string>> expectedAttribute, DiagnosticDescriptor rule, string description)
        {
            var identifierNameExpr = invocationExpr.Expression as IdentifierNameSyntax;
            //We may need to recursively dig into the expression if the top level doesn't hand over a identifier
            //TODO: Present a diagnostic instead of failing silently
            if (identifierNameExpr == null) return;

            //This will lookup the method associated with the invocation expression
            var memberSymbol = context.SemanticModel.GetSymbolInfo(identifierNameExpr).Symbol as IMethodSymbol;
            //If we failed to lookup the symbol then bail
            if (memberSymbol == null) return;

            //Locate the argument which corresponds to the one with the attribute and 
            //ensure that it also has the attribute
            bool foundMatch = false;

            //Grab the argument list so we can interrogate it
            var argumentList = invocationExpr.ArgumentList as ArgumentListSyntax;

            for (int i = 0; i < expectedAttribute.Count; i++)
            {
                //If there are no required attributes for this argument, then move on to the next
                if (expectedAttribute[i].Count == 0) continue;

                //It may seem unnecessary to check that i is valid, since providing the incorrect number of arguments should not
                //type check.  However, there is some candidate analysis while the code is incomplete.
                if (i < argumentList.Arguments.Count())
                {
                    //Here we are handling the case where the argument is an identifier
                    var argI = argumentList.Arguments[i].Expression as IdentifierNameSyntax;
                    //TODO: We should be calling the same method which was used to determine the attributes previously "GetAttributes"
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
                                if (expectedAttribute[i].Contains(argAttr.AttributeClass.ToString()))
                                {
                                    //TODO: Need mechanism for verifying all attributes were found
                                    foundMatch = true;
                                }
                            }

                            //If we haven't found a match then present a diagnotic error
                            if (!foundMatch)
                            {
                                var diagnostic = Diagnostic.Create(rule, argI.GetLocation(), description);
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
                            var diagnostic = Diagnostic.Create(rule, argLit.GetLocation(), description);
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

        /// <summary>
        /// If the variable to which we are assigning a value has an annotation, then we need to verify that the
        /// expression to which it is assigned with yeild a value with the appropriate annoation
        /// </summary>
        private void VerifyAssignmentExpression(SyntaxNodeAnalysisContext context, AssignmentExpressionSyntax assignmentExpression, List<List<string>> expectedAttribute, DiagnosticDescriptor rule, string description)
        {
            var argAttrs = expectedAttribute.First();

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
                        //TODO: Need to verify that all expected attributes are accounted for,
                        //not just that one of the attributes matches
                        if (argAttrs.Contains(retAttr.AttributeClass.ToString()))
                        {
                            foundMatch = true;
                        }
                    }

                    //If we haven't found a match then present a diagnotic error
                    if (!foundMatch)
                    {
                        var diagnostic = Diagnostic.Create(rule, invocationExpr.GetLocation(), description);
                        //Now we register this diagnostic with visual studio
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }
    }
}