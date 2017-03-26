using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SharpChecker
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class SharpCheckerDiagnosticAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>
        /// Get our list of diagnostics from the Checkers
        /// </summary>
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ASTUtilities.GetRules(); } }

        /// <summary>
        /// The entry point of the analysis.  This fires once per session, which in a batch processing
        /// mode, corresponds to one compilation.
        /// </summary>
        /// <param name="context"></param>
        public override void Initialize(AnalysisContext context)
        {
            context.RegisterCompilationStartAction(compilationContext =>
            {
                //Perform any setup necessary for our analysis in the constructor
                var analyzer = new ASTUtilities();

                //Subscribe to be notified when syntax node actions are fired for the types of syntax nodes which we will analyze
                compilationContext.RegisterSyntaxNodeAction<SyntaxKind>(analyzer.AnalyzeExpression, ASTUtilities.GetSyntaxKinds()); 

                //Register an end action to report diagnostics based on the final state.  There is some risk
                //in using this action because it is not gauranteed to fire after all of the syntax node actions.
                //The CompilationEndAction was initially used, which does provide this gaurantee, but does not 
                //fire when "full solution anlysis" is not enabled in Visual Studio.  This is not enabled by default.
                compilationContext.RegisterSemanticModelAction(analyzer.VerifyTypeAnnotations);
            });
        }
    }
}
