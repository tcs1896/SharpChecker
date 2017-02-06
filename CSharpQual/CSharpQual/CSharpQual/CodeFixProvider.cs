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
using Microsoft.CodeAnalysis.Formatting;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace CSharpQual
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CSharpQualCodeFixProvider)), Shared]
    public class CSharpQualCodeFixProvider : CodeFixProvider
    {
        private const string title = "Fix String Format";

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(CSharpQualAnalyzer.DiagnosticId); }
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

            // Register a code action that will invoke the fix.
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: title,
                    createChangedDocument: c => FixStringFormatAsync(context.Document, invocationExpr, c),
                    equivalenceKey: title),
                diagnostic);
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
            int maxValue = CSharpQualAnalyzer.GetMaxValueInStringPattern(pattern);



            //SeparatedSyntaxList<ArgumentListSyntax> newArgs = new SeparatedSyntaxList<ArgumentListSyntax>();
            //string argumentString = $"\"{patternLiteral}\"";
            if (maxValue > 0)
            {
                SyntaxNodeOrToken[] args = new SyntaxNodeOrToken[maxValue + 2];
                args[0] = Argument(
                            LiteralExpression(
                                SyntaxKind.StringLiteralExpression,
                                Literal(patternLiteral.ToString())));

                for (int i = 1; i < maxValue + 2; i++)
                {
                    //argumentString += ", String.Empty";
                    //if(argumentList.Arguments.Count < i)
                    //{
                    //    //Add empty arguments to avoid a runtime exception
                    //    newLiteral.WithArguments()
                    //}
                    args[i] = Argument(
                            LiteralExpression(
                                SyntaxKind.StringLiteralExpression,
                                Literal("")));
                }
                try {
                    //var newLiteral = ExpressionStatement(
                    //                InvocationExpression(
                    //                    MemberAccessExpression(
                    //                        SyntaxKind.SimpleMemberAccessExpression,
                    //                        IdentifierName("String"),
                    //                        IdentifierName("Format"))).WithArgumentList(
                    //ArgumentList(SeparatedList<ArgumentSyntax>(args))));

                    //Allow the compiler to create the appropriate syntax nodes by parsing a string literal
                    var newLiteral = SyntaxFactory.ParseExpression("\"valid regex\"")
                        .WithLeadingTrivia(patternLiteral.GetLeadingTrivia())
                        .WithTrailingTrivia(patternLiteral.GetTrailingTrivia())
                        //Adding the "Formatter" annotation tells Roslyn that we have added nodes, and we 
                        //would like them formatted according to the user's style settings
                        .WithAdditionalAnnotations(Formatter.Annotation);

                    //Now we begin the process of replacing the old node with the new one
                    var root = await document.GetSyntaxRootAsync();
                    var newRoot = root.ReplaceNode(patternLiteral, newLiteral);
                    var newDocument = document.WithSyntaxRoot(newRoot);
                    return newDocument;
                }catch(Exception ex)
                {
                }
            }

            return document;
        }
    }
}