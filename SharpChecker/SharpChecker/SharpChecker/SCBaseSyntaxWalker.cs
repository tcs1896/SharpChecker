using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

namespace SharpChecker
{
    class SCBaseSyntaxWalker : CSharpSyntaxWalker
    {
        private Dictionary<string, DiagnosticDescriptor> rulesDict;
        private ConcurrentDictionary<SyntaxNode, List<List<String>>> AnnotationDictionary;
        private SemanticModelAnalysisContext context;
        private List<string> sharpCheckerAttributes;

        public SCBaseSyntaxWalker(Dictionary<string, DiagnosticDescriptor> rulesDict, ConcurrentDictionary<SyntaxNode, List<List<String>>> annotationDictionary, SemanticModelAnalysisContext context, List<string> SharpCheckerAttributes)
        {
            this.rulesDict = rulesDict;
            this.AnnotationDictionary = annotationDictionary;
            this.context = context;
            this.sharpCheckerAttributes = SharpCheckerAttributes;
        }

        /// <summary>
        /// This is invoked for nodes of all types followed by the more specific Visit
        /// methods such as VisitInvocationExpression
        /// </summary>
        /// <param name="node"></param>
        public override void Visit(SyntaxNode node)
        {
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

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            VerifyMethodDecl(node);
            base.VisitMethodDeclaration(node);
        }

        /// <summary>
        /// If a method declaration overrides a declaration in a base class which has attributes
        /// associated with the parameters or the return type, then we need to make sure that the
        /// overriding method does not allow circumvention of those attributes
        /// </summary>
        /// <param name="methodDecl"></param>
        private void VerifyMethodDecl(MethodDeclarationSyntax methodDecl)
        {
            //This will lookup the method associated with the invocation expression
            var childMethodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDecl);
            //If we failed to lookup the symbol then bail
            if (childMethodSymbol == null)
            {
                return;
            }

            //First check to see if this is a method which overrides a virtual method
            var overriddenMethod = childMethodSymbol.OverriddenMethod;
            if (overriddenMethod != null)
            {
                // Make sure the return type is subtype of the parent
                var returnTypeAttrs = overriddenMethod.GetReturnTypeAttributes();
                List<string> returnTypeAttrStrings = GetSharpCheckerAttributeStrings(returnTypeAttrs);

                var derivedReturnTypeAttrs = childMethodSymbol.GetReturnTypeAttributes();

                foreach (var derTypeAttr in derivedReturnTypeAttrs)
                {
                    string derTypeAttrString = derTypeAttr.AttributeClass.MetadataName.ToString();
                    derTypeAttrString = derTypeAttrString.EndsWith("Attribute") ? derTypeAttrString.Replace("Attribute", "") : derTypeAttrString;

                    if (returnTypeAttrStrings.Contains(derTypeAttrString))
                    {
                        returnTypeAttrStrings.Remove(derTypeAttrString);
                    }
                }

                ReportDiagsForEach(methodDecl.Identifier.GetLocation(), returnTypeAttrStrings);

                //Now check to see if the attributes of the parameters agree with the overriden method
                var derivedMethParams = methodDecl.ParameterList.Parameters;
                if (derivedMethParams.Count() == 0) { return; } //No params to check

                if (overriddenMethod.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is MethodDeclarationSyntax overriddenSyntax)
                {
                    var overriddenParams = overriddenSyntax.ParameterList.Parameters;

                    //Check each param in turn
                    for (int i = 0; i < overriddenParams.Count(); i++)
                    {
                        //Get the attributes for this parameter of the overriden method
                        var param = overriddenParams[i];
                        var attrs = param.AttributeLists.AsEnumerable();
                        List<string> stringAttrs = new List<string>();
                        foreach (var paramAttr in attrs)
                        {
                            var finalAttrs = paramAttr.Attributes;
                            foreach (var fa in finalAttrs)
                            {
                                var faName = fa.Name.ToString();
                                if (sharpCheckerAttributes.Contains(faName))
                                {
                                    stringAttrs.Add(faName);
                                }
                            }
                        }

                        //Get the attributes of the same param for the derived method
                        var derParam = derivedMethParams[i];
                        var derAttrs = derParam.AttributeLists.AsEnumerable();
                        foreach (var derParamAttr in derAttrs)
                        {
                            var innerAttr = derParamAttr.Attributes;
                            foreach (var ia in innerAttr)
                            {
                                if (stringAttrs.Contains(ia.Name.ToString()))
                                {
                                    stringAttrs.Remove(ia.Name.ToString());
                                }
                            }
                        }

                        ReportDiagsForEach(derivedMethParams[i].GetLocation(), stringAttrs);
                    }
                }
            }
        }

