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
        public const string EncryptionProgStart = @"
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
            }";

        /// <summary>
        /// Here we are testing a few scenarios, all of which should be acceptable and
        /// present no diagnostics
        /// </summary>
        [TestMethod]
        public void NoDiagnosticsResult_Invocation()
        {
            var body = @"                
                ////////////////////////////////////////////////////
                //Expression Statement - Invocation Expressions
                ///////////////////////////////////////////////////

                //--Acceptable Cases--//
                //This should be an allowed usage because Ciphertext has the [Encrypted] attribute
                //At this call site we need to determine that the method expects an value with an attribute, then determine if the value
                //being passed has this attribute (or eventually a subtype attribute).
                //SendOverInternet(Ciphertext);
                //This is ok because the return type of the 'Encrypt' method has the [Encrypted] attribute
                SendOverInternet(Encrypt(plaintext));";

            var test = String.Concat(EncryptionProgStart, body, EncryptionProgEnd);

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void NoDiagnosticsResult_Assignment()
        {
            var body = @"                
                ////////////////////////////////////////////////////
                //Expression Statement - Assignment Statements
                ///////////////////////////////////////////////////

                //--Acceptable Cases--//
                //The return type of Encrypt is annotated, and will match the annotation 
                //of Ciphertext, so this should be accepted
                Ciphertext = Encrypt(plaintext);
                //We permit Encrypted values being assigned to unencrypted
                RawText = Encrypt(plaintext);";

            var test = String.Concat(EncryptionProgStart, body, EncryptionProgEnd);

            VerifyCSharpDiagnostic(test);
        }

        /// <summary>
        /// Here we are assigning the result of a method with no attributes to a property
        /// which can only contain [Encrypted] values, so a diagnostic is generated.
        /// </summary>
        [TestMethod]
        public void DecoratedPropAssignedToUndecoratedResult()
        {
            var body = @"                
                //--Error Cases--//
                //This should cause the diagnostic to fire because the return type of the method
                //doesn't have the appropriate attribute
                Ciphertext = RemoveSpecialChars(plaintext, 3);";
            var test = String.Concat(EncryptionProgStart, body, EncryptionProgEnd);
            var diagLoc = new[] { new DiagnosticResultLocation("Test0.cs", 48, 30) };
            VerifyDiag(test, diagLoc);
        }

        /// <summary>
        /// In the methods below we are invoking a method which needs an argument with an [Encrypted] 
        /// attribute but the one provided doesn't have it, so we present a diagnostic
        /// </summary>
        [TestMethod]
        public void InvocationArgumentDoesntRespectParamAttribute()
        {
            var body = @"                
                //--Error Cases--//
                //This should generate an error because 'RawText' does not have the [Encrypted] attribute
                SendOverInternet(RawText);";
            var test = String.Concat(EncryptionProgStart, body, EncryptionProgEnd);
            var diagLoc = new[] { new DiagnosticResultLocation("Test0.cs", 47, 34) };
            VerifyDiag(test, diagLoc);
        }

        [TestMethod]
        public void InvocationArgumentDoesntRespectParamAttribute_EmptyStringLiteral()
        {
            var body = @"                
                //--Error Cases--//
                //This should generate an error because empty string literal does not have the [Encrypted] attribute
                SendOverInternet("");";
            var test = String.Concat(EncryptionProgStart, body, EncryptionProgEnd);
            var diagLoc = new[] { new DiagnosticResultLocation("Test0.cs", 47, 34) };
            VerifyDiag(test, diagLoc);
        }

        [TestMethod]
        public void InvocationArgumentDoesntRespectParamAttribute_EmptyString()
        {
            var body = @"                
                //--Error Cases--//
                //This should generate an error because empty string does not have the [Encrypted] attribute
                SendOverInternet(String.Empty);";
            var test = String.Concat(EncryptionProgStart, body, EncryptionProgEnd);
            var diagLoc = new[] { new DiagnosticResultLocation("Test0.cs", 47, 34) };
            VerifyDiag(test, diagLoc);
        }

        [TestMethod]
        public void InvocationArgumentDoesntRespectParamAttribute_MethodInvocation()
        {
            var body = @"                
                //--Error Cases--//
                //This should generate an error because RemoveSpecialChars does not return the [Encrypted] attribute
                SendOverInternet(RemoveSpecialChars(Encrypt(plaintext), 1));";
            var test = String.Concat(EncryptionProgStart, body, EncryptionProgEnd);
            var diagLoc = new[] { new DiagnosticResultLocation("Test0.cs", 47, 34) };
            VerifyDiag(test, diagLoc);
        }

        [TestMethod]
        public void InvocationArgumentDoesntRespectParamAttribute_ThisMethodInvocation()
        {
            var body = @"                
                //--Error Cases--//
                //This should generate an error because RemoveSpecialChars does not return the [Encrypted] attribute
                SendOverInternet(this.RemoveSpecialChars(Encrypt(plaintext), 0));";
            var test = String.Concat(EncryptionProgStart, body, EncryptionProgEnd);
            var diagLoc = new[] { new DiagnosticResultLocation("Test0.cs", 47, 34) };
            VerifyDiag(test, diagLoc);
        }

        [TestMethod]
        public void InvocationArgumentDoesntRespectParamAttribute_SubExpressionArgs()
        {
            var body = @"                
                //--Error Cases--//
                //This should generate an error because RemoveSpecialChars does not return the [Encrypted] attribute
                SendOverInternet(RemoveSpecialChars(Encrypt(plaintext + "" ending""), (3 + 5)));";
            var test = String.Concat(EncryptionProgStart, body, EncryptionProgEnd);
            var diagLoc = new[] {
                new DiagnosticResultLocation("Test0.cs", 47, 34),
                new DiagnosticResultLocation("Test0.cs", 47, 53)
            };
            VerifyDiag(test, diagLoc);
        }

        [TestMethod]
        public void InvocationArgumentDoesntRespectParamAttribute_SubExpressionArg()
        {
            //This apparently hasn't been implemented yet
            var body = @"                
                //--Error Cases--//
                //This should generate an error because RemoveSpecialChars does not return the [Encrypted] attribute
                SendOverInternet(plaintext + "" ending"");";
            var test = String.Concat(EncryptionProgStart, body, EncryptionProgEnd);
            var diagLoc = new[] {
                new DiagnosticResultLocation("Test0.cs", 47, 17)
            };
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
                Id = "SharpCheckerMethodParams",
                Message = String.Format("Attribute application error {0}", "EncryptedSandbox.EncryptedAttribute"),
                Severity = DiagnosticSeverity.Error,
                Locations = diagLoc
            };

            VerifyCSharpDiagnostic(test, expected);
        }

        /// <summary>
        /// Override the appropriate method to pass in our analyzer
        /// </summary>
        /// <returns></returns>
        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new SharpCheckerBaseAnalyzer();
        }
    }
}