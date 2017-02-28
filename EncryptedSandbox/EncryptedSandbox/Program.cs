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
            //new Program().sendText();
            //Console.ReadLine();
        }

        [return:Encrypted]
        public string Encrypt(string text)
        {
            string rtn = text;
            // Performing the encryption
            return rtn;
        }

        // Only send encrypted data!
        public void SendOverInternet([Encrypted] String msg)
        {
            // Send the data over an insecure medium
        }

        public string RemoveSpecialChars(string original, int charCode)
        {
            // Remove the special characters
            return original;
        }

        public Program ProgramFactory()
        {
            return new EncryptedSandbox.Program();
        }

        [Encrypted]
        public string Ciphertext { get; set; }

        //TODO: How do we determine the class hierarchy associated with these attributes?
        //This may be a good way to specify type annotation hierarchy.
        //[NotEncrypted]
        public string RawText { get; set; }

        [Encrypted]
        public int Result { get; set; }

        void sendText()
        {
            string plaintext = "Anyone can read this!";

            ////////////////////////////////////////////////////
            //Expression Statement - Assignment Statements
            ///////////////////////////////////////////////////

            //The return type of Encrypt is annotated, and will match the annotation 
            //of Ciphertext, so this should be accepted
            Ciphertext = Encrypt(plaintext);
            //This should cause the diagnostic to fire because the return type of the method
            //doesn't have the appropriate attribute
            Ciphertext = RemoveSpecialChars(plaintext, 3);
            //Introduce a static method call
            Result = Utilities.ExecuteQuery("Update user.workstatus set status='Hired'");
            //We permit Encrypted values being assigned to unencrypted
            RawText = Encrypt(plaintext);

            ////////////////////////////////////////////////////
            //Expression Statement - Invocation Expressions
            ///////////////////////////////////////////////////

            //This should be an allowed usage because Ciphertext has the [Encrypted] attribute
            //At this call site we need to determine that the method expects an value with an attribute, then determine if the value
            //being passed has this attribute (or eventually a subtype attribute).
            SendOverInternet(Ciphertext);
            //This should generate an error because 'RawText' does not have the [Encrypted] attribute
            SendOverInternet(RawText);
            //These are also unnacceptable
            SendOverInternet("");
            SendOverInternet(String.Empty);
            SendOverInternet(Encrypt(plaintext));
            SendOverInternet(RemoveSpecialChars(Encrypt(plaintext), 1));
            SendOverInternet(this.RemoveSpecialChars(Encrypt(plaintext), 0));
            SendOverInternet(RemoveSpecialChars(Encrypt(plaintext + " ending"), (3 + 5)));
            //bool yep = true;
            //SendOverInternet(yep ? RemoveSpecialChars(Encrypt(plaintext + " ending"), (3 + 5)) : Encrypt(plaintext));
            //if(yep)
            //{
            //    Console.WriteLine("yep");
            //}
            //this.ProgramFactory().RemoveSpecialChars(plaintext, 5);



            int[] teamNumbers = new int[] { 12, 23, 27, 44, 56, 80, 82, 88, 93 };
            var quarterback = teamNumbers.Select(num => num < 20).FirstOrDefault();
            int myInt = 2 + 2;
            new Object();
            //new Utilities().MyProperty = new Object();
        }
    }
}
