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
            //We are interested in InvocationExpressions because we need to check that the arguments passed to a method with annotated parameters
            //have arguments with the same annotations.  We are interested in SimpleAssignmentExpressions because we only want to allow an annotated 
            //to an annotated variable when we can ensure that the value is of the appropriate annotated type.
            context.RegisterSyntaxNodeAction<SyntaxKind>(AnalyzeNode, SyntaxKind.InvocationExpression, SyntaxKind.SimpleAssignmentExpression);
        }

        private void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            //Perhaps we can take advantage of dynamic dispatch keeping the structure of the method below,
            //but passing an instance of a particular "SharpCheckerAnalyzer" which will know how to analyze itself

            var myASTUtilities = new ASTUtilities();
            var attrs = myASTUtilities.GetAttributes(context, context.Node);

            switch(attrs.Item1)
            {
                case ASTUtilities.AttributeType.HasAnnotation:
                    myASTUtilities.VerifyAttributes(context, context.Node, attrs.Item2, Rule, attributeName);
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
    }
}