        /// <summary>
        /// Report one diagnostic foreach attribute which is present when it should have 
        /// been paired off with a matching occurance and removed
        /// </summary>
        /// <param name="location"></param>
        /// <param name="errorAttributes"></param>
        private void ReportDiagsForEach(Location location, List<string> errorAttributes)
        {
            if(errorAttributes == null || errorAttributes.Count() == 0) { return; }

            foreach (var errorAttr in errorAttributes)
            {
                var diagnostic = Diagnostic.Create(rulesDict[errorAttr], location, errorAttr);
                context.ReportDiagnostic(diagnostic);
            }
        }

        /// <summary>
        /// Accepts a collection of attributes and filters them down to those which were discovered
        /// to have the [SharpChecker] attribute
        /// </summary>
        /// <param name="returnTypeAttrs"></param>
        /// <returns></returns>
        private List<String> GetSharpCheckerAttributeStrings(ImmutableArray<AttributeData> returnTypeAttrs)
        {
            var retAttrStrings = new List<String>();
            foreach (var attData in returnTypeAttrs)
            {
                //See if we have previosly recorded this as a attribute we are interested in
                string att = attData.AttributeClass.MetadataName;
                att = att.EndsWith("Attribute") ? att.Replace("Attribute", "") : att;

                if (sharpCheckerAttributes.Contains(att))
                {
                    retAttrStrings.Add(att);
                }
            }

            return retAttrStrings;
        }

