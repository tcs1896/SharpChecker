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
        private const string attributeName = "EncryptedSandbox.EncryptedAttribute";
        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction<SyntaxKind>(AnalyzeNode, SyntaxKind.InvocationExpression);
        }

        private void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            //We can safely cast here because we filter above when we register
            var invocationExpr = (InvocationExpressionSyntax)context.Node;

            var identifierNameExpr = invocationExpr.Expression as IdentifierNameSyntax;
            //Attempt to grab a memberAccessExpr.  In this case Regex.Match
            //var memberAccessExpr = invocationExpr.Expression as MemberAccessExpressionSyntax;
            //Checking the syntax is fast, and this is preferred as an initial mechanism because analyzers
            //may be executed many times a second as text is entered into an editor
            //if (memberAccessExpr?.Name.ToString() != "Format") return;
            //if (memberAccessExpr == null) return;

            if (identifierNameExpr?.Identifier.ToString() != "sendOverInternet") return;

            //This will lookup the method associated with the invocation expression
            var memberSymbol = context.SemanticModel.GetSymbolInfo(identifierNameExpr).Symbol as IMethodSymbol;
            //If we are not dealing with the correct namespace then bail
            if (!memberSymbol?.ToString().StartsWith("EncryptedSandbox.Program.sendOverInternet") ?? true) return;


            //Check to see if any of the formal parameters of the method being invoked have associated attributes
            //In order to do this, we need to lookup the appropriate method signature.  However, given dynamic
            //dispatch it doesn't seem possible to lookup the specific instance

            //This might be used to the attributes of a method
            //memberSymbol.GetAttributes();

            ImmutableArray<IParameterSymbol> paramSymbols = memberSymbol.Parameters;
            //Iterate over the parameters with an explicit index so we can compare the appropriate argument below
            for(int i = 0; i < paramSymbols.Count(); i++)
            {
                //Get the formal parameter
                var param = paramSymbols[i];

                var attributes = param.GetAttributes();

                foreach (var attr in attributes)
                {
                    //var myName = $"The attr {attr.AttributeClass}";
                    if (attr.AttributeClass.ToString() == attributeName)
                    {
                        //Locate the argument which corresponds to the one with the attribute and 
                        //ensure that it also has the attribute
                        bool foundMatch = false;

                        //Grab the argument list so we can interrogate it
                        var argumentList = invocationExpr.ArgumentList as ArgumentListSyntax;
                        //It may seem unnecessary to check that i is valid, since providing the incorrect number of arguments should not
                        //type check.  However, there is some candidate analysis while the code is incomplete.
                        if (i < argumentList.Arguments.Count())
                        {
                            //Here we are handling the case where the argument is an identifier
                            var argI = argumentList.Arguments[i].Expression as IdentifierNameSyntax;
                            //var argSymbol = context.SemanticModel.GetDeclaredSymbol(argumentList.Arguments[0]); //GetSymbolInfo(argumentList.Arguments[0]).Symbol as IPropertySymbol;

                            if (argI != null)
                            {
                                SymbolInfo info = context.SemanticModel.GetSymbolInfo(argI);
                                ISymbol symbol = info.Symbol;
                                if (symbol != null)
                                {
                                    var argAttrs = symbol.GetAttributes();

                                    foreach (var argAttr in argAttrs)
                                    {
                                        if (attr.AttributeClass.ToString() == attributeName)
                                        {
                                            foundMatch = true;
                                        }
                                    }

                                    //If we haven't found a match then present a diagnotic error
                                    if (!foundMatch)
                                    {
                                        var diagnostic = Diagnostic.Create(Rule, argI.GetLocation(), Description);
                                        //Now we register this diagnostic with visual studio
                                        context.ReportDiagnostic(diagnostic);
                                    }
                                }
                            }
                            else
                            {
                                //We are probably dealing with a literal - which cannot have the associated attribute
                                var argLit = argumentList.Arguments[i].Expression as LiteralExpressionSyntax;
                                if (argLit != null)
                                {
                                    var diagnostic = Diagnostic.Create(Rule, argLit.GetLocation(), Description);
                                    //Now we register this diagnostic with visual studio
                                    context.ReportDiagnostic(diagnostic);
                                }
                            }
                        }
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
