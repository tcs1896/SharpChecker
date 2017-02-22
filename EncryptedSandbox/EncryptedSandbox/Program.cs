using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EncryptedSandbox
{
    class Program
    {
        static void Main(string[] args)
        {
            new Program().sendText();
            Console.ReadLine();
        }

        [return:Encrypted]
        public string Encrypt(string text)
        {
            string rtn = text;
            // Performing the encryption
            return rtn;
        }

        // Only send encrypted data!
        public void sendOverInternet([Encrypted] String msg)
        {
            // Send the data over an insecure medium
        }

        [Encrypted]
        public string Ciphertext { get; set; }

        public string RawText { get; set; }

        void sendText()
        {
            string plaintext = "Anyone can read this!";
            //We want to verify this assignment.  The return type of Encrypt is annotated, and
            //will match the annotation of Ciphertext, so this should be accepted
            Ciphertext = Encrypt(plaintext);

            //This should be an allowed usage because Ciphertext has the [Encrypted] attribute
            //At this call site we need to determine that the method expects an value with an attribute, then determine if the value
            //being passed has this attribute (or eventually a subtype attribute).
            sendOverInternet(Ciphertext);
            //This should generate an error because 'RawText' does not have the [Encrypted] attribute
            sendOverInternet(RawText);
            //This is also unnacceptable
            sendOverInternet("");
        }
    }
}
