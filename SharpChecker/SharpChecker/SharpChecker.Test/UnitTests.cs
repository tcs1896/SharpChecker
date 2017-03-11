using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using TestHelper;
using SharpChecker;

namespace SharpChecker.Test
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
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
        [AttributeUsage(AttributeTargets.All, Inherited = true, AllowMultiple = true)]
        class EncryptedAttribute : Attribute
        {
        }

        class TypeName
        {   
            [Encrypted]
            public string Ciphertext { get; set; }

            public string RemoveSpecialChars(string original, int charCode)
            {
                // Remove the special characters
                return original;
            }

            static void Main(string[] args)
            {
                Ciphertext = RemoveSpecialChars(Ciphertext, 3);
            }
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = "SharpCheckerMethodParams",
                Message = String.Format("Attribute application error {0}", "EncryptedSandbox.EncryptedAttribute"),
                Severity = DiagnosticSeverity.Error,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 29, 30)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);

    //        var fixtest = @"
    //using System;
    //using System.Collections.Generic;
    //using System.Linq;
    //using System.Text;
    //using System.Threading.Tasks;
    //using System.Diagnostics;

    //namespace ConsoleApplication1
    //{
    //    class TYPENAME
    //    {   
    //    }
    //}";
    //        VerifyCSharpFix(test, fixtest);
        }

        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new SharpCheckerCodeFixProvider();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new SharpCheckerBaseAnalyzer();
        }
    }
}