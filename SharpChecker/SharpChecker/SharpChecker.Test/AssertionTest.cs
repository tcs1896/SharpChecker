﻿using Microsoft.CodeAnalysis;
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
    public class AssertionTest : CodeFixVerifier
    {
        /// <summary>
        /// Override the appropriate method to pass in our analyzer
        /// </summary>
        /// <returns></returns>
        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new SharpCheckerDiagnosticAnalyzer();
        }

        public string CheckersFilename { get => "nullness.xml"; }

        public const string EncryptionProgStart = @"
        using System;
        using System.Collections.Generic;
        using System.Linq;
        using System.Text;
        using System.Threading.Tasks;
        using System.Diagnostics;

        namespace NullnessSandbox
        {
            class TypeName
            {   
                static void Main(string[] args)
                {
                    var prog = new Program();
                    prog.SendText();
                    Console.ReadLine();
                }

                public int SendOverInternet([NonNull] String msg)
                {
                    return 15;
                }

                public string RemoveSpecialChars(string original, int charCode)
                {
                    // Remove the special characters
                    return original;
                }

                [NonNull]
                public string NonNullProp { get; set; }

                [MaybeNull]
                public string MaybeNullProp { get; set; }

                public string RawText { get; set; }

                ";

        //We fill in and verify the body of the main method in the tests below

        private const string EncryptionProgEnd = @"
            }


            [AttributeUsage(AttributeTargets.All, Inherited = true, AllowMultiple = true)] 
            class NonNullAttribute : Attribute
            {
            }

            [AttributeUsage(AttributeTargets.All, Inherited = true, AllowMultiple = true)] 
            class MaybeNullAttribute : Attribute
            {
            }
        }";

        [TestMethod]
        public void NoDiagnosticsResult_Assertion()
        {
            var body = @"                
            void SmallerMethod()
            {
                Debug.Assert(RawText != null, ""RawText:NonNull"");
                SendOverInternet(RawText);
            }";

            var test = String.Concat(EncryptionProgStart, body, EncryptionProgEnd);

            VerifyCSharpDiagnostic(test, CheckersFilename);
        }

        [TestMethod]
        public void PassNullLiteralAsNonNullArgument()
        {
            var body = @"                
            void SmallerMethod()
            {
                //Invoking method with appropriate attribute or assertion
                SendOverInternet(RawText);
            }";

            var test = String.Concat(EncryptionProgStart, body, EncryptionProgEnd);
            var diagLoc = new[] { new DiagnosticResultLocation("Test0.cs", 43, 34) };
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