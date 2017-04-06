using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SharpChecker.attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpChecker
{
    class NullnessAnalyzer : EncryptedAnalyzer
    {
        private const string DiagnosticId = "NullnessChecker";
        private const string Title = "Error in attribute applications";
        private const string MessageFormat = "Attribute application error {0}";
        private const string Description = "There is a mismatch between the effective attribute and the one expected";
        private const string Category = "Syntax";
        private static DiagnosticDescriptor NullnessRule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

        public override Dictionary<string, DiagnosticDescriptor> GetRules()
        {
            var dict = new Dictionary<string, DiagnosticDescriptor>
            {
                { nameof(NonNullAttribute).Replace("Attribute", ""), NullnessRule },
                { nameof(MaybeNullAttribute).Replace("Attribute", ""), NullnessRule }
            };
            return dict;
        }

        internal override void AnalyzeInvocationExpr(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocationExpr)
        {
            //We want to present a warning when we are dereferencing variable which may be null
            //TODO: Should we assume that variables without a attributes may be null
            if (invocationExpr.Expression is MemberAccessExpressionSyntax memAccessExpr)
            {
                var identifierNameExpr = memAccessExpr.Expression;
                var dereferencedAttrs = context.SemanticModel.GetSymbolInfo(memAccessExpr.Expression).Symbol.GetAttributes();
                var filteredAttrs = ASTUtil.GetSharpCheckerAttributeStrings(dereferencedAttrs);
                if (!ASTUtil.AnnotationDictionary.ContainsKey(identifierNameExpr) && filteredAttrs.Count() > 0)
                {
                    ASTUtil.AnnotationDictionary.TryAdd(identifierNameExpr, new List<List<String>>() { filteredAttrs });
                }
            }

            //Now perform the normal collection of attributes
            base.AnalyzeInvocationExpr(context, invocationExpr);
        }

        public override List<Node> GetAttributesToUseInAnalysis()
        {
            //TODO: Should have to hard code "MaybeNull" string - we currently are truncating the "Attribute" off of the items in the supertypes collection
            Node maybeNull = new Node() { AttributeName = "MaybeNull" };
            return new List<Node>() { maybeNull, new Node() { AttributeName = nameof(NonNullAttribute), Supertypes = new List<Node>() { maybeNull } } };
        }

        public override Type GetSyntaxWalkerType()
        {
            return typeof(NullnessSyntaxWalker);
        }
    }
}
