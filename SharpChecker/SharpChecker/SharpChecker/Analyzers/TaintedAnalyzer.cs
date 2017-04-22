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
        //Define the diagnostic for the Tainted type system
        private const string DiagnosticId = "TaintedChecker";
        private const string Title = "Error in attribute applications";
        private const string MessageFormat = "Attribute application error {0}";
        private const string Description = "There is a mismatch between the effective attribute and the one expected";
        private const string Category = "Syntax";
        private static DiagnosticDescriptor TaintedRule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

        /// <summary>
        /// Get the rules associated with this analysis
        /// </summary>
        /// <returns>Tainted and Untainted</returns>
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

        /// <summary>
        /// This method is called during the init phase of an analysis and should be used to
        /// register attributes for analysis.
        /// </summary>
        /// <returns>Tainted and Untainted and the associated hierarchical relationship</returns>
        public override List<Node> GetAttributesToUseInAnalysis()
        {
            Node tainted = new Node() { AttributeName = nameof(TaintedAttribute).Replace("Attribute", "") };
            return new List<Node>() { tainted, new Node() { AttributeName = nameof(UntaintedAttribute), Supertypes = new List<Node>() { tainted } } };
        }

        /// <summary>
        /// This is the link between the TaintedAnalyzer class and the TaintedSyntaxWalker class which will 
        /// verify the associated attributes.  
        /// </summary>
        /// <returns>The type TaintedSyntaxWalker</returns>
        public override Type GetSyntaxWalkerType()
        {
            return typeof(TaintedSyntaxWalker);
        }
    }
}
