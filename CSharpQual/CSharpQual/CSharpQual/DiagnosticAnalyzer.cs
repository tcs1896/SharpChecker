using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Text.RegularExpressions;

namespace CSharpQual
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class CSharpQualAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "CSQ_StringFormat";
        internal const string Title = "Error in string format";
        internal const string MessageFormat = "String format error {0}";
        internal const string Description = "The number of arguments should match those referenced in the string.";
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
            //Attempt to grab a memberAccessExpr.  In this case Regex.Match
            var memberAccessExpr = invocationExpr.Expression as MemberAccessExpressionSyntax;
            //Checking the syntax is fast, and this is preferred as an initial mechanism becauase analyzers
            //may be executed many times a second as text is entered into an editor
            if (memberAccessExpr?.Name.ToString() != "Format") return;
            //Now we know we have a 'Match' method invocation, so we incur the cost to get the associated symbol
            var memberSymbol = context.SemanticModel.GetSymbolInfo(memberAccessExpr).Symbol as IMethodSymbol;
            //If we are dealing with the correct namespace then bail
            if (!memberSymbol?.ToString().StartsWith("string.Format") ?? true) return;
            //Grab the argument list so we can interrogate it
            var argumentList = invocationExpr.ArgumentList as ArgumentListSyntax;
            //Make sure the first argument is a string literal.
            var patternLiteral = argumentList.Arguments[0].Expression as LiteralExpressionSyntax;
            if (patternLiteral == null) return;
            //Now that we know its a literal we can retrieve the value
            var patternOpt = context.SemanticModel.GetConstantValue(patternLiteral);
            if (!patternOpt.HasValue) return;
            var pattern = patternOpt.Value as string;
            if (pattern == null) return;

            //Dig into the first argument to see how many arguments are expected in the surface langauge
            int maxValue = 0;
            maxValue = GetMaxValueInStringPattern(pattern);
            //Now that we know the maximum value, check to make sure we have exactly that number of
            //arguments in addition to the one which specifies the pattern
            if (argumentList.Arguments.Count != (maxValue + 2))
            {
                //Create the appropriate diagnostic, span for the token we want to underline, and message
                var diagnostic =
                    Diagnostic.Create(Rule,
                    patternLiteral.GetLocation(), Description);
                //Now we register this diagnostic with visual studio
                context.ReportDiagnostic(diagnostic);
            }
        }

        public static int GetMaxValueInStringPattern(string pattern)
        {
            int maxValue = 0;

            foreach (Match m in Regex.Matches(pattern, "{.*?}"))
            {
                string stringMatch = m.Value.Replace("{", String.Empty).Replace("}", String.Empty);
                int thisValue;
                if (Int32.TryParse(stringMatch, out thisValue))
                {
                    if (thisValue > maxValue)
                    {
                        maxValue = thisValue;
                    }
                }
            }

            return maxValue;
        }
    }
}
