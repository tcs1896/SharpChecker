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
    public class EncryptedMethodTest : CodeFixVerifier
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

        public const string baseClass = @"using System;
                using System.Collections.Generic;
                using System.Linq;
                using System.Text;
                using Microsoft.CodeAnalysis;
                using Microsoft.CodeAnalysis.CSharp;

                class Program
                {  
                    public void Main(string[] args)
                    {
                    }
                }

                interface IStudent
                {
                    [return:Encrypted]
                    double GetGPA();
                    void AddGrade([Encrypted] double grade);
                }

                abstract class Student : IStudent
                {
                    protected List<double> grades = new List<double>();

                    public virtual void AddGrade([Encrypted] double grade)
                    {
                        grades.Add(grade);
                    }

                    [return:Encrypted]
                    public virtual List<string> GetClasses() 
                    { 
                        return initialized; 
                    }

                    [Encrypted]
                    public List<string> initialized = new List<string>();

                    [return:Encrypted]
                    public abstract double GetGPA();
                }

                [AttributeUsage(AttributeTargets.All, Inherited = true, AllowMultiple = true)] 
                class EncryptedAttribute : Attribute
                {
                }";

        /// <summary>
        /// Here we are testing the simple case where attributes match between the base class
        /// definition and the overriding definition and we present no diagnostics
        /// </summary>
        [TestMethod]
        public void NoDiagnosticsResult_Invocation()
        {
            var overridingClass = @"                
                class Graduate : Student
                {
                    [Encrypted]
                    private double average;

                    [return:Encrypted]
                    public override double GetGPA()
                    {
                        //TODO: use mechanism to suppress warning here instead of commenting this out
                        //average = (grades.Sum() / grades.Count()) * 1.05;
                        return average;
                    }

                    public override void AddGrade([Encrypted] double grade)
                    {
                        double inflation = grade + 1;
                        grades.Add(inflation);
                    }
                }";
            var test = String.Concat(baseClass, overridingClass);
            VerifyCSharpDiagnostic(test, CheckersFilename);
        }

        [TestMethod]
        public void OverridingMethodHasNoAttribute()
        {
            var overridingClass = @"                
                class Graduate : Student
                {
                    [return:Encrypted]
                    public override double GetGPA()
                    {
                        //average = (grades.Sum() / grades.Count()) * 1.05;
                        return average;
                    }

                    public override void AddGrade(double grade)
                    {
                        double inflation = grade + 1;
                        grades.Add(inflation);
                    }

                    [Encrypted]
                    private double average;
                }";
            var test = String.Concat(baseClass, overridingClass);
            var diagLoc = new[] { new DiagnosticResultLocation("Test0.cs", 57, 51) };
            VerifyDiag(test, diagLoc);
        }

        [TestMethod]
        public void OverridingMethodHasNoReturnAttribute()
        {
            var overridingClass = @"                
                class Graduate : Student
                {
                    public override double GetGPA()
                    {
                        double average = (grades.Sum() / grades.Count()) * 1.05;
                        return average;
                    }

                    public override void AddGrade([Encrypted] double grade)
                    {
                        double inflation = grade + 1;
                        grades.Add(inflation);
                    }
                }";
            var test = String.Concat(baseClass, overridingClass);
            var diagLoc = new[] { new DiagnosticResultLocation("Test0.cs", 50, 44) };
            VerifyDiag(test, diagLoc);
        }

        [TestMethod]
        public void ReturnedValueDoesntHaveAttribute()
        {
            var overridingClass = @"                
                class Program
                {
                    [return:Encrypted]
                    public string Encrypt(string text)
                    {
                        string rtn = text; 
                        // Performing the encryption
                        return rtn;
                    }
                }";
            var test = String.Concat(baseClass, overridingClass);
            var diagLoc = new[] { new DiagnosticResultLocation("Test0.cs", 55, 32) };
            VerifyDiag(test, diagLoc);
        }

        [TestMethod]
        public void NoDiagnosticsResult_MethodReturnsAppropriatelyAttributedValue()
        {
            var overridingClass = @"                
                class Program
                {
                    [Encrypted]
                    private string enc;

                    [return:Encrypted]
                    public string Encrypt(string text)
                    {
                        // Performing the encryption
                        return enc;
                    }
                }";
            var test = String.Concat(baseClass, overridingClass);
            VerifyCSharpDiagnostic(test, CheckersFilename);
        }

        [TestMethod]
        public void OverridingMethodWithGenericHasNoAttribute()
        {
            var overridingClass = @"                
                class Graduate : Student
                {
                    [return:Encrypted]
                    public override double GetGPA()
                    {
                        //average = (grades.Sum() / grades.Count()) * 1.05;
                        return average;
                    }

                    public override void AddGrade([Encrypted] double grade)
                    {
                        double inflation = grade + 1;
                        grades.Add(inflation);
                    }

                    public override List<string> GetClasses()
                    {
                        return new List<string>() { ""CSCI720"" };
                    }

                    [Encrypted]
                    private double average;
                }";
            var test = String.Concat(baseClass, overridingClass);
            var diagLoc = new[] { new DiagnosticResultLocation("Test0.cs", 63, 50) };
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
                Id = "EncryptionChecker",
                Message = String.Format("Attribute application error {0}", "Encrypted"),
                Severity = DiagnosticSeverity.Error,
                Locations = diagLoc
            };

            VerifyCSharpDiagnostic(test, CheckersFilename, expected);
        }
    }
}
