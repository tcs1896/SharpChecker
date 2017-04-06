using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using SharpChecker.attributes;
using System.Collections.Concurrent;

namespace SharpChecker
{
    class ASTUtilities
    {
        //Dictionary where we will store type annotations.  Initially, these are explicitly declared with attributes.
        //Eventually, they may be inferred based on context, data flow, and control flow.
        public ConcurrentDictionary<SyntaxNode, List<List<String>>> AnnotationDictionary = new ConcurrentDictionary<SyntaxNode, List<List<string>>>();
        //The list of attributes with which the analysis will be concerned
        public List<Node> SharpCheckerAttributes = new List<Node>();
        //The list of analyzers which were specified in the checkers.xml file in the target project
        private List<SCBaseAnalyzer> analyzers = new List<SCBaseAnalyzer>();
        //A dictionary mapping the attribute to the associated rule.  Used in the walker class to register diagnostics.
        private static Dictionary<string, DiagnosticDescriptor> rulesDict = new Dictionary<string, DiagnosticDescriptor>();

        /// <summary>
        /// Prior to Roslyn firing actions associated with the expressions and statements we want to analyze
        /// we load the list of attributes with which the analysis will be concerned.
        /// </summary>
        public ASTUtilities(List<string> checkers)
        {
            //Dynamically load the analyzers associated with the checkers
            foreach (var checker in checkers)
            {
                //For now we assume that the name of the analyzer is the string provided + Analyzer
                var analyzerClass = $"SharpChecker.{checker}Analyzer";
                var type = Type.GetType(analyzerClass);
                if(type == null) { continue; }
                var analyzer = (SCBaseAnalyzer)Activator.CreateInstance(type);
                if(analyzer == null) { continue; }
                //If we arrive here we have an analyzer instance
                analyzer.ASTUtil = this;
                analyzers.Add(analyzer);
            }

            //Now that we have instantiated all the analyzers ask them which attributes they analyze
            AddAttributesForAllAnalyzers();
        }

        /// <summary>
        /// Collect all of the attributes associated with the analyzer classes
        /// </summary>
        public void AddAttributesForAllAnalyzers()
        {
            if(analyzers == null && analyzers.Count == 0) { return; }

            foreach (var analyzer in analyzers)
            {
                var attributes = analyzer.GetAttributesToUseInAnalysis();
                foreach (var attr in attributes)
                {
                    AddAttributeClassToAnalysis(attr);
                }
            }
        }

        /// <summary>
        /// This helper method should be called to add attributes to the list
        /// with which SharpChecker will concern itself.
        /// </summary>
        /// <param name="attr">The name of the attribute class</param>
        public void AddAttributeClassToAnalysis(Node attr)
        {
            var attrToAdd = RemoveAttributeEnding(attr.AttributeName);
            if(!SharpCheckerAttributes.Any(nod => nod.AttributeName == attr.AttributeName))
            {
                attr.AttributeName = attrToAdd;
                SharpCheckerAttributes.Add(attr);
            }
        }

        /// <summary>
        /// Returns a collection of diagnostic descriptors to the DiagnosticAnalyzer which drives the analysis.
        /// We need some mechanism for additional type systems in plug into this and add to the list  
        /// of supported diagnostics.  We could instantiate an object of another class which has a method to return these.
        /// Type system creators could then override that method to add their own diagnostics to the list before
        /// invoking the base functionality, so that the whole accumulated list is returned here.
        /// </summary>
        /// <returns>The rules that we will enforce</returns>
        public static ImmutableArray<DiagnosticDescriptor> GetRules()
        {
            List<DiagnosticDescriptor> descriptors = new List<DiagnosticDescriptor>();
            //If you add a new analyzer class, make sure to add it to this list.  We can't discover these classes using the
            //AdditionalFiles mechanism because we don't have the compilation context when assigning the SupportedDiagnostics
            //property of our DiagnosticAnalyzer instance
            var analyzerClasses = new SCBaseAnalyzer[] { new SCBaseAnalyzer(), new EncryptedAnalyzer(), new NullnessAnalyzer() };
            foreach (var anClass in analyzerClasses)
            {
                var rules = anClass.GetRules();
                
                foreach (var diag in rules)
                {
                    rulesDict[diag.Key] = diag.Value;
                    descriptors.Add(diag.Value);
                }
            }

            return ImmutableArray.Create(descriptors.ToArray());
        }

        /// <summary>
        /// Get the syntax kinds which we would like to visit to collect attributes
        /// </summary>
        /// <returns>A collection of SyntaxKinds</returns>
        public SyntaxKind[] GetSyntaxKinds()
        {
            List<SyntaxKind> kindList = new List<SyntaxKind>();
            foreach (var analyzer in analyzers)
            {
                var analyzerKinds = analyzer.GetSyntaxKinds();
                foreach (var aKind in analyzerKinds)
                {
                    if(!kindList.Contains(aKind))
                    {
                        kindList.Add(aKind);
                    }
                }
            }

            return kindList.ToArray();
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
            //Here we are executing each analyzer in turn, is there a way that we could
            //compose them so that we didn't have to do this
            foreach (var analyzer in analyzers)
            {
                analyzer.AnalyzeExpression(context, context.Node);
            }
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
                //See if we have previously recorded this as a attribute we are interested in
                string att = RemoveAttributeEnding(attData.AttributeClass.MetadataName);
                //if (SharpCheckerAttributes.Contains(att))
                if(SharpCheckerAttributes.Any(nod => nod.AttributeName == att))
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
                    AnnotationDictionary.TryAdd(sn, new List<List<string>>() { argAttrStrings });
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
            foreach(var analyzer in analyzers)
            {
                var walkerType = analyzer.GetSyntaxWalkerType();
                var walker = (SCBaseSyntaxWalker)Activator.CreateInstance(walkerType, rulesDict, AnnotationDictionary, context, SharpCheckerAttributes);
                if (analyzer == null) { continue; }
                walker.Visit(context.SemanticModel.SyntaxTree.GetRoot());
            }

            //var walker = new NullnessSyntaxWalker(rulesDict, AnnotationDictionary, context, SharpCheckerAttributes);
            //walker.Visit(context.SemanticModel.SyntaxTree.GetRoot());

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
