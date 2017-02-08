using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using TestHelper;
using CSharpQual;

namespace CSharpQual.Test
{
    [TestClass]
    public class UnitTest : CodeFixVerifier
    {

        //No diagnostics expected to show up
        [TestMethod]
        public void TestMethod1()
        {
            var test = @"";

            VerifyCSharpDiagnostic(test);
        }

        //Diagnostic and CodeFix both triggered and checked for
        [TestMethod]
        public void TestMethod2()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSQSandbox
{
    class Program
    {
        static void Main(string[] args)
        {
            //This doesn't need to format anything
            Console.WriteLine(String.Format(""No value to replace""));
            //This should be fine
            Console.WriteLine(String.Format(""This {0} formatted {1}"", ""is a"", ""test""));
            //Here we have too many arguments
            Console.WriteLine(String.Format(""This {0} formatted {1}"", ""is a"", ""test"", ""more""));
            //The number of arguments here are fewer than necessary
            Console.WriteLine(String.Format(""This {0} formatted {1}"", ""is a""));
            //The tokens to replace are not numbered correctly
            Console.WriteLine(String.Format(""This {0} formatted {3}"", ""is a""));
        }
    }
}";
            var expected = new DiagnosticResult
            {
                Id = "CSharpQual",
                Message = String.Format("Type name '{0}' contains lowercase letters", "TypeName"),
                Severity = DiagnosticSeverity.Warning,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 11, 15)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);

            var fixtest = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
        class TYPENAME
        {   
        }
    }";
            VerifyCSharpFix(test, fixtest);
        }

        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new CSharpQualCodeFixProvider();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new CSharpQualAnalyzer();
        }
    }
}