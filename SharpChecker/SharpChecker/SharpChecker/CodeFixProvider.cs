using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SharpChecker
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SharpCheckerCodeFixProvider)), Shared]
    public class SharpCheckerCodeFixProvider : CodeFixProvider
    {
        private const string title = "Fix String Format";

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(SharpCheckerAnalyzer.DiagnosticId); }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            // TODO: Replace the following code with your own analysis, generating a CodeAction for each fix to suggest
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Find the type invocationExpression identified by the diagnostic.
            var invocationExpr = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<InvocationExpressionSyntax>().First();

            //// Register a code action that will invoke the fix.
            //context.RegisterCodeFix(
            //    CodeAction.Create(
            //        title: title,
            //        createChangedDocument: c => FixStringFormatAsync(context.Document, invocationExpr, c),
            //        equivalenceKey: title),
            //    diagnostic);
        }

        private async Task<Document> FixStringFormatAsync(Document document, InvocationExpressionSyntax invocationExpr, CancellationToken cancellationToken)
        {
            //This code is largely the same as that in the analyzer, except we already know we have
            //all the required elements because our analyzer was triggered, and so we can remove 
            //all the conditional logic.
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var memberAccessExpr =
              invocationExpr.Expression as MemberAccessExpressionSyntax;
            var memberSymbol =
              semanticModel.GetSymbolInfo(memberAccessExpr).Symbol as IMethodSymbol;
            var argumentList = invocationExpr.ArgumentList as ArgumentListSyntax;
            var patternLiteral = argumentList.Arguments[0].Expression as LiteralExpressionSyntax;
            var patternOpt = semanticModel.GetConstantValue(patternLiteral);
            var pattern = patternOpt.Value as string;
            int maxValue = 1;

            //If the pattern in the String.Format statement has no tokens to replace we do not present an error
            if (maxValue > 0)
            {
                //Add the pattern value back to the list of arguments
                SeparatedSyntaxList<ArgumentSyntax> args = new SeparatedSyntaxList<ArgumentSyntax>();
                args = args.Add(Argument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(pattern))));

                //Repopulate the arguments, and add empty strings for any additional replacment tokens in the pattern
                for (int i = 1; i < maxValue + 2; i++)
                {
                    if (i < argumentList.Arguments.Count)
                    {
                        var argLiteral = argumentList.Arguments[i].Expression as LiteralExpressionSyntax;
                        var argOpt = semanticModel.GetConstantValue(argLiteral);
                        var arg = argOpt.Value as string;
                        args = args.Add(Argument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(arg))));
                    }
                    else
                    {
                        args = args.Add(Argument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(""))));
                    }
                }

                //Create a new immutable document by instantiating a new one with the new argument list
                ArgumentListSyntax newArgumentListSyntax = argumentList.WithArguments(args);
                var root = await document.GetSyntaxRootAsync();
                var newRoot = root.ReplaceNode(argumentList, newArgumentListSyntax);
                var newDocument = document.WithSyntaxRoot(newRoot);
                return newDocument;
            }

            return document;
        }
    }
}