using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SharpChecker.ASTUtilities;

namespace SharpChecker
{
    class SCBaseSyntaxWalker : CSharpSyntaxWalker
    {
        private DiagnosticDescriptor rule;
        private string attributeName;
        private Dictionary<SyntaxNode, List<List<String>>> AnnotationDictionary;
        private SemanticModelAnalysisContext context;

        public int MyProperty { get; set; }

        public SCBaseSyntaxWalker(DiagnosticDescriptor rule, string attributeName, Dictionary<SyntaxNode, List<List<String>>> annotationDictionary, SemanticModelAnalysisContext context)
        {
            this.rule = rule;
            this.attributeName = attributeName;
            this.AnnotationDictionary = annotationDictionary;
            this.context = context;
        }

        /// <summary>
        /// This is invoked for nodes of all types followed by the more specific Visit
        /// methods such as VisitInvocationExpression
        /// </summary>
        /// <param name="node"></param>
        public override void Visit(SyntaxNode node)
        {
            //Visit all of the relevant nodes of the tree adding annotations
            //SyntaxWalker
            //VisitInvocationExpression{
            //if(Kind = something)
            //      invocationSimple()
            //else(Kind = somethingelse)
            //      invocationSomethingElse()
            //}
            //
            //Method to be overridden if default behavior is not desired
            //protected virtual invocationSimple()
            //{
            //      enforce subtyping
            //}

            base.Visit(node);
        }

        public override void VisitAssignmentExpression(AssignmentExpressionSyntax node)
        {
            VerifyAssignmentExpr(node);
            base.VisitAssignmentExpression(node);
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            VerifyInvocationExpr(node);
            base.VisitInvocationExpression(node);
        }

        /// <summary>
        /// If the variable to which we are assigning a value has an annotation, then we need to verify that the
        /// expression to which it is assigned with yeild a value with the appropriate annoation
        /// </summary>
        private void VerifyAssignmentExpr(AssignmentExpressionSyntax assignmentExpression)
        {
            // First check the variable to which we are assigning
            var identifierName = assignmentExpression.Left as IdentifierNameSyntax;

            //Get the expected attributes for the arguments of this invocation
            List<String> expectedAttributes = null;
            if (identifierName != null && AnnotationDictionary.ContainsKey(identifierName))
            {
                expectedAttributes = AnnotationDictionary[identifierName].FirstOrDefault();
            }
            else
            {
                //There is nothing to verify, or we need to introduce the default annotation
                //Should we return here in the case of nothing to verify?
                Debug.WriteLine("no attributes to verify");
                return;
            }

            // We have found an attribute, so now we verify the RHS
            var invocationExpr = assignmentExpression.Right as InvocationExpressionSyntax;
            if (invocationExpr != null)
            {
                var returnTypeAttrs = new List<String>();

                var identifierNameExpr = invocationExpr.Expression as IdentifierNameSyntax;
                if (identifierNameExpr != null)
                {
                    if (AnnotationDictionary.ContainsKey(identifierNameExpr))
                    {
                        returnTypeAttrs = AnnotationDictionary[identifierNameExpr].FirstOrDefault();
                    }
                }
                else
                {
                    //If we don't have a local method invocation, we may have a static or instance method invocation
                    var memberAccessExpr = invocationExpr.Expression as MemberAccessExpressionSyntax;
                    if (memberAccessExpr != null)
                    {
                        if (AnnotationDictionary.ContainsKey(memberAccessExpr))
                        {
                            returnTypeAttrs = AnnotationDictionary[memberAccessExpr].FirstOrDefault();
                        }
                    }
                }

                // Now we check the return type to see if there is an attribute assigned
                foreach (var retAttr in returnTypeAttrs)
                {
                    if (expectedAttributes.Contains(retAttr))
                    {
                        expectedAttributes.Remove(retAttr);
                    }
                }

                //If we haven't found a match then present a diagnotic error
                if (expectedAttributes.Count() > 0)
                {
                    var diagnostic = Diagnostic.Create(rule, invocationExpr.GetLocation(), attributeName);
                    context.ReportDiagnostic(diagnostic);
                }

            }
            else
            {
                var rhsLit = assignmentExpression.Right as LiteralExpressionSyntax;
                if (rhsLit != null)
                {
                    var diagnostic = Diagnostic.Create(rule, rhsLit.GetLocation(), attributeName);
                    context.ReportDiagnostic(diagnostic);
                }
                else
                {
                    var diagnostic = Diagnostic.Create(rule, assignmentExpression.GetLocation(), "Not implemented");
                    context.ReportDiagnostic(diagnostic);
                    //TODO: We should pull out methods such as this so that they can be used in several areas of the code
                    //var fieldSymbol = context.SemanticModel.GetSymbolInfo(assignmentExpression.Right).Symbol as IFieldSymbol;
                    //if (fieldSymbol != null)
                    //{
                    //    VerifyAttribute(context, assignmentExpression.Right, fieldSymbol, argAttrs, rule, description);
                    //}
                    //TODO: Add property symbols here
                }
            }
        }



