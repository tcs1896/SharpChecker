using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EncryptedSandbox
{
    [Encrypted]
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
            return rtn;
        }

        // Only send encrypted data!
        public void sendOverInternet([Encrypted] String msg)
        {
            // ...
        }

        [Encrypted]
        public string Ciphertext { get; set; }

        public string RawText { get; set; }

        void sendText()
        {
            // ...
            string plaintext = "Anyone can read this!";
            Ciphertext = Encrypt(plaintext);

            //Demonstrate how to retrieve the properties of a class and check for the custom attribute
            Type t = typeof(Program);
            var props = t.GetProperties();
            foreach (PropertyInfo prop in props)
            {
                object[] attrs = prop.GetCustomAttributes(true);
                foreach (object attr in attrs)
                {
                    EncryptedAttribute encrAttr = attr as EncryptedAttribute;
                    if (encrAttr != null)
                    {
                        Console.WriteLine($"Property {prop.Name} has the {nameof(EncryptedAttribute)}");
                    }
                }
            }

            //Demonstrate how to investigate the methods of a class, seraching for the custome attribute
            var methods = t.GetMethods();
            foreach (var met in methods)
            {
                var attrs = met.GetCustomAttributes(true);
                foreach (var attr in attrs)
                {
                    EncryptedAttribute encrAttr = attr as EncryptedAttribute;
                    if (encrAttr != null)
                    {
                        Console.WriteLine($"Method {met.Name} has the {nameof(EncryptedAttribute)}");
                    }
                }

                //Determine if the method arguments have the attribute assigned
                var methodArgs = met.GetParameters();
                foreach (var mArg in methodArgs)
                {
                    var mArgAttrs = mArg.GetCustomAttributes(true);
                    foreach (var attr in mArgAttrs)
                    {
                        EncryptedAttribute encrAttr = attr as EncryptedAttribute;
                        if (encrAttr != null)
                        {
                            Console.WriteLine($"Method {met.Name} has parameter {mArg.Name} with the {nameof(EncryptedAttribute)}");
                        }
                    }
                }
            }

            //This should be an allowed usage because Ciphertext has the [Encrypted] attribute
            //At this call site we need to determine that the method expects an value with an attribute, then determine if the value
            //being passed has this attribute (or eventually a subtype attribute).
            sendOverInternet(Ciphertext);
            sendOverInternet(RawText);
            sendOverInternet("");
        }

        //private static void FindAttributes<T>(T methods) where T : IEnumerable<T>
        //{

        //}

        //void sendPassword()
        //{
        //    String password = getUserPassword();
        //    sendOverInternet(password);
        //}

    }
}
