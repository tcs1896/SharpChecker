using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestHelper;

namespace SharpChecker.Test
{
    [TestClass]
    public class NullnessTest : CodeFixVerifier
    {
        /// <summary>
        /// Override the appropriate method to pass in our analyzer
        /// </summary>
        /// <returns></returns>
        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new SharpCheckerDiagnosticAnalyzer();
        }

        public const string EncryptionProgStart = @"
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using System.Text;
            using System.Threading.Tasks;
            using System.Diagnostics;

            namespace EncryptedSandbox
            {
                class TypeName
                {   
                    [NonNull]
                    public string NonNullProp { get; set; }
                    [MaybeNull]
                    public string MaybeNullProp { get; set; }


                    static void Main(string[] args)
                    {";

        //We fill in and verify the body of the main method in the tests below

        private const string EncryptionProgEnd = @"

                    }
                }

                [AttributeUsage(AttributeTargets.All, Inherited = true, AllowMultiple = true)]
                public class NonNullAttribute : Attribute
                {
                }

                [AttributeUsage(AttributeTargets.All, Inherited = true, AllowMultiple = true)]
                public class MaybeNullAttribute : Attribute
                {
                }
            }";


        [TestMethod]
        public void NoDiagnosticsResult_AssignNullToMaybeNull()
        {
            var body = @"                

                //--Acceptable Cases--//
                MaybeNullProp = null;";

            var test = String.Concat(EncryptionProgStart, body, EncryptionProgEnd);

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void NoDiagnosticsResult_AssignStringLiteralToMaybeNull()
        {
            var body = @"                

                //--Acceptable Cases--//
                MaybeNullProp = ""literal"";";

            var test = String.Concat(EncryptionProgStart, body, EncryptionProgEnd);

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void NoDiagnosticsResult_AssignStringLiteralToNonnull()
        {
            var body = @"                

                //--Acceptable Cases--//
                NonNullProp = ""literal"";";

            var test = String.Concat(EncryptionProgStart, body, EncryptionProgEnd);

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void AssigningNullLiteralToNonNull()
        {
            var body = @"                
                //--Error Cases--//
                NonNullProp = null;";

            var test = String.Concat(EncryptionProgStart, body, EncryptionProgEnd);
            var diagLoc = new[] { new DiagnosticResultLocation("Test0.cs", 22, 31) };
            VerifyDiag(test, diagLoc);
        }

        /// <summary>
        /// This is a helper method to push the verification through the machinery provided by DiagnosticVerifier
        /// </summary>
        /// <param name="test"></param>
        /// <param name="diagLoc"></param>
        private void VerifyDiag(string test, DiagnosticResultLocation[] diagLoc)
        {
            var expected = new DiagnosticResult
            {
                Id = "NullnessChecker",
                Message = String.Format("Attribute application error {0}", "NonNull"),
                Severity = DiagnosticSeverity.Error,
                Locations = diagLoc
            };

            VerifyCSharpDiagnostic(test, expected);
        }
    }
}
