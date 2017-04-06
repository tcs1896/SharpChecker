using Microsoft.CodeAnalysis;
using SharpChecker.attributes;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpChecker
{
    class EncryptedAnalyzer : SCBaseAnalyzer
    {
        private const string DiagnosticId = "EncryptionChecker";
        private const string Title = "Error in attribute applications";
        private const string MessageFormat = "Attribute application error {0}";
        private const string Description = "There is a mismatch between the effective attribute and the one expected";
        private const string Category = "Syntax";
        private static DiagnosticDescriptor EncryptionRule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

        public override Dictionary<string, DiagnosticDescriptor> GetRules()
        {
            var dict = new Dictionary<string, DiagnosticDescriptor>
            {
                { nameof(EncryptedAttribute).Replace("Attribute", ""), EncryptionRule }
            };
            return dict;
        }

        public override List<Node> GetAttributesToUseInAnalysis()
        {
            return new List<Node>() { new Node() { AttributeName = nameof(EncryptedAttribute) } };
        }

    }
}
