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
        public static DiagnosticDescriptor EncryptionRule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

        public override Dictionary<string, DiagnosticDescriptor> GetRules()
        {
            var baseRules = base.GetRules();
            baseRules.Add(nameof(EncryptedAttribute).Replace("Attribute", ""), EncryptionRule);
            return baseRules;
        }

        public override List<String> GetAttributesToUseInAnalysis()
        {
            var baseAttributes = base.GetAttributesToUseInAnalysis();
            baseAttributes.Add(nameof(EncryptedAttribute));
            return baseAttributes;
        }

    }
}
