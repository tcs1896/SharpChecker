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
    public class SharpCheckerAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "SharpCheckerMethodParams";
        internal const string Title = "Error in attribute applications";
        internal const string MessageFormat = "Attribute application error {0}";
        internal const string Description = "There is a mismatch between the attribute of the formal parameter and that of the argument";
        internal const string Category = "Syntax";

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            // TODO: Consider registering other actions that act on syntax instead of or in addition to symbols
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Analyzer%20Actions%20Semantics.md for more information
            context.RegisterSyntaxNodeAction<SyntaxKind>(AnalyzeNode, SyntaxKind.InvocationExpression);
        }

        private void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            //We can safely cast here because we filter above when we register
            var invocationExpr = (InvocationExpressionSyntax)context.Node;

            var identifierNameExpr = invocationExpr.Expression as IdentifierNameSyntax;
            //Attempt to grab a memberAccessExpr.  In this case Regex.Match
            //var memberAccessExpr = invocationExpr.Expression as MemberAccessExpressionSyntax;
            //Checking the syntax is fast, and this is preferred as an initial mechanism becauase analyzers
            //may be executed many times a second as text is entered into an editor
            //if (memberAccessExpr?.Name.ToString() != "Format") return;
            //if (memberAccessExpr == null) return;

            if (identifierNameExpr?.Identifier.ToString() != "sendOverInternet") return;

            //Now we know we have a 'Match' method invocation, so we incur the cost to get the associated symbol
            var memberSymbol = context.SemanticModel.GetSymbolInfo(identifierNameExpr).Symbol as IMethodSymbol;
            //If we are dealing with the correct namespace then bail
            if (!memberSymbol?.ToString().StartsWith("EncryptedSandbox.Program.sendOverInternet") ?? true) return;
            //Grab the argument list so we can interrogate it
            var argumentList = invocationExpr.ArgumentList as ArgumentListSyntax;

            //Check to see if any of the formal parameters of the method being invoked have associated attributes
            //In order to do this, we need to lookup the appropriate method signature.  However, given dynamic
            //dispatch it doesn't seem possible to lookup the specific instance

            //This might be used to the attributes of a method
            //memberSymbol.GetAttributes();

            ImmutableArray<IParameterSymbol> paramSymbols = memberSymbol.Parameters;
            foreach (var param in paramSymbols)
            {
                var attributes = param.GetAttributes();

                foreach (var attr in attributes)
                {
                    var myName = $"The attr {attr.AttributeClass}";
                    if (attr.AttributeClass.ToString() == "EncryptedSandbox.EncryptedAttribute")
                    {
                        bool hitLogic = true;
                    }

                    //EncryptedAttribute encrAttr = attr as EncryptedAttribute;
                    //if (encrAttr != null)
                    //{
                    //    Console.WriteLine($"Method {met.Name} has parameter {mArg.Name} with the {nameof(EncryptedAttr)}");
                    //}
                }
            };

            ////Make sure the first argument is a string literal.
            //var patternLiteral = argumentList.Arguments[0].Expression as LiteralExpressionSyntax;
            //if (patternLiteral == null) return;
            ////Now that we know its a literal we can retrieve the value
            //var patternOpt = context.SemanticModel.GetConstantValue(patternLiteral);
            //if (!patternOpt.HasValue) return;
            //var pattern = patternOpt.Value as string;
            //if (pattern == null) return;

            ////Dig into the first argument to see how many arguments are expected in the surface langauge
            //int maxValue = 0;
            //maxValue = 1;
            ////Now that we know the maximum value, check to make sure we have exactly that number of
            ////arguments in addition to the one which specifies the pattern
            //if (argumentList.Arguments.Count != (maxValue + 2))
            //{
            //    //Create the appropriate diagnostic, span for the token we want to underline, and message
            //    var diagnostic =
            //        Diagnostic.Create(Rule,
            //        patternLiteral.GetLocation(), Description);
            //    //Now we register this diagnostic with visual studio
            //    context.ReportDiagnostic(diagnostic);
            //}
        }
    }
}
