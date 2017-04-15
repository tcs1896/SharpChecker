using Microsoft.CodeAnalysis;
using SharpChecker.Attributes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpChecker
{
    class TaintedAnalyzer : SCBaseAnalyzer
    {
        private const string DiagnosticId = "TaintedChecker";
        private const string Title = "Error in attribute applications";
        private const string MessageFormat = "Attribute application error {0}";
        private const string Description = "There is a mismatch between the effective attribute and the one expected";
        private const string Category = "Syntax";
        private static DiagnosticDescriptor TaintedRule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

        [return: NonNull]
        public override Dictionary<string, DiagnosticDescriptor> GetRules()
        {
            var dict = new Dictionary<string, DiagnosticDescriptor>
            {
                { nameof(TaintedAttribute).Replace("Attribute", ""), TaintedRule },
                { nameof(UntaintedAttribute).Replace("Attribute", ""), TaintedRule }
            };
            Debug.Assert(dict != null, "dict:NonNull");
            return dict;
        }

        public override List<Node> GetAttributesToUseInAnalysis()
        {
            Node tainted = new Node() { AttributeName = "Tainted" };
            return new List<Node>() { tainted, new Node() { AttributeName = nameof(UntaintedAttribute), Supertypes = new List<Node>() { tainted } } };
        }

        public override Type GetSyntaxWalkerType()
        {
            return typeof(TaintedSyntaxWalker);
        }
    }
}
