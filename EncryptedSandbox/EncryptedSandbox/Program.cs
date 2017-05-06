using SharpChecker.Attributes;
using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using static RijndaelManage.RijndaelEncryption;

namespace EncryptedSandbox
{
    class Program
    {
        static void Main(string[] args)
        {
            var prog = new Program();
            prog.SendText();
            Console.ReadLine();
        }

        void SendText()
        {
            //An [Encrypted] value is returned from the Encrypt method
            //so this may safely be passed to SendOverInternet which
            //expects an [Encrypted] argument
            SendOverInternet(Encrypt(RawText));
        }
        public string RawText { get; set; }
        public int SendOverInternet([Encrypted] byte[] msg)
        {
            return Transfer(msg);
        }
        [return:Encrypted]
        public byte[] Encrypt(string text)
        {
            byte[] rtn = null;
            using (RijndaelManaged myRijndael = new RijndaelManaged())
            {
                myRijndael.GenerateKey();
                myRijndael.GenerateIV();
                rtn = EncryptStringToBytes(text, myRijndael.Key, myRijndael.IV);
            }
            Debug.Assert(true, "rtn:Encrypted");
            return rtn;
        }

        public static int Transfer(byte[] transmission) { return 15; }
        // Only send encrypted data!
        public int SendOverInternet([Encrypted] String msg)
        {
            // Send the data over an insecure medium
            // Return the time of the transmission
            return 15;
        }

        //public string RemoveSpecialChars(string original, int charCode)
        //{
        //    // Remove the special characters
        //    return original;
        //}

        public Program ProgramFactory()
        {
            return new EncryptedSandbox.Program();
        }

        [Encrypted]
        public byte[] Ciphertext { get; set; }

        [Encrypted]
        public int Result { get; set; }

        [NonNull]
        public string Id { get; set; }

        [Encrypted]
        public string EncryptedText { get; set; }
        //void SetText()
        //{
        //    //This causes the diagnostic to fire because the return type of the method
        //    //doesn't have the appropriate attribute
        //    EncryptedText = RemoveSpecialChars(plaintext, 3);
        //}
        public string RemoveSpecialChars(byte[] original, int charCode)
        {
            // Remove the special characters
            return original.ToString();
        }
        
        void second()
        { 
            Id = null;
            Id = "Unique Identifier 1234";

            string plaintext = "Anyone can read this!";

            ////////////////////////////////////////////////////
            //Expression Statement - Assignment Statements
            ///////////////////////////////////////////////////

            //--Acceptable Cases--//
            //The return type of Encrypt is annotated, and will match the annotation 
            //of Ciphertext, so this should be accepted
            Ciphertext = Encrypt(plaintext);
            //We permit Encrypted values being assigned to unencrypted
            RawText = EncryptedText;


            //Introduce a static method call
            Result = Utilities.ExecuteQuery("Update user.workstatus set status='Hired'");

            //This is an example of assigning to a property - at the moment this should present an error
            //because an attribute has been added to the property.  
            new Utilities().MyProperty = new Object();
            EncryptedText = "";
            EncryptedText = String.Empty;
            
            //Random samples
            int[] teamNumbers = new int[] { 12, 23, 27, 44, 56, 80, 82, 88, 93 };
            var quarterback = teamNumbers.Select(num => num < SendOverInternet(RawText)).FirstOrDefault();
            int myInt = 2 + 2;
            new Object();

            while(myInt < SendOverInternet(RawText))
            {
                myInt++;
            }

            ////////////////////////////////////////////////////
            //Expression Statement - Invocation Expressions
            ///////////////////////////////////////////////////

            //--Acceptable Cases--//
            //This should be an allowed usage because Ciphertext has the [Encrypted] attribute
            //At this call site we need to determine that the method expects an value with an attribute, then determine if the value
            //being passed has this attribute (or eventually a subtype attribute).
            SendOverInternet(Ciphertext);
            //This is ok because the return type of the 'Encrypt' method has the [Encrypted] attribute
            SendOverInternet(Encrypt(plaintext));
            //This should be allowed because both braches of the conditional return a value with the 
            bool yep = true;
            SendOverInternet(yep ? Encrypt(plaintext + " ending") : Encrypt("testing"));
            //These should be acceptable
            if (yep)
            {
                Console.WriteLine(myInt);
            }
            this.ProgramFactory().RemoveSpecialChars(plaintext, 5);

            Utilities utils = new Utilities();
            utils.WriteToDisk(EncryptedText);

            //--Error Cases--//
            //This should generate an error because 'RawText' does not have the [Encrypted] attribute
            SendOverInternet(RawText);
            //These are also unnacceptable
            SendOverInternet("");
            SendOverInternet(String.Empty);
            SendOverInternet(RemoveSpecialChars(Encrypt(plaintext), 1));
            SendOverInternet(this.RemoveSpecialChars(Encrypt(plaintext), 0));
            SendOverInternet(RemoveSpecialChars(Encrypt(plaintext + " ending"), (3 + 5)));
            SendOverInternet(yep ? RemoveSpecialChars(Encrypt(plaintext + " ending"), (3 + 5)) : Encrypt(plaintext));
            SendOverInternet(plaintext + " ending");

            //We only want to write encrypted values to disk
            utils.WriteToDisk("unencrypted string");
            utils.WriteToDisk(plaintext);
        }
    }
}
