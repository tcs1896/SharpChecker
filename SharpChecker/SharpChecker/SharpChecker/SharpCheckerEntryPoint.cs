using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.Text;
using System.Xml.Linq;
using System;
using System.Diagnostics;
using System.Threading;

namespace SharpChecker
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class SharpCheckerEntryPoint : DiagnosticAnalyzer
    {
        /// <summary>
        /// Get our list of diagnostics from the Checkers
        /// </summary>
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ASTUtilities.GetRules(); } }

        /// <summary>
        /// The entry point of the analysis.  This fires once per session, which in a batch processing
        /// mode, corresponds to one compilation.
        /// </summary>
        /// <param name="context">The analysis context</param>
        public override void Initialize(AnalysisContext context)
        {
            context.RegisterCompilationStartAction(compilationContext =>
            {
                //Retrieve the checkers which the target code has identified as active
                List<string> checkers = GetCheckersFromAdditionalFiles(compilationContext.Options.AdditionalFiles, compilationContext.CancellationToken);
                
                //Perform any setup necessary for our analysis in the constructor
                var analyzer = new ASTUtilities(checkers);

                //Subscribe to be notified when syntax node actions are fired for the types of syntax nodes which we will analyze
                compilationContext.RegisterSyntaxNodeAction<SyntaxKind>(analyzer.AnalyzeExpression, analyzer.GetSyntaxKinds());

                //Register an end action to report diagnostics based on the final state.  There is some risk
                //in using this action because it is not gauranteed to fire after all of the syntax node actions.
                //The CompilationEndAction was initially used, which does provide this gaurantee, but does not 
                //fire when "full solution anlysis" is not enabled in Visual Studio.  This is not enabled by default.
                compilationContext.RegisterSemanticModelAction(analyzer.VerifyTypeAnnotations);
            });
        }

        /// <summary>
        /// This method searches for a file called checkers.xml in the project being analyzed.  It must be 
        /// identifed as an "AdditionalFiles" node in the csproj xml.
        /// 
        /// The mechanism for pulling in additional files and reading the xml content was based on the sample
        /// provided in the roslyn source: https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Using%20Additional%20Files.md
        /// </summary>
        /// <param name="additionalFiles">The additional files present in the target code</param>
        /// <param name="cancellationToken">The analysis cancellation token</param>
        /// <returns></returns>
        private List<string> GetCheckersFromAdditionalFiles(ImmutableArray<AdditionalText> additionalFiles, CancellationToken cancellationToken)
        {
            // The list of checkers to be returned
            List<string> checkers = new List<string>();
            // Find the file with the checkers we are enabling
            AdditionalText checkersFile = additionalFiles.FirstOrDefault(file => Path.GetFileName(file.Path).Equals("checkers.xml"));

            if (checkersFile != null)
            {
                SourceText fileText = checkersFile.GetText(cancellationToken);

                MemoryStream stream = new MemoryStream();
                using (StreamWriter writer = new StreamWriter(stream, System.Text.Encoding.UTF8, 1024, true))
                {
                    fileText.Write(writer);
                }

                stream.Position = 0;

                try
                {
                    // Read all the <Checker> elements to get the checkers.
                    XDocument document = XDocument.Load(stream);
                    foreach (XElement termElement in document.Descendants("Checker"))
                    {
                        checkers.Add(termElement.Value);
                    }
                }
                catch (Exception ex)
                {
                    //If we are unable to parse the document, then we may be editing it currently.
                    Debug.WriteLine($"Error reading the 'checkers.xml' document.  Exception: {ex.Message}");
                }
            }

            return checkers;
        }
    }
}
