using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using SharpChecker.attributes;

namespace SharpChecker
{
    class ASTUtilities
    {
        //Dictionary where we will store type annotations.  Initially, these are explicitly declared with attributes.
        //Eventually, they may be inferred based on context, data flow, and control flow.
        public Dictionary<SyntaxNode, List<List<String>>> AnnotationDictionary = new Dictionary<SyntaxNode, List<List<string>>>();
        //The list of attributes with which the analysis will be concerned
        public List<string> SharpCheckerAttributes = new List<string>();

        private static SCBaseAnalyzer baseAnalyzer; //= new EncryptedAnalyzer().AnalyzerFactory();

        static ASTUtilities()
        {
            var type = Type.GetType("SharpChecker.EncryptedAnalyzer");
            baseAnalyzer = (SCBaseAnalyzer)Activator.CreateInstance(type);
        }

        /// <summary>
        /// Prior to Roslyn firing actions associated with the expressions and statements we want to analyze
        /// we load the list of attributes which with the analysis will be concerned.
        /// </summary>
        public ASTUtilities()
        {
            var myAttributes = baseAnalyzer.GetAttributesToUseInAnalysis();
            foreach (var att in myAttributes)
            {
                AddAttributeClassToAnalysis(att);
            }
        }

        /// <summary>
        /// This helper method should be called to add attributes to the list
        /// with which SharpChecker will concern itself.
        /// </summary>
        /// <param name="attr">The name of the attribute class</param>
        public void AddAttributeClassToAnalysis(string attr)
        {
            var attrToAdd = RemoveAttributeEnding(attr);
            if (!SharpCheckerAttributes.Contains(attrToAdd))
            {
                SharpCheckerAttributes.Add(attrToAdd);
            }
        }

        /// <summary>
        /// Returns a collection of diagnostic descriptors to the DiagnosticAnalyzer which drives the analysis.
        /// We need some mechamism for additional type systems in plug into this and add to the list  
        /// of supported diagnostics.  We could instantiate an object of another class which has a method to return these.
        /// Type system creators could then override that method to add their own diagnostics to the list before
        /// invoking the base functionality, so that the whole accumulated list is returned here.
        /// </summary>
        /// <returns>The rules that we will enforce</returns>
        public static ImmutableArray<DiagnosticDescriptor> GetRules()
        {
            return baseAnalyzer.GetRules();
        }

        public static SyntaxKind[] GetSyntaxKinds()
        {
            return baseAnalyzer.GetSyntaxKinds();
        }

        /// <summary>
        /// Helper method used to remove the "Attribute" suffix present in certain contexts
        /// </summary>
        /// <param name="raw">A string which may end in "Attribute"</param>
        /// <returns>The argument value without the "Attribute" suffix</returns>
        public string RemoveAttributeEnding(string raw)
        {
            return raw.EndsWith("Attribute") ? raw.Replace("Attribute", "") : raw;
        }

        /// <summary>
        /// Serves as an entry point for the analysis.  This is the executed by the SyntaxNodeActions fired by Roslyn. 
        /// </summary>
        /// <param name="context">The analysis context</param>
        public void AnalyzeExpression(SyntaxNodeAnalysisContext context)
        {
            baseAnalyzer.AnalyzeExpression(context, context.Node, this);
        }


        /// <summary>
        /// Accepts a collection of attributes and filters them down to those which were registered for analysis
        /// </summary>
        /// <param name="returnTypeAttrs">A collection of attributes</param>
        /// <returns>The intersection of the attribute set which was registered and that provided as an argument</returns>
        public List<String> GetSharpCheckerAttributeStrings(ImmutableArray<AttributeData> returnTypeAttrs)
        {
            var retAttrStrings = new List<String>();
            foreach(var attData in returnTypeAttrs)
            {
                //See if we have previosly recorded this as a attribute we are interested in
                string att = RemoveAttributeEnding(attData.AttributeClass.MetadataName);
                if (SharpCheckerAttributes.Contains(att))
                {
                    retAttrStrings.Add(att);
                }
            }

            return retAttrStrings;
        }

        /// <summary>
        /// Helper method which accepts retreives the attributes associated with a symbol
        /// and adds them to our global table with a sn as the key
        /// </summary>
        /// <param name="sn">The syntax node which we are analyzing</param>
        /// <param name="symbol">The symbol associated with the syntax node</param>
        public void AddSymbolAttributes(SyntaxNode sn, ISymbol symbol)
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
        /// Get a list of attributes associated with a syntax node
        /// </summary>
        /// <param name="context">The analysis context</param>
        /// <param name="synNode">A syntax node</param>
        /// <returns>A list of attributes associated with a syntax node</returns>
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
        /// syntax tree and verify that they respect our subtyping relationships. We use a 
        /// CSharpSyntaxWalker which visits all the nodes of the tree, so that we can implement 
        /// different methods to analyze different constructs, and only traverse the tree once 
        /// while performing our validation.
        /// </summary>
        /// <param name="context">The analysis context</param>
        public void VerifyTypeAnnotations(SemanticModelAnalysisContext context)
        {
            var walker = new SCBaseSyntaxWalker(baseAnalyzer.GetRules().First(), AnnotationDictionary, context, SharpCheckerAttributes);
            walker.Visit(context.SemanticModel.SyntaxTree.GetRoot());

            //A thought about expanding upon the current functionality:
            //From this point forward we would never need to persist the information which we gain back out to the world so
            //we could add annotations, and do things such as: upon finding a null check, walk over all references to the
            //variable checked for null which occur in that block an annotate them as nonnull

            //Preserving the commented sections below for now as a reminder of some false starts.  Need to include this
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
