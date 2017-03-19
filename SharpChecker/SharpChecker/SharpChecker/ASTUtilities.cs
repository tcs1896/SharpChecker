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
        //Dictionary where we will store type annotations.  Initially, these are explicity defined with attributes.
        //The attributes themselves are decorated with the [SharpChecker] attribute.  Eventually, they may be inferred
        //based on context, data flow, and control flow.
        Dictionary<SyntaxNode, List<List<String>>> AnnotationDictionary = new Dictionary<SyntaxNode, List<List<string>>>();
        //We cache the list of attributes which we are concerned with, so that we don't have to lookup the definition many times
        List<string> SharpCheckerAttributes = new List<string>();

        //We may want to put these somewhere else in the future
        private DiagnosticDescriptor rule;

        public ASTUtilities(DiagnosticDescriptor rule, CompilationStartAnalysisContext compilationContext)
        {
            this.rule = rule;
            //We may want to search for attribute definitions which are decorated with something like 
            //[SharpChecker] then include these attributes in our analysis...
            //
            //[SharpChecker]
            //[AttributeUsage(AttributeTargets.All, Inherited = true, AllowMultiple = true)]
            //class EncryptedAttribute : Attribute
            //{}
            foreach(var tree in compilationContext.Compilation.SyntaxTrees)
            {
                var classes = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>();
                foreach (var clazz in classes)
                {
                    //If there are no attributes decorating this class then move on
                    if(clazz.AttributeLists.Count() == 0) { continue; }

                    bool hasAttributeUsage = false;
                    bool hasSharpChecker = false;
                    foreach (var attrList in clazz.AttributeLists)
                    {
                        foreach (var attr in attrList.Attributes)
                        {
                            var attrClass = attr.Name.ToString();
                            if (attrClass.EndsWith("Attribute"))
                            {
                                attrClass = attrClass.Replace("Attribute", "");
                            }

                            if (attrClass == "AttributeUsage")
                            {
                                hasAttributeUsage = true;
                            }

                            if (attrClass == "SharpChecker")
                            {
                                hasSharpChecker = true;
                            }
                        }
                    }

                    //If this was defined as an attribute, and has the [SharpChecker] attribute
                    if (hasAttributeUsage && hasSharpChecker)
                    {
                        var className = clazz.Identifier.Text;
                        if (className.EndsWith("Attribute"))
                        {
                            className = className.Replace("Attribute", "");
                        }
                        SharpCheckerAttributes.Add(className);
                    }
                }
            }
        }

        /// <summary>
        /// Descend into the syntax node as far as necessary to determine the associated attributes which are expected.
        /// This is the executed by the SyntaxNodeActions fired by Roslyn.
        /// </summary>
        /// <param name="context"></param>
        public void GetAttributes(SyntaxNodeAnalysisContext context)
        {
            //Determine what type of sytax node we are dealing with
            switch (context.Node.Kind())
            {
                case SyntaxKind.InvocationExpression:
                    var invocationExpr = context.Node as InvocationExpressionSyntax;
                    AnalyzeInvocationExpr(context, invocationExpr);
                    break;
                case SyntaxKind.SimpleAssignmentExpression:
                    var assignmentExpression = context.Node as AssignmentExpressionSyntax;
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
        /// <param name="context"></param>
        /// <param name="invocationExpr"></param>
        private void AnalyzeInvocationExpr(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocationExpr)
        {
            var identifierNameExpr = invocationExpr.Expression as IdentifierNameSyntax;
            if (identifierNameExpr == null)
            {
                return;
            }
            
            //This will lookup the method associated with the invocation expression
            var memberSymbol = context.SemanticModel.GetSymbolInfo(identifierNameExpr).Symbol as IMethodSymbol;
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
                AddAttributesToDictionary(identifierNameExpr, returnTypeAttrs);
            }

            //Grab the argument list so we can interrogate it
            var argumentList = invocationExpr.ArgumentList as ArgumentListSyntax;

            for (int i = 0; i < argumentList.Arguments.Count; i++)
            {
                //Here we are handling the case where the argument is an identifier
                if (argumentList.Arguments[i].Expression is IdentifierNameSyntax argI)
                {
                    var symbol = context.SemanticModel.GetSymbolInfo(argI).Symbol;
                    AddSymbolAttributes(argI, symbol);
                }
                else
                {
                    //If this is another invocation expression then we should recurse
                    //This is an important pattern which should be replicated elsewhere
                    //As an example: If we have a binary expression we should recursively 
                    //analyze the right and left then combine the result - like type checking
                    if (argumentList.Arguments[i].Expression is InvocationExpressionSyntax argInvExpr)
                    {
                        AnalyzeInvocationExpr(context, argInvExpr);
                    }
                    else
                    {
                        if (context.SemanticModel.GetSymbolInfo(argumentList.Arguments[i].Expression).Symbol is IFieldSymbol fieldSymbol)
                        {
                            //var fieldAttrs = fieldSymbol.GetAttributes();
                            AddSymbolAttributes(argumentList.Arguments[i].Expression, fieldSymbol);
                        }
                        else
                        {
                            var propertySymbol = context.SemanticModel.GetSymbolInfo(argumentList.Arguments[i].Expression).Symbol as IPropertySymbol;
                            AddSymbolAttributes(argumentList.Arguments[i].Expression, propertySymbol);
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
                var attributeStrings = GetSharpCheckerAttributeStrings(attributes);

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
                AnnotationDictionary.Add(argumentList, attrListParams);
            }
        }

        private List<String> GetSharpCheckerAttributeStrings(ImmutableArray<AttributeData> returnTypeAttrs)
        {
            var retAttrStrings = new List<String>();
            foreach(var attData in returnTypeAttrs)
            {
                //See if we have previosly recorded this as a attribute we are interested in
                string att = attData.AttributeClass.MetadataName;
                if (att.EndsWith("Attribute"))
                {
                    att = att.Replace("Attribute", "");
                }

                if (SharpCheckerAttributes.Contains(att))
                {
                    retAttrStrings.Add(att);
                }
            }

            return retAttrStrings;
        }

        private void AddAttributesToDictionary(IdentifierNameSyntax identifierNameExpr, ImmutableArray<AttributeData> returnTypeAttrs)
        {
            var retAttrStrings = GetSharpCheckerAttributeStrings(returnTypeAttrs);
            //An exception was generated here because we were attempting to add the same name twice.
            //This leads me to believe that the same identifier occurring in different locations in
            //the source text may not be distinguished.  We could perhaps introduce a composite key
            //involving "span" so that we could distinguish uses at different locations in the source text.
            if (!AnnotationDictionary.ContainsKey(identifierNameExpr))
            {
                AnnotationDictionary.Add(identifierNameExpr, new List<List<String>>() { retAttrStrings });
            }
        }

        /// <summary>
        /// Helper method which accepts retreives the attributes associated with a symbol
        /// and adds them to our global table with a sn as the key
        /// </summary>
        /// <param name="sn">The syntax node which we are analyzing</param>
        /// <param name="symbol">The symbol associated with the syntax node</param>
        private void AddSymbolAttributes(SyntaxNode sn, ISymbol symbol)
        {
            if (symbol != null)
            {
                var argAttrs = symbol.GetAttributes();
                List<String> argAttrStrings = GetSharpCheckerAttributeStrings(argAttrs);

                //Add the list of expected attributes to the dictionary
                if (AnnotationDictionary.ContainsKey(sn))
                {
                    //We should probably check for duplicates here
                    if (!AnnotationDictionary[sn].Contains(argAttrStrings))
                    {
                        AnnotationDictionary[sn].Add(argAttrStrings);
                    }
                }
                //Not sure if this acceptable.  We may need to distinguish between separate instances
                else
                {
                    AnnotationDictionary.Add(sn, new List<List<string>>() { argAttrStrings });
                }
            }
        }

        /// <summary>
        /// Get the annotated type associated with the LHS of an assignment (maybe called the receiver)
        /// </summary>
        /// <param name="context"></param>
        /// <param name="assignmentExpression"></param>
        /// <returns></returns>
        private void AnalyzeAssignmentExpression(SyntaxNodeAnalysisContext context, AssignmentExpressionSyntax assignmentExpression)
        {
            // First check the variable to which we are assigning
            if (assignmentExpression.Left is IdentifierNameSyntax identifierName)
            {
                List<string> attrs = GetAttributes(context, identifierName);

                //If we didn't find any annotations then we return the appropriate enum value indicating as much
                if (attrs.Count() > 0)
                {
                    //Add the list of expected attributes to the dictionary
                    AnnotationDictionary.Add(identifierName, new List<List<string>>() { attrs });
                }
            }
            else
            {
                if (assignmentExpression.Left is MemberAccessExpressionSyntax memAccess)
                {
                    List<string> memAttrs = GetAttributes(context, memAccess);

                    //If we didn't find any annotations then we return the appropriate enum value indicating as much
                    if (memAttrs.Count() > 0)
                    {
                        //Add the list of expected attributes to the dictionary
                        AnnotationDictionary.Add(memAccess, new List<List<string>>() { memAttrs });
                    }
                }
            }
        }

        /// <summary>
        /// Get a list of attributes associated with a syntax node
        /// </summary>
        /// <param name="context"></param>
        /// <param name="synNode"></param>
        /// <returns></returns>
        public List<string> GetAttributes(SyntaxNodeAnalysisContext context, SyntaxNode synNode)
        {
            List<string> attrs = new List<string>();

            // Get the symbol associated with the property
            var symbol = context.SemanticModel.GetSymbolInfo(synNode).Symbol;
            if (symbol != null)
            {
                // Check if there are attributes associated with this symbol
                var argAttrs = symbol.GetAttributes();
                attrs = GetSharpCheckerAttributeStrings(argAttrs);
            }

            return attrs;
        }

        /// <summary>
        /// Now that we have collected all the type annotation information we walk over the
        /// syntax tree and verify that they respect our subtyping relationships
        /// </summary>
        /// <param name="context"></param>
        public void VerifyTypeAnnotations(SemanticModelAnalysisContext context)
        {
            //Here we should lookup any nodes which have attributes, and check to make sure
            //they are abided by.  If not then we present a diagnostic.

            //It may be most effecient to leverage a walker which visits all the nodes of the tree,
            //so that we can implement different methods to analyze different constructs, and 
            //only traverse the tree once while performing our validation

            //At this point because we would never need to persist the information which we gain back out to the world
            //we could add annotations, and do things such as: upon finding a null check, walk over all references to the
            //variable checked for null which occur in that block an annotate them as nonnull

            var walker = new SCBaseSyntaxWalker(rule, AnnotationDictionary, context, SharpCheckerAttributes);
            walker.Visit(context.SemanticModel.SyntaxTree.GetRoot());

            //Leaving this around for now as a reminder of some false starts.  Need to include this
            //in the final writeup along with suggested improvements to Roslyn

            //var newCompilation = context.Compilation.ReplaceSyntaxTree(tree, walker.GetTree());

            //var changedArgument = walker.GetTree().GetRoot().GetAnnotatedNodesAndTokens("Parameter");
            ////The immediate parent is the argumentlistsyntax, the parent of that is the method being invoked
            ////Getting an error here because the syntax node is not in the tree
            //var iMeth = context.Compilation.GetSemanticModel(tree).GetSymbolInfo(changedArgument.First().Parent.Parent).Symbol as IMethodSymbol;

            //var changedArgument = tree.GetRoot().GetAnnotatedNodesAndTokens("Parameter");
            //The immediate parent is the argumentlistsyntax, the parent of that is the method being invoked
            //var iMeth = context.Compilation.GetSemanticModel(tree).GetSymbolInfo(changedArgument.First().Parent.Parent).Symbol as IMethodSymbol;

            ////var changedArgument = context.Compilation.SyntaxTrees.First().GetRoot().GetAnnotatedNodes("Parameter");
            //if(changedArgument.Count() > 0)
            //{
            //    var changed = changedArgument.Count();
            //}

            //var changedClass = context.Compilation.SyntaxTrees.First().GetRoot().DescendantNodes()
            //    .Where(n => n.HasAnnotation(syntaxAnnotation)).Single();
        }
    }
}
