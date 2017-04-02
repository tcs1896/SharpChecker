using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpChecker.Test
{
    /// <summary>
    /// This was pulled from a stack overflow response:
    /// http://stackoverflow.com/questions/34047123/how-to-add-additional-files-to-an-ad-hoc-roslyn-workspace-to-expose-them-to-anal
    /// </summary>
    public sealed class AnalyzerAdditionalFile : AdditionalText
    {
        private readonly string path;

        public AnalyzerAdditionalFile(string path)
        {
            this.path = path;
        }

        //Mask the real value of the path so that we can use different files for different unit tests
        public override string Path => "checkers.xml";

        public override SourceText GetText(CancellationToken cancellationToken)
        {
            return SourceText.From(File.ReadAllText(path));
        }
    }
}
