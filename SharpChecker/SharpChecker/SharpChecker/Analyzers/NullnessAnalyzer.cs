using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SharpChecker.Attributes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpChecker
{
    class NullnessAnalyzer : SCBaseAnalyzer
    {
        //Define the diagnostic for the Nullness type system
        private const string DiagnosticId = "NullnessChecker";
        private const string Title = "Error in attribute applications";
        private const string MessageFormat = "Attribute application error {0}";
        private const string Description = "There is a mismatch between the effective attribute and the one expected";
        private const string Category = "Syntax";
        private static DiagnosticDescriptor NullnessRule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

        /// <summary>
        /// Get the rules associated with this analysis
        /// </summary>
        /// <returns>NonNull and MaybeNull</returns>
        [return:NonNull]
        public override Dictionary<string, DiagnosticDescriptor> GetRules()
        {
            var dict = new Dictionary<string, DiagnosticDescriptor>
            {
                { nameof(NonNullAttribute).Replace("Attribute", ""), NullnessRule },
                { nameof(MaybeNullAttribute).Replace("Attribute", ""), NullnessRule }
            };
            Debug.Assert(dict != null, "dict:NonNull");
            return dict;
        }

        /// <summary>
        /// Override the analysis of invocation expressions to verify that when a variable is dereferenced
        /// it has the appropriate annotated type.  This allows us to present an error when a possibly
        /// null value is unsafely dereferenced.  We still want to exercise the functionality present in the
        /// base class, so we conclude by calling the method which we are overridding here.
        /// </summary>
        /// <param name="context">The analysis context</param>
        /// <param name="invocationExpr">The invocation expression</param>
        internal override void AnalyzeInvocationExpr(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocationExpr)
        {
            //We want to present a warning when we are dereferencing variable which may be null
            if (invocationExpr.Expression is MemberAccessExpressionSyntax memAccessExpr)
            {
                var identifierNameExpr = memAccessExpr.Expression;
                var symbol = context.SemanticModel.GetSymbolInfo(memAccessExpr.Expression).Symbol;
                if (symbol != null)
                {
                    var dereferencedAttrs = symbol.GetAttributes();
                    var filteredAttrs = ASTUtil.GetSharpCheckerAttributeStrings(dereferencedAttrs);
                    if (!ASTUtil.AnnotationDictionary.ContainsKey(identifierNameExpr) && filteredAttrs.Count() > 0)
                    {
                        ASTUtil.AnnotationDictionary.TryAdd(identifierNameExpr, new List<List<String>>() { filteredAttrs });
                    }
                }
            }

            //Now perform the normal collection of attributes
            base.AnalyzeInvocationExpr(context, invocationExpr);
        }

        /// <summary>
        /// This method is called during the init phase of an analysis and should be used to
        /// register attributes for analysis.
        /// </summary>
        /// <returns>NonNull and MaybeNull and the associated hierarchical relationship</returns>
        public override List<Node> GetAttributesToUseInAnalysis()
        {
            Node maybeNull = new Node() { AttributeName = nameof(MaybeNullAttribute).Replace("Attribute", "") };
            return new List<Node>() { maybeNull, new Node() { AttributeName = nameof(NonNullAttribute), Supertypes = new List<Node>() { maybeNull } } };
        }

        /// <summary>
        /// This is the link between the NullnessAnalyzer class and the NullnessSyntaxWalker class which will 
        /// verify the associated attributes.  
        /// </summary>
        /// <returns>The type NullnessSyntaxWalker</returns>
        public override Type GetSyntaxWalkerType()
        {
            return typeof(NullnessSyntaxWalker);
        }
    }
}
