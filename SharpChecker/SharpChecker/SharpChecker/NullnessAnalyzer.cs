using Microsoft.CodeAnalysis;
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
        public static DiagnosticDescriptor NullnessRule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

        public override Dictionary<string, DiagnosticDescriptor> GetRules()
        {
            var baseRules = base.GetRules();
            baseRules.Add(nameof(MaybeNullAttribute).Replace("Attribute", ""), NullnessRule);
            baseRules.Add(nameof(NonNullAttribute).Replace("Attribute", ""), NullnessRule);
            return baseRules;
        }

        public override List<String> GetAttributesToUseInAnalysis()
        {
            var baseAttributes = base.GetAttributesToUseInAnalysis();
            baseAttributes.Add(nameof(MaybeNullAttribute));
            baseAttributes.Add(nameof(NonNullAttribute));
            return baseAttributes;
        }
    }
}
