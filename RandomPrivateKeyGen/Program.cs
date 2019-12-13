using System;

namespace RandomPrivateKeyGen
{
    class Program
    {
        static void Main(string[] args)
        {
            var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            var bytes = new byte[256 / 8];
            rng.GetBytes(bytes);
            var privateKey = Convert.ToBase64String(bytes);
            Console.WriteLine(privateKey);
        }
    }
}