        /// <summary>
        /// If we are invoking a method which has attributes on its formal parameters, then we need to verify that
        /// the arguments passed abide by these annotations
        /// </summary>
        private void VerifyInvocationExpr(InvocationExpressionSyntax invocationExpr)
        {
            var identifierNameExpr = invocationExpr.Expression as IdentifierNameSyntax;
            //We may need to recursively dig into the expression if the top level doesn't hand over a identifier
            //TODO: Present a diagnostic instead of failing silently
            if (identifierNameExpr == null) return;

            //Get the expected attributes for the arguments of this invocation
            var argList = invocationExpr.ArgumentList;
            List<List<String>> expectedAttribute = null;
            if (AnnotationDictionary.ContainsKey(argList))
            {
                expectedAttribute = AnnotationDictionary[argList];
            }
            else
            {
                //There is nothing to verify, or we need to introduce the default annotation
                //Should we return here in the case of nothing to verify?
                Debug.WriteLine("no attributes to verify");
                return;
            }

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
                    if (argI != null)
                    {
                        List<String> argAttrs = new List<string>();

                        if (AnnotationDictionary.ContainsKey(argI))
                        {
                            argAttrs = AnnotationDictionary[argI].FirstOrDefault();
                        }

                        foreach (var argAttr in argAttrs)
                        {
                            if (expectedAttribute[i].Contains(argAttr))
                            {
                                //We may want to create a deep copy of this object instead of
                                //removing them directly from the source collection, as it stands only
                                //making one verification pass, this should be ok
                                expectedAttribute[i].Remove(argAttr);
                            }
                        }

                        //If we haven't found a match then present a diagnotic error
                        if (expectedAttribute[i].Count() > 0)
                        {
                            var diagnostic = Diagnostic.Create(rule, argI.GetLocation(), attributeName);
                            context.ReportDiagnostic(diagnostic);
                        }
                    }
                    else
                    {
                        //We are probably dealing with a literal - which cannot have the associated attribute
                        var argLit = argumentList.Arguments[i].Expression as LiteralExpressionSyntax;
                        if (argLit != null)
                        {
                            var diagnostic = Diagnostic.Create(rule, argLit.GetLocation(), attributeName);
                            context.ReportDiagnostic(diagnostic);
                        }
                        else
                        {
                            List<String> returnTypeAttrs = null;
                            var argInvExpr = argumentList.Arguments[i].Expression as InvocationExpressionSyntax;
                            if (argInvExpr != null)
                            {
                                //If we have a local method invocation
                                var methodIdNameExpr = argInvExpr.Expression as IdentifierNameSyntax;
                                if (methodIdNameExpr != null)
                                {
                                    if (AnnotationDictionary.ContainsKey(methodIdNameExpr))
                                    {
                                        returnTypeAttrs = AnnotationDictionary[methodIdNameExpr].FirstOrDefault();
                                    }
                                }
                                else
                                {
                                    //If we don't have a local method invocation, we may have a static or instance method invocation
                                    var memberAccessExpr = argInvExpr.Expression as MemberAccessExpressionSyntax;
                                    if (memberAccessExpr != null)
                                    {
                                        if (AnnotationDictionary.ContainsKey(memberAccessExpr))
                                        {
                                            returnTypeAttrs = AnnotationDictionary[memberAccessExpr].FirstOrDefault();
                                        }
                                    }
                                }
                            }

                            if (returnTypeAttrs != null)
                            {
                                // Now we check the return type to see if there is an attribute assigned
                                foreach (var retAttr in returnTypeAttrs)
                                {
                                    if (expectedAttribute[i].Contains(retAttr))
                                    {
                                        expectedAttribute[i].Remove(retAttr);
                                    }
                                }

                                //If we haven't found a match then present a diagnotic error
                                if (expectedAttribute[i].Count() > 0)
                                {
                                    var diagnostic = Diagnostic.Create(rule, argumentList.Arguments[i].Expression.GetLocation(), attributeName);
                                    context.ReportDiagnostic(diagnostic);
                                }
                            }
                            else //We may be dealing with a field like String.Empty
                            {
                                returnTypeAttrs = new List<string>();
                                var expr = argumentList.Arguments[i].Expression;
                                if (AnnotationDictionary.ContainsKey(expr))
                                {
                                    returnTypeAttrs = AnnotationDictionary[expr].FirstOrDefault();
                                }

                                foreach (var retAttr in returnTypeAttrs)
                                {
                                    if (expectedAttribute[i].Contains(retAttr))
                                    {
                                        expectedAttribute[i].Remove(retAttr);
                                    }
                                }

                                //If we haven't found a match then present a diagnotic error
                                if (expectedAttribute[i].Count() > 0)
                                {
                                    var diagnostic = Diagnostic.Create(rule, argumentList.Arguments[i].Expression.GetLocation(), attributeName);
                                    context.ReportDiagnostic(diagnostic);
                                }

                                //var fieldSymbol = context.SemanticModel.GetSymbolInfo(argumentList.Arguments[i].Expression).Symbol as IFieldSymbol;
                                //if (fieldSymbol != null)
                                //{
                                //    var fieldAttrs = fieldSymbol.GetAttributes();
                                //    foreach (var fieldAttr in fieldAttrs)
                                //    {
                                //        if (expectedAttribute[i].Contains(fieldAttr.AttributeClass.ToString()))
                                //        {
                                //            //TODO: Need mechanism to determine which attributes we care about so
                                //            //that additional ones which are present do not throw off our analysis
                                //            //and present warnings when they should not
                                //            expectedAttribute[i].Remove(fieldAttr.AttributeClass.ToString());
                                //        }
                                //    }

                                //    //If we haven't found a match then present a diagnotic error
                                //    if (expectedAttribute[i].Count() > 0)
                                //    {
                                //        var diagnostic = Diagnostic.Create(rule, argumentList.Arguments[i].Expression.GetLocation(), description);
                                //        context.ReportDiagnostic(diagnostic);
                                //    }
                                //}
                                //else
                                //{
                                //    var propertySymbol = context.SemanticModel.GetSymbolInfo(argumentList.Arguments[i].Expression).Symbol as IPropertySymbol;
                                //    if (propertySymbol != null)
                                //    {
                                //        var propAttrs = fieldSymbol.GetAttributes();
                                //        foreach (var propAttr in propAttrs)
                                //        {
                                //            if (expectedAttribute[i].Contains(propAttr.AttributeClass.ToString()))
                                //            {
                                //                //TODO: Need mechanism to determine which attributes we care about so
                                //                //that additional ones which are present do not throw off our analysis
                                //                //and present warnings when they should not
                                //                expectedAttribute[i].Remove(propAttr.AttributeClass.ToString());
                                //            }
                                //        }

                                //        //If we haven't found a match then present a diagnotic error
                                //        if (expectedAttribute[i].Count() > 0)
                                //        {
                                //            var diagnostic = Diagnostic.Create(rule, argumentList.Arguments[i].Expression.GetLocation(), description);
                                //            context.ReportDiagnostic(diagnostic);
                                //        }
                                //    }
                                //    else
                                //    {
                                //        var diagnostic = Diagnostic.Create(rule, argumentList.Arguments[i].Expression.GetLocation(), nameof(AttributeType.NotImplemented));
                                //        context.ReportDiagnostic(diagnostic);
                                //    }
                                //}
                            }
                        }
                    }
                }
            }
        }
    }
}
