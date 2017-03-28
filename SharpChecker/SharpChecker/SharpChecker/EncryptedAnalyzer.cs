using Microsoft.CodeAnalysis;
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
        //This is the default diagnostic
        public const string DiagnosticId = "EncryptionChecker";
        internal const string Title = "Error in attribute applications";
        internal const string MessageFormat = "Attribute application error {0}";
        internal const string Description = "There is a mismatch between the effective attribute and the one expected";
        internal const string Category = "Syntax";
        public static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

        public override SCBaseAnalyzer AnalyzerFactory()
        {
            return this;
        }

        public override ImmutableArray<DiagnosticDescriptor> GetRules()
        {
            return ImmutableArray.Create(Rule);
        }

    }
}
