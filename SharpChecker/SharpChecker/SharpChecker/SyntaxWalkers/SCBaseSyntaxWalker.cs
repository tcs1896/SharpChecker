using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SharpChecker.Attributes;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using static SharpChecker.Enums;

namespace SharpChecker
{
    class SCBaseSyntaxWalker : CSharpSyntaxWalker
    {
        internal Dictionary<string, DiagnosticDescriptor> rulesDict;
        internal ConcurrentDictionary<SyntaxNode, List<List<String>>> AnnotationDictionary;
        internal SemanticModelAnalysisContext context;
        internal List<Node> attributesOfInterest;

        public SCBaseSyntaxWalker(Dictionary<string, DiagnosticDescriptor> rulesDict, ConcurrentDictionary<SyntaxNode, List<List<String>>> annotationDictionary, SemanticModelAnalysisContext context, List<Node> attributesOfInterest)
        {
            this.rulesDict = rulesDict;
            this.AnnotationDictionary = annotationDictionary;
            this.context = context;
            this.attributesOfInterest = attributesOfInterest;
        }

        /// <summary>
        /// This is invoked for nodes of all types followed by the more specific Visit
        /// methods such as VisitInvocationExpression
        /// </summary>
        /// <param name="node"></param>
        //public override void Visit(SyntaxNode node)
        //{
        //    base.Visit(node);
        //}

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

        public override void VisitReturnStatement(ReturnStatementSyntax node)
        {
            VerifyReturnStmt(node);
            base.VisitReturnStatement(node);
        }

        /// <summary>
        /// If a method has a return attribute, then we need to verify that any return statements
        /// appearing in the body of that function have the appropriate attribute
        /// </summary>
        /// <param name="node">The return statement syntax node</param>
        private void VerifyReturnStmt(ReturnStatementSyntax node)
        {
            //Determine the expected return attributes of this method
            SyntaxNode parent = node.Parent;
            while(!(parent is MethodDeclarationSyntax))
            {
                parent = parent.Parent;
            }
            var expectedAttrs = new List<string>();
            if (parent is MethodDeclarationSyntax methodDef)
            {
                var methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDef);
                expectedAttrs = GetSharpCheckerAttributeStrings(methodSymbol.GetReturnTypeAttributes());
            }

            //Verify the expression being returned has the appropriate annotation
            if (node.Expression != null)
            {
                VerifyExpectedAttrsInSyntaxNode(expectedAttrs, node.Expression); 
            }
        }

        /// <summary>
        /// If a method declaration overrides a declaration in a base class which has attributes
        /// associated with the parameters or the return type, then we need to make sure that the
        /// overriding method does not allow circumvention of those attributes
        /// </summary>
        /// <param name="methodDecl"></param>
        internal virtual void VerifyMethodDecl(MethodDeclarationSyntax methodDecl)
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
                List<string> actualAttrs = new List<string>();
                foreach (var derTypeAttr in derivedReturnTypeAttrs)
                {
                    string derTypeAttrString = derTypeAttr.AttributeClass.MetadataName.ToString();
                    derTypeAttrString = derTypeAttrString.EndsWith("Attribute") ? derTypeAttrString.Replace("Attribute", "") : derTypeAttrString;
                    actualAttrs.Add(derTypeAttrString);
                }

