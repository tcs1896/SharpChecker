using Microsoft.CodeAnalysis;
using SharpChecker.Attributes;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpChecker
{
    class EncryptedAnalyzer : SCBaseAnalyzer
    {
        //Define the diagnostic for the Encrypted type system
        private const string DiagnosticId = "EncryptionChecker";
        private const string Title = "Error in attribute applications";
        private const string MessageFormat = "Attribute application error {0}";
        private const string Description = "There is a mismatch between the effective attribute and the one expected";
        private const string Category = "Syntax";
        private static DiagnosticDescriptor EncryptionRule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

        /// <summary>
        /// Get the rules associated with this analysis
        /// </summary>
        /// <returns>Encrypted</returns>
        [return: NonNull]
        public override Dictionary<string, DiagnosticDescriptor> GetRules()
        {
            var dict = new Dictionary<string, DiagnosticDescriptor>
            {
                { nameof(EncryptedAttribute).Replace("Attribute", ""), EncryptionRule }
            };
            Debug.Assert(dict != null, "dict:NonNull");
            return dict;
        }

        /// <summary>
        /// This method is called during the init phase of an analysis and should be used to
        /// register attributes for analysis.
        /// </summary>
        /// <returns>Encrypted</returns>
        public override List<Node> GetAttributesToUseInAnalysis()
        {
            return new List<Node>() { new Node() { AttributeName = nameof(EncryptedAttribute) } };
        }

    }
}
