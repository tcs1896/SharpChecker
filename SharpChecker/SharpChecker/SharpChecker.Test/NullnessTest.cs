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

        public const string ProgStart = @"
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

        private const string ProgEnd = @"

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
                //This property has an attribute which indicates it may be null, so assigning
                //null should not result in a diagnostic
                MaybeNullProp = null;";

            var test = String.Concat(ProgStart, body, ProgEnd);

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void NoDiagnosticsResult_AssignStringLiteralToMaybeNull()
        {
            var body = @"                

                //--Acceptable Cases--//
                //Assigning a string literal should be acceptable regardless of the nullability
                MaybeNullProp = ""literal"";";

            var test = String.Concat(ProgStart, body, ProgEnd);

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void NoDiagnosticsResult_AssignStringLiteralToNonnull()
        {
            var body = @"                

                //--Acceptable Cases--//
                //Assigning a string literal should be acceptable regardless of the nullability
                NonNullProp = ""literal"";";

            var test = String.Concat(ProgStart, body, ProgEnd);

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void AssigningNullLiteralToNonNull()
        {
            var body = @"                
                //--Error Cases--//
                //Assigning null to a property which has the NonNull attribute should result in a diagnostic
                NonNullProp = null;";

            var test = String.Concat(ProgStart, body, ProgEnd);
            var diagLoc = new[] { new DiagnosticResultLocation("Test0.cs", 23, 31) };
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