        /// <summary>
        /// If the variable to which we are assigning a value has an annotation, then we need to verify that the
        /// expression to which it is assigned with yeild a value with the appropriate annotation
        /// </summary>
        private void VerifyAssignmentExpr(AssignmentExpressionSyntax assignmentExpression)
        {
            List<String> expectedAttributes = null;
            // First check the variable to which we are assigning
            if (assignmentExpression.Left is IdentifierNameSyntax identifierName)
            {
                if (AnnotationDictionary.ContainsKey(identifierName))
                {
                    expectedAttributes = AnnotationDictionary[identifierName].FirstOrDefault();
                }
            }
            else if (assignmentExpression.Left is MemberAccessExpressionSyntax memAccess)
            {
                if (AnnotationDictionary.ContainsKey(memAccess))
                {
                    expectedAttributes = AnnotationDictionary[memAccess].FirstOrDefault();
                }
            }

            if(expectedAttributes == null || expectedAttributes.Count() == 0)
            {
                //There is nothing to verify, or we need to introduce the default annotation
                //Should we return here in the case of nothing to verify?
                Debug.WriteLine("no attributes to verify");
                return;
            }

            var returnTypeAttrs = new List<String>();

            switch (assignmentExpression.Right.Kind())
            {
                case SyntaxKind.InvocationExpression:
                    if (assignmentExpression.Right is InvocationExpressionSyntax invocationExpr
                        && AnnotationDictionary.ContainsKey(invocationExpr.Expression))
                    {
                        returnTypeAttrs = AnnotationDictionary[invocationExpr.Expression].FirstOrDefault();
                    }
                    break;
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
            ReportDiagsForEach(assignmentExpression.Right.GetLocation(), expectedAttributes);
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
            List<List<String>> expectedAttributes = null;
            if (AnnotationDictionary.ContainsKey(argList))
            {
                expectedAttributes = AnnotationDictionary[argList];
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

            for (int i = 0; i < expectedAttributes.Count; i++)
            {
                //If there are no required attributes for this argument, then move on to the next
                if (expectedAttributes[i].Count == 0) continue;

                //It may seem unnecessary to check that i is valid, since providing the incorrect number of arguments should not
                //type check.  However, there is some candidate analysis while the code is incomplete.
                if (i < argumentList.Arguments.Count())
                {
                    VerifyExpectedAttrInExpression(expectedAttributes[i], argumentList.Arguments[i].Expression);
                }
            }
        }

        /// <summary>
        /// Used to confirm that the expression in node has the expected attributes
        /// </summary>
        /// <param name="expectedAttributes">A collection of expected attributes</param>
        /// <param name="node">The node which is being analyzed</param>
        private void VerifyExpectedAttrInExpression(List<string> expectedAttributes, SyntaxNode node)
        {
            //Need to make a local copy the expected attributes incase we recurse and use the same original
            //for multiple branches
            List<string> expectedAttr = new List<string>(expectedAttributes);

            //Here we are handling the case where the argument is an identifier
            if (node is IdentifierNameSyntax argI)
            {
                List<String> argAttrs = new List<string>();

                if (AnnotationDictionary.ContainsKey(argI))
                {
                    argAttrs = AnnotationDictionary[argI].FirstOrDefault();
                }

                foreach (var argAttr in argAttrs)
                {
                    if (expectedAttr.Contains(argAttr))
                    {
                        expectedAttr.Remove(argAttr);
                    }
                }

                //If we haven't found a match then present a diagnotic error
                ReportDiagsForEach(argI.GetLocation(), expectedAttr);
            }
            else if (node is ConditionalExpressionSyntax conditional)
            {
                //Verify each branch of a ternary conditional expression
                VerifyExpectedAttrInExpression(expectedAttr, conditional.WhenTrue);
                VerifyExpectedAttrInExpression(expectedAttr, conditional.WhenFalse);
            }
            else
            {
                //We are probably dealing with a literal - which cannot have the associated attribute
                if (node is LiteralExpressionSyntax argLit)
                {
                    ReportDiagsForEach(argLit.GetLocation(), expectedAttr);
                }
                else
                {
                    List<String> returnTypeAttrs = null;
                    if (node is InvocationExpressionSyntax argInvExpr)
                    {
                        //If we have a local method invocation
                        if (argInvExpr.Expression is IdentifierNameSyntax methodIdNameExpr)
                        {
                            if (AnnotationDictionary.ContainsKey(methodIdNameExpr))
                            {
                                returnTypeAttrs = AnnotationDictionary[methodIdNameExpr].FirstOrDefault();
                            }
                        }
                        else
                        {
                            //If we don't have a local method invocation, we may have a static or instance method invocation
                            if (argInvExpr.Expression is MemberAccessExpressionSyntax memberAccessExpr)
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
                            if (expectedAttr.Contains(retAttr))
                            {
                                expectedAttr.Remove(retAttr);
                            }
                        }

                        //If we haven't found a match then present a diagnotic error
                        ReportDiagsForEach(node.GetLocation(), expectedAttr);
                    }
                    else //We may be dealing with a field like String.Empty
                    {
                        returnTypeAttrs = new List<string>();
                        if (AnnotationDictionary.ContainsKey(node))
                        {
                            returnTypeAttrs = AnnotationDictionary[node].FirstOrDefault();
                        }

                        foreach (var retAttr in returnTypeAttrs)
                        {
                            if (expectedAttr.Contains(retAttr))
                            {
                                expectedAttr.Remove(retAttr);
                            }
                        }

                        //If we haven't found a match then present a diagnotic error
                        ReportDiagsForEach(node.GetLocation(), expectedAttr);

                        //var fieldSymbol = context.SemanticModel.GetSymbolInfo(argumentList.Arguments[i].Expression).Symbol as IFieldSymbol;
                        //if (fieldSymbol != null)
                        //{
                        //    var fieldAttrs = fieldSymbol.GetAttributes();
                        //    foreach (var fieldAttr in fieldAttrs)
                        //    {
                        //        if (expectedAttribute[i].Contains(fieldAttr.AttributeClass.ToString()))
                        //        {
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
