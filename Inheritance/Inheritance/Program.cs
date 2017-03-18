using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Inheritance
{
    class Program
    {
        static void Main(string[] args)
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

        public void AddGrade(double grade)
        {
            grades.Add(grade);
        }

        public abstract double GetGPA();
    }

    class Graduate : Student
    {
        public override double GetGPA()
        {
            double average = (grades.Sum() / grades.Count()) * 1.05;
            return average;
        }
    }

    class Undergraduate : Student
    {
        public override double GetGPA()
        {
            double average = grades.Sum() / grades.Count();
            return average;
        }
    }
}
