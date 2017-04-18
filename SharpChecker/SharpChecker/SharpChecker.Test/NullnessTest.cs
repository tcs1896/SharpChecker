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
            return new SharpCheckerEntryPoint();
        }

        public string CheckersFilename { get => "nullness.xml"; } 

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
                    public static string NonNullProp { get; set; }
                    [MaybeNull]
                    public static string MaybeNullProp { get; set; }
                    public static string NoAttrProp { get; set; }

                    static void Main(string[] args)
                    {";

        //We fill in and verify the body of the main method in the tests below

        private const string ProgEnd = @"

                    }

                    public void PrintGreeting([NonNull] string greeting)
                    {
                        Console.WriteLine($""Hello {greeting}"");
                    }

                    public string Self(string self)
                    {
                        return self;
                    }

                    [return:NonNull]
                    public string NonNullSelf()
                    {
                        string self = ""Test"";
                        Debug.Assert(self != null, ""self:NonNull"");
                        return self;
                    }

                    public TypeName Create()
                    {
                        return new TypeName();
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

            VerifyCSharpDiagnostic(test, CheckersFilename);
        }

        [TestMethod]
        public void NoDiagnosticsResult_UseMaybeNullWithinGuardedBlock()
        {
            var body = @"                

                //--Acceptable Cases--//
                //If we have checked explicitly for null, then no diagnostic should be presented
                if(MaybeNullProp != null)
                {
                    PrintGreeting(MaybeNullProp);
                }";

            var test = String.Concat(ProgStart, body, ProgEnd);

            VerifyCSharpDiagnostic(test, CheckersFilename);
        }

        [TestMethod]
        public void NoDiagnosticsResult_AssignStringLiteralToMaybeNull()
        {
            var body = @"                
                //--Acceptable Cases--//
                //Assigning a string literal should be acceptable regardless of the nullability
                MaybeNullProp = ""literal"";";

            var test = String.Concat(ProgStart, body, ProgEnd);
            
            VerifyCSharpDiagnostic(test, CheckersFilename);
        }

        [TestMethod]
        public void NoDiagnosticsResult_NullPropogatingOperator()
        {
            var body = @"                
                //--Acceptable Cases--//
                TypeName tn = new TypeName();
                NoAttrProp = tn.Self()?.ToString();";

            var test = String.Concat(ProgStart, body, ProgEnd);

            VerifyCSharpDiagnostic(test, CheckersFilename);
        }

        [TestMethod]
        public void NoDiagnosticsResult_NullPropogatingOperatorWithAttr()
        {
            var body = @"                
                //--Acceptable Cases--//
                var tn = new TypeName();
                NonNullProp = tn.Create()?.NonNullSelf();";

            var test = String.Concat(ProgStart, body, ProgEnd);

            VerifyCSharpDiagnostic(test, CheckersFilename);
        }

        [TestMethod]
        public void AssigningToNullPropogatingOperatorResultWithoutAttr()
        {
            var body = @"                
                //--Error Cases--//
                TypeName tn = new TypeName();
                NonNullProp = tn.Self()?.Self();";

            var test = String.Concat(ProgStart, body, ProgEnd);
            var diagLoc = new[] { new DiagnosticResultLocation("Test0.cs", 23, 31) };
            VerifyDiag(test, diagLoc);
        }

        [TestMethod]
        public void NoDiagnosticsResult_AssignNonNullToMaybeNull()
        {
            var body = @"                
                //--Acceptable Cases--//
                //Assigning a string literal should be acceptable regardless of the nullability  
                MaybeNullProp = NonNullProp;";

            var test = String.Concat(ProgStart, body, ProgEnd);

            VerifyCSharpDiagnostic(test, CheckersFilename);
        }

        [TestMethod]
        public void AssigningMaybeNullToNonNull()
        {
            var body = @"                
                //--Error Cases--//
                //Assigning null to a property which has the NonNull attribute should result in a diagnostic
                NonNullProp = MaybeNullProp;";

            var test = String.Concat(ProgStart, body, ProgEnd);
            var diagLoc = new[] { new DiagnosticResultLocation("Test0.cs", 23, 31) };
            VerifyDiag(test, diagLoc);
        }

        [TestMethod]
        public void NoDiagnosticsResult_AssignStringLiteralToNonnull()
        {
            var body = @"                

                //--Acceptable Cases--//
                //Assigning a string literal should be acceptable regardless of the nullability
                NonNullProp = ""literal"";";

            var test = String.Concat(ProgStart, body, ProgEnd);

            VerifyCSharpDiagnostic(test, CheckersFilename);
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
        
        [TestMethod]
        public void DereferencingMaybeNull()
        {
            var body = @"                
                //--Error Cases--//
                //Dereferencing a property which may be null should result in a diagnostic
                MaybeNullProp.ToLower();";

            var test = String.Concat(ProgStart, body, ProgEnd);
            var diagLoc = new[] { new DiagnosticResultLocation("Test0.cs", 23, 17) };
            VerifyDiag(test, diagLoc, "MaybeNull");
        }

        [TestMethod]
        public void PassNullLiteralAsNonNullArgument()
        {
            var body = @"                
                //--Error Cases--//
                //Passing null as an argument to a method which expects a NonNull value should result in a diagnostic
                PrintGreeting(null);";

            var test = String.Concat(ProgStart, body, ProgEnd);
            var diagLoc = new[] { new DiagnosticResultLocation("Test0.cs", 23, 31) };
            VerifyDiag(test, diagLoc);
        }

        /// <summary>
        /// This is a helper method to push the verification through the machinery provided by DiagnosticVerifier
        /// </summary>
        /// <param name="test"></param>
        /// <param name="diagLoc"></param>
        private void VerifyDiag(string test, DiagnosticResultLocation[] diagLoc, string attr = "NonNull")
        {
            var expected = new DiagnosticResult
            {
                Id = "NullnessChecker",
                Message = String.Format("Attribute application error {0}", attr),
                Severity = DiagnosticSeverity.Error,
                Locations = diagLoc
            };

            VerifyCSharpDiagnostic(test, CheckersFilename, expected);
        }
    }
}
