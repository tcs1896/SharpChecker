using System.Collections.Generic;
using System.Linq;
using SharpChecker.attributes;

namespace InheritanceSandbox
{
    class Program
    {
        static void Main(string[] args)
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
        public abstract double GetGPA();
    }

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
    }

    class Undergraduate : Graduate
    {
        public override double GetGPA()
        {
            double average = grades.Sum() / grades.Count();
            return average;
        }

        public override void AddGrade([Encrypted] double grade)
        {
            double inflation = grade + 1;
            grades.Add(inflation);
        }
    }
}
