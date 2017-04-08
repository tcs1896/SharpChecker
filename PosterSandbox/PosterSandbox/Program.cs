using SharpChecker.attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PosterSandbox
{
    class Program
    {
        [return:Encrypted]
        public static T Identity<T>([Encrypted] T self) { return self; }
        [Encrypted] public string EncryptedProp { get; set; }

        public static void SendText([Encrypted] string unencrypted) { }
        public static string UnencryptedProp { get; set; }
        static void Main(string[] args)
        {
            SendText(UnencryptedProp);






            //This should present a warning.  Also, when using static properties there was no warning.
            //These may be failing for the same reasons.
            prog.EncryptedProp = Program.Identity<string>(prog.EncryptedProp); 
        }
    }
}
