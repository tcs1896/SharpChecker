using System;
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

        SyntaxAnnotation syntaxAnnotation = new SyntaxAnnotation("Parameter");

        //We may want to put these somewhere else in the future
        private DiagnosticDescriptor Rule;
        private string attributeName;
        private SyntaxNode docRoot;

        public ASTUtilities(DiagnosticDescriptor Rule, string attributeName)
        {
            this.Rule = Rule;
            this.attributeName = attributeName;
            //We may want to search for attribute definitions which are decorated with something like 
            //[SharpChecker] then include these attributes in our analysis...
            //
            //[SharpChecker]
            //[AttributeUsage(AttributeTargets.All, Inherited = true, AllowMultiple = true)]
            //class EncryptedAttribute : Attribute
            //{}
        }

        public void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            //Perhaps we can take advantage of dynamic dispatch keeping the structure of the method below,
            //but passing an instance of a particular "SharpCheckerAnalyzer" which will know how to analyze itself
            var attrs = this.GetAttributes(context, context.Node);

            switch (attrs.Item1)
            {
                case ASTUtilities.AttributeType.HasAnnotation:
                    this.VerifyAttributes(context, context.Node, attrs.Item2, Rule, attributeName);
                    break;
                case ASTUtilities.AttributeType.NotImplemented:
                    context.ReportDiagnostic(
                        Diagnostic.Create(Rule, context.Node.GetLocation(), nameof(ASTUtilities.AttributeType.NotImplemented)));
                    break;
                case ASTUtilities.AttributeType.Invalid:
                    context.ReportDiagnostic(
                        Diagnostic.Create(Rule, context.Node.GetLocation(), nameof(ASTUtilities.AttributeType.NotImplemented)));
                    break;
                case ASTUtilities.AttributeType.IsDefaultable:
                    context.ReportDiagnostic(
                        Diagnostic.Create(Rule, context.Node.GetLocation(), nameof(ASTUtilities.AttributeType.IsDefaultable)));
                    break;
                case ASTUtilities.AttributeType.NoAnnotation:
                    //There is no annotation to verify, so do nothing
                    break;
            }
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

            //In addition to the symbols we need the argument syntax so that we may hang annotations on our AST
            var argList = invocationExpr.ArgumentList.Arguments;

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

                for(int j = 0; j < attributes.Count(); j++)
                {
                    var attr = attributes[j];

                    var oldArg = argList[i];
                    
                    var newArg = oldArg.WithAdditionalAnnotations(syntaxAnnotation);
                    var analysisRoot = context.SemanticModel.Compilation.SyntaxTrees.First().GetRoot();
                    docRoot = docRoot?.ReplaceNode(oldArg, newArg) ?? analysisRoot.ReplaceNode(oldArg, newArg);

                    //invocationExpr = invocationExpr.ReplaceNode(oldArg, newArg);
                    
                    //var paramSyntax = param.DeclaringSyntaxReferences;
                    //if(paramSyntax.Count() != 1)
                    //{
                    //    //There are either 0 or multiple declaring symbols.  If we hit this
                    //    //we may need to get a little more granular.

                    //}
                    //else
                    //{
                    //    var oldParam = paramSyntax.First();
                    //    var newParam = oldParam

                    //    root = root.ReplaceNode(oldUsing, newUsing);

                    //}

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
                List<string> attrs = GetAttributes(context, identifierName);

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
            else
            {
                var memAccess = assignmentExpression.Left as MemberAccessExpressionSyntax;
                if(memAccess != null)
                {
                    List<string> memAttrs = GetAttributes(context, memAccess);

                    //If we didn't find any annotations then we return the appropriate enum value indicating as much
                    if (memAttrs.Count() > 0)
                    {
                        return new Tuple<AttributeType, List<string>>(AttributeType.HasAnnotation, memAttrs);
                    }
                    else
                    {
                        return new Tuple<AttributeType, List<string>>(AttributeType.NoAnnotation, null);
                    }
                }
            }

            return new Tuple<AttributeType, List<string>>(AttributeType.NotImplemented, null);
        }

        /// <summary>
        /// Get a list of attributes associated with a identifier
        /// </summary>
        /// <param name="context"></param>
        /// <param name="identifierName"></param>
        /// <returns></returns>
        public List<string> GetAttributes(SyntaxNodeAnalysisContext context, IdentifierNameSyntax identifierName)
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

            return attrs;
        }

        /// <summary>
        /// Get a list of attributes associated with a member access expression
        /// </summary>
        /// <param name="context"></param>
        /// <param name="memAccess"></param>
        /// <returns></returns>
        public List<string> GetAttributes(SyntaxNodeAnalysisContext context, MemberAccessExpressionSyntax memAccess)
        {
            List<string> attrs = new List<string>();
            //May need to differentiate between a property access expression and a method invocation

            // Get the symbol associated with the property
            SymbolInfo info = context.SemanticModel.GetSymbolInfo(memAccess);
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

            return attrs;
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
                                    //TODO: Need mechanism to determine which attributes we care about so
                                    //that additional ones which are present do not throw off our analysis
                                    //and present warnings when they should not
                                    expectedAttribute[i].Remove(argAttr.AttributeClass.ToString());
                                }
                            }

                            //If we haven't found a match then present a diagnotic error
                            if (expectedAttribute[i].Count() > 0)
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
                            // Used to store the method symbol associated with the invocation expression
                            IMethodSymbol argSymbol = null;
                            var argInvExpr = argumentList.Arguments[i].Expression as InvocationExpressionSyntax;
                            if (argInvExpr != null)
                            {
                                //If we have a local method invocation
                                var methodIdNameExpr = argInvExpr.Expression as IdentifierNameSyntax;
                                if (methodIdNameExpr != null)
                                {
                                    argSymbol = context.SemanticModel.GetSymbolInfo(methodIdNameExpr).Symbol as IMethodSymbol;
                                }
                                else
                                {
                                    //If we don't have a local method invocation, we may have a static or instance method invocation
                                    var memberAccessExpr = argInvExpr.Expression as MemberAccessExpressionSyntax;
                                    if (memberAccessExpr != null)
                                    {
                                        argSymbol = context.SemanticModel.GetSymbolInfo(memberAccessExpr).Symbol as IMethodSymbol;
                                    }
                                }
                            }

                            if (argSymbol != null)
                            {
                                // Now we check the return type to see if there is an attribute assigned
                                var returnTypeAttrs = argSymbol.GetReturnTypeAttributes();
                                foreach (var retAttr in returnTypeAttrs)
                                {
                                    if (expectedAttribute[i].Contains(retAttr.AttributeClass.ToString()))
                                    {
                                        //TODO: Need mechanism to determine which attributes we care about so
                                        //that additional ones which are present do not throw off our analysis
                                        //and present warnings when they should not
                                        expectedAttribute[i].Remove(retAttr.AttributeClass.ToString());
                                    }
                                }

                                //If we haven't found a match then present a diagnotic error
                                if (expectedAttribute[i].Count() > 0)
                                {
                                    var diagnostic = Diagnostic.Create(rule, argumentList.Arguments[i].Expression.GetLocation(), description);
                                    context.ReportDiagnostic(diagnostic);
                                }
                            }
                            else //We may be dealing with a field like String.Empty
                            {
                                var fieldSymbol = context.SemanticModel.GetSymbolInfo(argumentList.Arguments[i].Expression).Symbol as IFieldSymbol;
                                if (fieldSymbol != null)
                                {
                                    var fieldAttrs = fieldSymbol.GetAttributes();
                                    foreach (var fieldAttr in fieldAttrs)
                                    {
                                        if (expectedAttribute[i].Contains(fieldAttr.AttributeClass.ToString()))
                                        {
                                            //TODO: Need mechanism to determine which attributes we care about so
                                            //that additional ones which are present do not throw off our analysis
                                            //and present warnings when they should not
                                            expectedAttribute[i].Remove(fieldAttr.AttributeClass.ToString());
                                        }
                                    }

                                    //If we haven't found a match then present a diagnotic error
                                    if (expectedAttribute[i].Count() > 0)
                                    {
                                        var diagnostic = Diagnostic.Create(rule, argumentList.Arguments[i].Expression.GetLocation(), description);
                                        context.ReportDiagnostic(diagnostic);
                                    }
                                }
                                else
                                {
                                    var propertySymbol = context.SemanticModel.GetSymbolInfo(argumentList.Arguments[i].Expression).Symbol as IPropertySymbol;
                                    if (propertySymbol != null)
                                    {
                                        var propAttrs = fieldSymbol.GetAttributes();
                                        foreach (var propAttr in propAttrs)
                                        {
                                            if (expectedAttribute[i].Contains(propAttr.AttributeClass.ToString()))
                                            {
                                                //TODO: Need mechanism to determine which attributes we care about so
                                                //that additional ones which are present do not throw off our analysis
                                                //and present warnings when they should not
                                                expectedAttribute[i].Remove(propAttr.AttributeClass.ToString());
                                            }
                                        }

                                        //If we haven't found a match then present a diagnotic error
                                        if (expectedAttribute[i].Count() > 0)
                                        {
                                            var diagnostic = Diagnostic.Create(rule, argumentList.Arguments[i].Expression.GetLocation(), description);
                                            context.ReportDiagnostic(diagnostic);
                                        }
                                    }
                                    else
                                    {
                                        var diagnostic = Diagnostic.Create(rule, argumentList.Arguments[i].Expression.GetLocation(), nameof(AttributeType.NotImplemented));
                                        context.ReportDiagnostic(diagnostic);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void VerifyAttribute(SyntaxNodeAnalysisContext context, ExpressionSyntax exp, IFieldSymbol fieldSymbol, List<string> expectedAttributes, DiagnosticDescriptor rule, string description)
        {
            var fieldAttrs = fieldSymbol.GetAttributes();
            foreach (var fieldAttr in fieldAttrs)
            {
                if (expectedAttributes.Contains(fieldAttr.AttributeClass.ToString()))
                {
                    //TODO: Need mechanism to determine which attributes we care about so
                    //that additional ones which are present do not throw off our analysis
                    //and present warnings when they should not
                    expectedAttributes.Remove(fieldAttr.AttributeClass.ToString());
                }
            }

            //If we haven't found a match then present a diagnotic error
            if (expectedAttributes.Count() > 0)
            {
                var diagnostic = Diagnostic.Create(rule, exp.GetLocation(), description);
                context.ReportDiagnostic(diagnostic);
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
                    var returnTypeAttrs = memberSymbol.GetReturnTypeAttributes();
                    foreach (var retAttr in returnTypeAttrs)
                    {
                        if (argAttrs.Contains(retAttr.AttributeClass.ToString()))
                        {
                            //TODO: Need mechanism to determine which attributes we care about so
                            //that additional ones which are present do not throw off our analysis
                            //and present warnings when they should not
                            argAttrs.Remove(retAttr.AttributeClass.ToString());
                        }


                    }

                    //If we haven't found a match then present a diagnotic error
                    if (argAttrs.Count() > 0)
                    {
                        var diagnostic = Diagnostic.Create(rule, invocationExpr.GetLocation(), description);
                        //Now we register this diagnostic with visual studio
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
            else
            {
                var rhsLit = assignmentExpression.Right as LiteralExpressionSyntax;
                if (rhsLit != null)
                {
                    var diagnostic = Diagnostic.Create(rule, rhsLit.GetLocation(), description);
                    //Now we register this diagnostic with visual studio
                    context.ReportDiagnostic(diagnostic);
                }
                else
                {
                    //TODO: We should pull out methods such as this so that they can be used in several areas of the code
                    var fieldSymbol = context.SemanticModel.GetSymbolInfo(assignmentExpression.Right).Symbol as IFieldSymbol;
                    if (fieldSymbol != null)
                    {
                        VerifyAttribute(context, assignmentExpression.Right, fieldSymbol, argAttrs, rule, description);
                    }
                    //TODO: Add property symbols here
                }
            }
        }

        public void CompilationEndAction(CompilationAnalysisContext context)
        {
            //Lines below copied from the sample in D:\GitHub\roslyn\src\Samples\Samples.sln
            //Specificially - CompilationStartedAnalyzerWithCompilationWideAnalysis

            //Here we should lookup any nodes which have attributes, and check to make sure
            //they are abided by.  If not then we present a diagnostic.
            //It may be most effecient to leverage a walker which visits all the nodes of the tree,
            //so that we can implement different methods to analyze different constructs, and 
            //only traverse the tree once while performing our validation
            var changedArgument = docRoot.GetAnnotatedNodesAndTokens("Parameter");

            //var changedArgument = context.Compilation.SyntaxTrees.First().GetRoot().GetAnnotatedNodes("Parameter");
            if(changedArgument.Count() > 0)
            {
                var changed = changedArgument.Count();
            }

            //var changedClass = context.Compilation.SyntaxTrees.First().GetRoot().DescendantNodes()
            //    .Where(n => n.HasAnnotation(syntaxAnnotation)).Single();

            var stophere = true;
            //if (_interfacesWithUnsecureMethods == null || _secureTypes == null)
            //{
            //    // No violating types.
            //    return;
            //}

            //// Report diagnostic for violating named types.
            //foreach (var secureType in _secureTypes)
            //{
            //    foreach (var unsecureInterface in _interfacesWithUnsecureMethods)
            //    {
            //        if (secureType.AllInterfaces.Contains(unsecureInterface))
            //        {
            //            var diagnostic = Diagnostic.Create(Rule, secureType.Locations[0], secureType.Name, SecureTypeInterfaceName, unsecureInterface.Name);
            //            context.ReportDiagnostic(diagnostic);

            //            break;
            //        }
            //    }
            //}
        }
    }
}
