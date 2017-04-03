using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using TestHelper;
using SharpChecker;
using System.Diagnostics;

namespace SharpChecker.Test
{
    [TestClass]
    public class EncryptedAssignmentTest : CodeFixVerifier
    {
        /// <summary>
        /// Override the appropriate method to pass in our analyzer
        /// </summary>
        /// <returns></returns>
        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new SharpCheckerDiagnosticAnalyzer();
        }

        public string CheckersFilename { get => "encrypted.xml"; }

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
                    [Encrypted]
                    public string Ciphertext { get; set; }
                    public string RawText { get; set; }

                    private string plaintext = ""unencrypted"";

                    [return:Encrypted]
                    public string Encrypt(string text)
                    {
                        string rtn = text;
                        // Performing the encryption
                        return rtn;
                    }

                    public string RemoveSpecialChars(string original, int charCode)
                    {
                        // Remove the special characters
                        return original;
                    }

                    public void SendOverInternet([Encrypted] String msg)
                    {
                        // Send the data over an insecure medium
                    }

                    static void Main(string[] args)
                    {";

        //We fill in and verify the body of the main method in the tests below

        private const string EncryptionProgEnd = @"

                    }
                }

                public class Utilities
                {
                    [Encrypted]
                    public Object MyProperty { get; set; }

                    public static int ExecuteQuery(string sql)
                    {
                        //Execute the query against the database
                        return 1;
                    }
                }

                [AttributeUsage(AttributeTargets.All, Inherited = true, AllowMultiple = true)] 
                class EncryptedAttribute : Attribute
                {
                }
            }";

        [TestMethod]
        public void NoDiagnosticsResult_AssignmentToUnattributed()
        {
            //This unit test is failing when all tests are run, but not when it is executed in isolation
            var body = @"                
                ////////////////////////////////////////////////////
                //Expression Statement - Assignment Statements
                ///////////////////////////////////////////////////

                //--Acceptable Cases--//
                //We permit Encrypted values being assigned to unencrypted
                RawText = Encrypt(plaintext);";

            var test = String.Concat(EncryptionProgStart, body, EncryptionProgEnd);

            VerifyCSharpDiagnostic(test, CheckersFilename);
        }

        /// <summary>
        /// Here we are assigning the result of a method with no attributes to a property
        /// which can only contain [Encrypted] values, so a diagnostic is generated.
        /// </summary>
        [TestMethod]
        public void AssignmentDecoratedPropToUndecoratedResult()
        {
            var body = @"                
                //--Error Cases--//
                //This should cause the diagnostic to fire because the return type of the method
                //doesn't have the appropriate attribute
                Ciphertext = RemoveSpecialChars(plaintext, 3);";
            var test = String.Concat(EncryptionProgStart, body, EncryptionProgEnd);
            var diagLoc = new[] { new DiagnosticResultLocation("Test0.cs", 43, 30) };
            VerifyDiag(test, diagLoc);
        }

        [TestMethod]
        public void AssignmentDecoratedPropToUndecoratedResultFromStaticMethod()
        {
            var body = @"                
                //--Error Cases--//
                //This should cause the diagnostic to fire because the return type of the method
                //doesn't have the appropriate attribute
                //Introduce a static method call
                Ciphertext = Utilities.ExecuteQuery(""Update user.workstatus set status = 'Hired'"");";
            var test = String.Concat(EncryptionProgStart, body, EncryptionProgEnd);
            var diagLoc = new[] { new DiagnosticResultLocation("Test0.cs", 44, 30) };
            VerifyDiag(test, diagLoc);
        }

        [TestMethod]
        public void AssignmentDecoratedPropToUndecoratedResultFromConstructor()
        {
            var body = @"                
                //--Error Cases--//
                //This is an example of assigning to a property - at the moment this should present an error
                //because an attribute has been added to the property.
                new Utilities().MyProperty = new Object();";
            var test = String.Concat(EncryptionProgStart, body, EncryptionProgEnd);
            var diagLoc = new[] { new DiagnosticResultLocation("Test0.cs", 43, 46) };
            VerifyDiag(test, diagLoc);
        }

        [TestMethod]
        public void NoDiagnosticsResult_AssignmentsWithNoAttribues()
        {
            var body = @"                
                //Random samples
                int[] teamNumbers = new int[] { 12, 23, 27, 44, 56, 80, 82, 88, 93 };
                var quarterback = teamNumbers.Select(num => num < 20).FirstOrDefault();";
            var test = String.Concat(EncryptionProgStart, body, EncryptionProgEnd);
            VerifyCSharpDiagnostic(test, CheckersFilename);
        }

        [TestMethod]
        public void NoDiagnosticsResult_AssignmentWithMatchingAttribute()
        {
            //This unit test is failing when all tests are run, but not when it is executed in isolation
            var body = @"                
                ////////////////////////////////////////////////////
                //Expression Statement - Assignment Statements
                ///////////////////////////////////////////////////

                //--Acceptable Cases--//
                //The return type of Encrypt is annotated, and will match the annotation 
                //of Ciphertext, so this should be accepted
                Ciphertext = Encrypt(plaintext);";

            var test = $"{EncryptionProgStart}{body}{EncryptionProgEnd}";

            VerifyCSharpDiagnostic(test, CheckersFilename);
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
                Id = "EncryptionChecker",
                Message = String.Format("Attribute application error {0}", "Encrypted"),
                Severity = DiagnosticSeverity.Error,
                Locations = diagLoc
            };

            VerifyCSharpDiagnostic(test, CheckersFilename, expected);
        }
    }
}
