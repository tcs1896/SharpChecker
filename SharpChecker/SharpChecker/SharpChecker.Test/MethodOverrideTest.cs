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
    public class MethodOverrideTest : CodeFixVerifier
    {
        /// <summary>
        /// Override the appropriate method to pass in our analyzer
        /// </summary>
        /// <returns></returns>
        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new SharpCheckerBaseAnalyzer();
        }

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

                [AttributeUsage(AttributeTargets.All, Inherited = true, AllowMultiple = true)]
                class EncryptedAttribute : Attribute
                {
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
                    public abstract double GetGPA();
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
                    [return:Encrypted]
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
            VerifyCSharpDiagnostic(test);
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
                        double average = (grades.Sum() / grades.Count()) * 1.05;
                        return average;
                    }

                    public override void AddGrade(double grade)
                    {
                        double inflation = grade + 1;
                        grades.Add(inflation);
                    }
                }";
            var test = String.Concat(baseClass, overridingClass);
            var diagLoc = new[] { new DiagnosticResultLocation("Test0.cs", 48, 51) };
            VerifyDiag(test, diagLoc, "Encrypted");
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
            var diagLoc = new[] { new DiagnosticResultLocation("Test0.cs", 41, 21) };
            VerifyDiag(test, diagLoc);
        }

        /// <summary>
        /// This is a helper method to push the verification through the machinery provided by DiagnosticVerifier
        /// </summary>
        /// <param name="test"></param>
        /// <param name="diagLoc"></param>
        private void VerifyDiag(string test, DiagnosticResultLocation[] diagLoc, string attribute = "EncryptedAttribute")
        {
            var expected = new DiagnosticResult
            {
                Id = "SharpCheckerMethodParams",
                Message = String.Format("Attribute application error {0}", attribute),
                Severity = DiagnosticSeverity.Error,
                Locations = diagLoc
            };

            VerifyCSharpDiagnostic(test, expected);
        }
    }
}
