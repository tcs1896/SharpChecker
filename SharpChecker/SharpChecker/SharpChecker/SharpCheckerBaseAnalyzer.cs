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
        public const string DiagnosticId = "SharpCheckerMethodParams";
        internal const string Title = "Error in attribute applications";
        internal const string MessageFormat = "Attribute application error {0}";
        internal const string Description = "There is a mismatch between the attribute of the formal parameter and that of the argument";
        internal const string Category = "Syntax";
        private const string attributeName = "EncryptedSandbox.EncryptedAttribute";
        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterCompilationStartAction(compilationContext =>
            {
                // Perform any setup necessary for our analysis in the constructor
                var analyzer = new ASTUtilities(Rule, attributeName);

                // Register an intermediate non-end action that accesses and modifies the state.
                //compilationContext.RegisterSymbolAction(analyzer.AnalyzeNode, SymbolKind.NamedType, SymbolKind.Method);

                //We are interested in InvocationExpressions because we need to check that the arguments passed to a method with annotated parameters
                //have arguments with the same annotations.  We are interested in SimpleAssignmentExpressions because we only want to allow an annotated 
                //to an annotated variable when we can ensure that the value is of the appropriate annotated type.
                compilationContext.RegisterSyntaxNodeAction<SyntaxKind>(analyzer.AnalyzeNode, SyntaxKind.InvocationExpression, SyntaxKind.SimpleAssignmentExpression);

                // Register an end action to report diagnostics based on the final state.
                compilationContext.RegisterCompilationEndAction(analyzer.CompilationEndAction);
            });
        }

        //We may want to define methods here which are invoked above.  That way if someone would like
        //to override the default behavior, they may do so.
    }
}
