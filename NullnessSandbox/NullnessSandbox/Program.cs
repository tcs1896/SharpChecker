using SharpChecker.attributes;
using System;
using System.Linq;

namespace NullnessSandbox
{
    class Program
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

        void SendText()
        {
            ////////////////////////////////////////////////////
            //Expression Statement - Assignment Statements
            ///////////////////////////////////////////////////

            //--Acceptable Cases--//
            NonNullProp = "String literal";

            MaybeNullProp = null;
            MaybeNullProp = "String literal";

            if(RawText != null)
            {
                SendOverInternet(RawText);
            }

            SendOverInternet(String.Empty);

            //--Error Cases--//
            NonNullProp = null;
            SendOverInternet(null);
            SendOverInternet(RawText);
            

            //Random samples
            int[] teamNumbers = new int[] { 12, 23, 27, 44, 56, 80, 82, 88, 93 };
            var quarterback = teamNumbers.Select(num => num < 20).FirstOrDefault();
            int myInt = 2 + 2;
            new Object();

            while (myInt < SendOverInternet(RawText))
            {
                myInt++;
            }
        }
    }
}
