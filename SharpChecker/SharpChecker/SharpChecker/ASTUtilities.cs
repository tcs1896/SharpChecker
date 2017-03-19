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
            //You shouldn't be asking for an annotation on this type of element
            Invalid = 4
        }

        SyntaxAnnotation syntaxAnnotation = new SyntaxAnnotation("Parameter");

        Dictionary<SyntaxNode, List<List<String>>> AnnotationDictionary = new Dictionary<SyntaxNode, List<List<string>>>();

        //We may want to put these somewhere else in the future
        private DiagnosticDescriptor rule;
        private string attributeName;

        public ASTUtilities(DiagnosticDescriptor rule, string attributeName)
        {
            this.rule = rule;
            this.attributeName = attributeName;
            //We may want to search for attribute definitions which are decorated with something like 
            //[SharpChecker] then include these attributes in our analysis...
            //
            //[SharpChecker]
            //[AttributeUsage(AttributeTargets.All, Inherited = true, AllowMultiple = true)]
            //class EncryptedAttribute : Attribute
            //{}
        }

        public void FindAllAttributes(SyntaxNodeAnalysisContext context)
        {
            //Perhaps we can take advantage of dynamic dispatch keeping the structure of the method below,
            //but passing an instance of a particular "SharpCheckerAnalyzer" which will know how to analyze itself
            this.GetAttributes(context, context.Node);

            //switch (attrs.Item1)
            //{
            //    case ASTUtilities.AttributeType.HasAnnotation:
            //        //this.VerifyAttributes(context, context.Node, attrs.Item2, Rule, attributeName);
            //        break;
            //    case ASTUtilities.AttributeType.NotImplemented:
            //        context.ReportDiagnostic(
            //            Diagnostic.Create(Rule, context.Node.GetLocation(), nameof(ASTUtilities.AttributeType.NotImplemented)));
            //        break;
            //    case ASTUtilities.AttributeType.Invalid:
            //        context.ReportDiagnostic(
            //            Diagnostic.Create(Rule, context.Node.GetLocation(), nameof(ASTUtilities.AttributeType.NotImplemented)));
            //        break;
            //    case ASTUtilities.AttributeType.IsDefaultable:
            //        context.ReportDiagnostic(
            //            Diagnostic.Create(Rule, context.Node.GetLocation(), nameof(ASTUtilities.AttributeType.IsDefaultable)));
            //        break;
            //    case ASTUtilities.AttributeType.NoAnnotation:
            //        //There is no annotation to verify, so do nothing
            //        break;
            //}
        }

        /// <summary>
        /// Descend into the syntax node as far as necessary to determine the associated attributes which are expected
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public void GetAttributes(SyntaxNodeAnalysisContext context, SyntaxNode node)
        {
            //Determine if we are dealing with an InvocationExpression or a SimpleAssignment
            switch (node.Kind())
            {
                case SyntaxKind.InvocationExpression:
                    var invocationExpr = node as InvocationExpressionSyntax;
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
                var retAttrStrings = new List<String>();
                foreach (var retAttr in returnTypeAttrs)
                {
                    retAttrStrings.Add(retAttr.AttributeClass.ToString());
                }
                //An exception was generated here because we were attempting to add the same name twice.
                //This leads me to believe that the same identifier occurring in different locations in
                //the source text may not be distinguished.  We could perhaps introduce a composite key
                //involving "span" so that we could distinguish uses at different locations in the source text.
                if (!AnnotationDictionary.ContainsKey(identifierNameExpr))
                {
                    AnnotationDictionary.Add(identifierNameExpr, new List<List<String>>() { retAttrStrings });
                }
            }

            //Grab the argument list so we can interrogate it
            var argumentList = invocationExpr.ArgumentList as ArgumentListSyntax;

            for (int i = 0; i < argumentList.Arguments.Count; i++)
            {
                //Here we are handling the case where the argument is an identifier
                var argI = argumentList.Arguments[i].Expression as IdentifierNameSyntax;
                if (argI != null)
                {
                    SymbolInfo info = context.SemanticModel.GetSymbolInfo(argI);
                    ISymbol symbol = info.Symbol;
                    AddSymbolAttributes(argI, symbol);
                }
                else
                {
                    var argInvExpr = argumentList.Arguments[i].Expression as InvocationExpressionSyntax;
                    //If this is another invocation expression then we should recurse
                    //This is an important pattern which should be replicated elsewhere
                    //As an example: If we have a binary expression we should recursively 
                    //analyze the right and left then combine the result - like type checking
                    if (argInvExpr != null)
                    {
                        AnalyzeInvocationExpr(context, argInvExpr);
                    }
                    else
                    {
                        var fieldSymbol = context.SemanticModel.GetSymbolInfo(argumentList.Arguments[i].Expression).Symbol as IFieldSymbol;
                        if (fieldSymbol != null)
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

                for(int j = 0; j < attributes.Count(); j++)
                {
                    var attr = attributes[j];
                    paramAttrs.Add(attr.AttributeClass.ToString());
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
                List<String> argAttrStrings = new List<string>();
                foreach (var argAttr in argAttrs)
                {
                    argAttrStrings.Add(argAttr.AttributeClass.ToString());
                }
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
            var identifierName = assignmentExpression.Left as IdentifierNameSyntax;

            if (identifierName != null)
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
                var memAccess = assignmentExpression.Left as MemberAccessExpressionSyntax;
                if(memAccess != null)
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

            var walker = new SCBaseSyntaxWalker(rule, attributeName, AnnotationDictionary, context);
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
