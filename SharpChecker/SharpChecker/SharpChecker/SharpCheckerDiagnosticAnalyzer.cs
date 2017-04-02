using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.Text;
using System.Xml.Linq;

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
                // The mechanism for pulling in additional files and reading the xml content was based on the sample
                // provided in the roslyn source: https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Using%20Additional%20Files.md

                // Find the file with the checkers we are enabling
                ImmutableArray<AdditionalText> additionalFiles = compilationContext.Options.AdditionalFiles;
                AdditionalText checkersFile = additionalFiles.FirstOrDefault(file => Path.GetFileName(file.Path).Equals("checkers.xml"));

                if (checkersFile != null)
                {
                    List<string> checkers = new List<string>();
                    SourceText fileText = checkersFile.GetText(compilationContext.CancellationToken);

                    MemoryStream stream = new MemoryStream();
                    using (StreamWriter writer = new StreamWriter(stream, System.Text.Encoding.UTF8, 1024, true))
                    {
                        fileText.Write(writer);
                    }

                    stream.Position = 0;

                    // Read all the <Term> elements to get the terms.
                    XDocument document = XDocument.Load(stream);
                    foreach (XElement termElement in document.Descendants("Checker"))
                    {
                        checkers.Add(termElement.Value);
                    }

                    //Perform any setup necessary for our analysis in the constructor
                    var analyzer = new ASTUtilities(checkers);

                    //Subscribe to be notified when syntax node actions are fired for the types of syntax nodes which we will analyze
                    compilationContext.RegisterSyntaxNodeAction<SyntaxKind>(analyzer.AnalyzeExpression, analyzer.GetSyntaxKinds());

                    //Register an end action to report diagnostics based on the final state.  There is some risk
                    //in using this action because it is not gauranteed to fire after all of the syntax node actions.
                    //The CompilationEndAction was initially used, which does provide this gaurantee, but does not 
                    //fire when "full solution anlysis" is not enabled in Visual Studio.  This is not enabled by default.
                    compilationContext.RegisterSemanticModelAction(analyzer.VerifyTypeAnnotations);
                }
            });
        }
    }
}