                ReportDiagsForEach(methodDecl.Identifier.GetLocation(), returnTypeAttrStrings, actualAttrs);

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
                                if (attributesOfInterest.Any(nod => nod.AttributeName == faName))
                                {
                                    stringAttrs.Add(faName);
                                }
                            }
                        }

                        //Get the attributes of the same param for the derived method
                        var derParam = derivedMethParams[i];
                        var derAttrs = derParam.AttributeLists.AsEnumerable();
                        var actuals = new List<string>();
                        foreach (var derParamAttr in derAttrs)
                        {
                            var innerAttr = derParamAttr.Attributes;
                            foreach (var ia in innerAttr)
                            {
                                actuals.Add(ia.Name.ToString());
                            }
                        }

                        ReportDiagsForEach(derivedMethParams[i].GetLocation(), stringAttrs, actuals);
                    }
                }
            }
        }

        /// <summary>
        /// Report one diagnostic foreach attribute which is present when it should have 
        /// been paired off with a matching occurance and removed
        /// </summary>
        /// <param name="location"></param>
        /// <param name="expectedAttributes"></param>
        internal void ReportDiagsForEach(Location location, List<string> expectedAttributes, List<string> actualAttributes)
        {
            if(expectedAttributes == null || expectedAttributes.Count() == 0) { return; }

            // Now we check the return type to see if there is an attribute assigned
            if (actualAttributes != null)
            {
                foreach (var actAttr in actualAttributes)
                {
                    var actualNode = attributesOfInterest.Where(nod => nod.AttributeName == actAttr).FirstOrDefault();
                    if (!String.IsNullOrWhiteSpace(actualNode.AttributeName))
                    {
                        RemoveAllInHierarchy(expectedAttributes, actualNode);
                    }
                }
            }

            foreach (var errorAttr in expectedAttributes)
            {
                if (rulesDict.ContainsKey(errorAttr))
                {
                    var diagnostic = Diagnostic.Create(rulesDict[errorAttr], location, errorAttr);
                    context.ReportDiagnostic(diagnostic);
                }
                else
                {
                    var diagnostic = Diagnostic.Create(rulesDict[AttributeType.NotImplemented.ToString()], location, errorAttr);
                    context.ReportDiagnostic(diagnostic);
                }
                
            }
        }

        /// <summary>
        /// Walk up the supertype hiearachy (DAG) removing all expected attributes which are satisfied
        /// both those which are present
        /// </summary>
        /// <param name="expectedAttributes">The collection of expected attributes which is modified in place</param>
        /// <param name="actualNode">The actual attribute with its supertypes</param>
        private static void RemoveAllInHierarchy(List<string> expectedAttributes, Node actualNode)
        {
            if (expectedAttributes.Contains(actualNode.AttributeName))
            {
                expectedAttributes.Remove(actualNode.AttributeName);
            }

            var supertypes = actualNode.Supertypes;
            if (supertypes != null && supertypes.Count() > 0)
            {
                foreach (var sup in supertypes)
                {
                    if (expectedAttributes.Contains(sup.AttributeName))
                    {
                        expectedAttributes.Remove(sup.AttributeName);
                    }

                    //Continue to walk up the chain
                    RemoveAllInHierarchy(expectedAttributes, sup);
                }
            }
        }


        /// <summary>
        /// Accepts a collection of attributes and filters them down to those which were registered for analysis
        /// </summary>
        /// <param name="attrDataCollection"></param>
        /// <returns></returns>
        internal List<String> GetSharpCheckerAttributeStrings(ImmutableArray<AttributeData> attrDataCollection)
        {
            var attrStrings = new List<String>();
            foreach (var attData in attrDataCollection)
            {
                //See if we have previosly recorded this as a attribute we are interested in
                string att = attData.AttributeClass.MetadataName;
                att = att.EndsWith("Attribute") ? att.Replace("Attribute", "") : att;

                if (attributesOfInterest.Any(nod => nod.AttributeName == att))
                {
                    attrStrings.Add(att);
                }
            }

            return attrStrings;
        }

        /// <summary>
        /// If the variable to which we are assigning a value has an annotation, then we need to verify that the
        /// expression to which it is assigned with yeild a value with the appropriate annotation
        /// </summary>
        internal virtual void VerifyAssignmentExpr(AssignmentExpressionSyntax assignmentExpression)
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
                case SyntaxKind.StringLiteralExpression:
                case SyntaxKind.StringLiteralToken:
                    var strDefault = GetDefaultForStringLiteral();
                    if(!string.IsNullOrWhiteSpace(strDefault))
                    {
                        returnTypeAttrs = new List<string>() { strDefault };
                    }
                    break;
                case SyntaxKind.NullLiteralExpression:
                case SyntaxKind.NullKeyword:
                    var nullDefault = GetDefaultForNullLiteral();
                    if (!string.IsNullOrWhiteSpace(nullDefault))
                    {
                        returnTypeAttrs = new List<string>() { nullDefault };
                    }
                    break;
                case SyntaxKind.IdentifierName:
                    if(assignmentExpression.Right is IdentifierNameSyntax identifier && AnnotationDictionary.Keys.Contains(identifier))
                    {
                        returnTypeAttrs = AnnotationDictionary[identifier]?.FirstOrDefault();
                    }
                    break;
            }

            //If we haven't found a match then present a diagnotic error
            ReportDiagsForEach(assignmentExpression.Right.GetLocation(), expectedAttributes, returnTypeAttrs);
        }



        /// <summary>
        /// If we are invoking a method which has attributes on its formal parameters, then we need to verify that
        /// the arguments passed abide by these annotations
        /// </summary>
        internal virtual void VerifyInvocationExpr(InvocationExpressionSyntax invocationExpr)
        {
            var identifierNameExpr = invocationExpr.Expression as IdentifierNameSyntax;
            if (identifierNameExpr == null)
            {
                var memAccess = invocationExpr.Expression as MemberAccessExpressionSyntax;
                if(memAccess == null)
                {
                    ReportDiagsForEach(invocationExpr.GetLocation(), new List<string>() { AttributeType.NotImplemented.ToString() }, null);
                    return;
                }
                else
                {
                    //Check to see if this is an assertion
                    if (memAccess?.Name.ToString() == "Assert")
                    {
                        RefineTypesBasedOnAssertion(invocationExpr, memAccess);
                    }
                }
            }

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
                    if (argumentList.Arguments[i].Expression != null)
                    {
                        VerifyExpectedAttrsInSyntaxNode(expectedAttributes[i], argumentList.Arguments[i].Expression);
                    }
                }
            }
        }

        /// <summary>
        /// When we encounter a Debug.Assert statement we parse the second argument looking for an identifier:attribute pair.
        /// When we find a match for this pattern we refine the type of that identifier to be the attribute in the context
        /// in which the Assert occurs.
        /// </summary>
        /// <param name="invocationExpr"></param>
        /// <param name="memAccess"></param>
        private void RefineTypesBasedOnAssertion(InvocationExpressionSyntax invocationExpr, MemberAccessExpressionSyntax memAccess)
        {
            var memberSymbol = context.SemanticModel.GetSymbolInfo(memAccess).Symbol as IMethodSymbol;
            //If we are dealing with the correct namespace then bail
            if (!memberSymbol?.ToString().StartsWith("System.Diagnostics.Debug.Assert") ?? true) return;
            //If we make it this far we know we have an assertion
            //Grab the argument which may contain additional information for SharpChecker
            string variable = string.Empty;
            string attribute = string.Empty;
            if(invocationExpr.ArgumentList.Arguments.Count() > 1)
            {
                var refineStmt = invocationExpr.ArgumentList.Arguments[1].Expression as LiteralExpressionSyntax;

                if (refineStmt == null) return;
                //Now that we know its a literal we can retrieve the value
                var patternOpt = context.SemanticModel.GetConstantValue(refineStmt);
                if (!patternOpt.HasValue) return;
                var pattern = patternOpt.Value as string;
                if (pattern == null) return;

                var splitAssertion = pattern.Split(':');
                if(splitAssertion.Length > 1)
                {
                    variable = splitAssertion[0];
                    attribute = splitAssertion[1];
                }

                if (!String.IsNullOrWhiteSpace(variable) && !String.IsNullOrWhiteSpace(attribute))
                {
                    //Backtrack to the context of the Debug.Assert method invocation then search
                    //for any references to the variable which is being refined by the assertion
                    var invocationContext = invocationExpr.Parent;
                    while(invocationContext.Kind() != SyntaxKind.Block)
                    {
                        invocationContext = invocationContext.Parent;
                    }

                    var blockContext = invocationContext as BlockSyntax;
                    foreach (var stmt in blockContext.Statements)
                    {
                        IEnumerable<SyntaxNode> allOccurances = stmt.DescendantNodes()
                                            .OfType<IdentifierNameSyntax>()
                                            .Where(idns => idns.Identifier.Text == variable);
                        //If we don't find any identifiers which match, then look for Memeber Access expressions
                        if(allOccurances.Count() == 0)
                        {
                            allOccurances = stmt.DescendantNodes()
                                            .OfType<MemberAccessExpressionSyntax>()
                                            .Where(maes => maes.ToString() == variable);
                        }

                        //Modify the attribute associated with the syntax node
                        foreach (var occur in allOccurances)
                        {
                            if (AnnotationDictionary.ContainsKey(occur))
                            {
                                //TODO: We should really be replacing the appropriate attribute with the new one instead of
                                //replacing all attributes.  The correct one is the one in the attribute hierarchy of the new one.
                                AnnotationDictionary[occur] = new List<List<string>>() { new List<string>() { attribute } };
                                //ReplaceAttributeList(occur, attribute);
                            }
                            else
                            {
                                AnnotationDictionary.TryAdd(occur, new List<List<string>>() { new List<string>() { attribute } });
                            }
                        }
                    }
                }
            }
        }

        public void ReplaceAttributeList(SyntaxNode synNode, string attribute)
        {

            AnnotationDictionary[synNode] = new List<List<string>>() { new List<string>() { attribute } };
            //List<string> toUpdate = AnnotationDictionary[synNode][0];
            //foreach (string attr in toUpdate)
            //{
            //    foreach (var interest in attributesOfInterest)
            //    {
            //        bool isInHierarchy = IsInHeirarchy(interest);
            //    }
            //}

            //Node toReplace;
            //foreach(var attr in attributesOfInterest)
            //{
            //    if(attr.AttributeName == attribute)
            //    {

            //    }
            //}
        }

        //private bool IsInHeirarchy(Node interest)
        //{
        //    if(interest.AttributeName)
        //}

        /// <summary>
        /// Used to confirm that the expression in node has the expected attributes
        /// </summary>
        /// <param name="expectedAttributes">A collection of expected attributes</param>
        /// <param name="node">The node which is being analyzed</param>
        internal virtual void VerifyExpectedAttrsInSyntaxNode(List<string> expectedAttributes, [NonNull] SyntaxNode node)
        {
            //If there are no expected attributes, or there is no node to analyze then bail
            if(expectedAttributes == null || expectedAttributes.Count() == 0 || node == null) { return; }

            //Need to make a local copy the expected attributes in case we recurse and use the same original
            //collection of expected attributes for multiple branches
            List<string> expectedAttr = new List<string>(expectedAttributes);

            //Here we are handling the case where the argument is an identifier
            if (node is IdentifierNameSyntax argI)
            {
                List<String> argAttrs = new List<string>();

                if (AnnotationDictionary.ContainsKey(argI))
                {
                    argAttrs = AnnotationDictionary[argI].FirstOrDefault();
                }

                //If we haven't found a match then present a diagnotic error
                ReportDiagsForEach(argI.GetLocation(), expectedAttr, argAttrs);
            }
            else if (node is ConditionalExpressionSyntax conditional)
            {
                //Verify each branch of a ternary conditional expression
                Debug.Assert(conditional.WhenTrue != null, "conditional.WhenTrue:NonNull");
                VerifyExpectedAttrsInSyntaxNode(expectedAttr, conditional.WhenTrue);
                if (conditional.WhenFalse != null)
                {
                    VerifyExpectedAttrsInSyntaxNode(expectedAttr, conditional.WhenFalse);
                }
            }
            else if (node is LiteralExpressionSyntax argLit)
            {
                //We are dealing with a literal - which cannot have the associated attribute
                //However, we probably need to consider the default here.  For the nullness type
                //system we may want to implicitly assign [NonNull] to literal strings
                string defaultAttr = string.Empty;
                if (node.RawKind == (int)SyntaxKind.NullLiteralExpression)
                {
                    defaultAttr = GetDefaultForNullLiteral();
                }
                else
                {
                    //Get the default attibute for a literal string
                    defaultAttr = GetDefaultForStringLiteral();
                }

                ReportDiagsForEach(argLit.GetLocation(), expectedAttr, new List<string>() { defaultAttr });
            }
            else if (node is InvocationExpressionSyntax argInvExpr)
            {
                List<String> returnTypeAttrs = new List<string>();

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

                //If we haven't found a match then present a diagnotic error
                ReportDiagsForEach(node.GetLocation(), expectedAttr, returnTypeAttrs);
            }
            else
            {
                //We may be dealing with a field like String.Empty
                List<String> returnTypeAttrs = new List<string>();
                if (AnnotationDictionary.ContainsKey(node))
                {
                    returnTypeAttrs = AnnotationDictionary[node].FirstOrDefault();
                }

                //If we haven't found a match then present a diagnotic error
                ReportDiagsForEach(node.GetLocation(), expectedAttr, returnTypeAttrs);
            }
        }

        /// <summary>
        /// This should be overridden when a default attribute should be applied to string literal expressions
        /// </summary>
        /// <returns>The attribute inferred for string literals</returns>
        internal virtual string GetDefaultForStringLiteral()
        {
            return null;
        }

        /// <summary>
        /// This should be overridden when a default attribute should be applied to null literal expressions
        /// </summary>
        /// <returns>The attribute inferred for null literals</returns>
        internal virtual string GetDefaultForNullLiteral()
        {
            return null;
        }
    }
}
