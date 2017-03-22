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
    public class SharpCheckerBaseAnalyzer : DiagnosticAnalyzer
    {
        //Could we invert control here, so that our analyzer calls out to a factory method which returns array 
        //of supported diagnostics.  I'm imagining one base method which would register the basic functionality.
        //Type system creators could then override that method to add their own diagnostics to the list before
        //invoking the base functionality, so that the whole accumulated list is returned here.
        public const string DiagnosticId = "SharpCheckerMethodParams";
        internal const string Title = "Error in attribute applications";
        internal const string MessageFormat = "Attribute application error {0}";
        internal const string Description = "There is a mismatch between the effective attribute and the one expected";
        internal const string Category = "Syntax";
        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            //At one point I was thinking that we needed assurances about the execution of certain actions which couldn't 
            //be counted upon unless we did all of our analysis in one place (the endcompilation action).  However, given
            //the new design of accumulating all the type annotations in a collection we can leverage the different types of
            //actions which Roslyn exposes.
            //context.RegisterCompilationAction(compilationContext =>
            //{
            //    var analyzer = new ASTUtilities(Rule, attributeName);
            //    analyzer.CompilationEndAction(compilationContext);
            //});

            context.RegisterCompilationStartAction(compilationContext =>
            {
                // Perform any setup necessary for our analysis in the constructor
                var analyzer = new ASTUtilities(Rule, compilationContext.Compilation.SyntaxTrees);

                // Register an intermediate non-end action that accesses and modifies the state.
                //compilationContext.RegisterSymbolAction(analyzer.AnalyzeNode, SymbolKind.NamedType, SymbolKind.Method);

                //We are interested in InvocationExpressions because we need to check that the arguments passed to a method with annotated parameters
                //have arguments with the same annotations.  We are interested in SimpleAssignmentExpressions because we only want to allow an annotated 
                //to an annotated variable when we can ensure that the value is of the appropriate annotated type.
                compilationContext.RegisterSyntaxNodeAction<SyntaxKind>(analyzer.AnalyzeExpression, 
                    SyntaxKind.InvocationExpression, SyntaxKind.SimpleAssignmentExpression); 

                // Register an end action to report diagnostics based on the final state.
                compilationContext.RegisterSemanticModelAction(analyzer.VerifyTypeAnnotations);
            });
        }

        //We may want to define methods here which are invoked above.  That way if someone would like
        //to override the default behavior, they may do so.
        //Alternatively they could override the methods in ASTUtilities like analyzer.AnalyzeNode
    }
}
