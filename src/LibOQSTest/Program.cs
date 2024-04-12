using Blockcore.NBitcoin.BIP39;
using dotnetOQSSample.BIP39;
using LibOQSTest.Core;

namespace LibOQSTest
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var mnemonicSHA3 = new dotnetOQSSample.BIP39.Mnemonic(Wordlist.English, WordCount.Twelve);
            var seedSHA3 = mnemonicSHA3.DeriveSeed();

            Console.WriteLine(mnemonicSHA3.ToString());
            Console.WriteLine("Seed bytes Sha3Digest512: " + ByteArrayToString(seedSHA3));

            Console.WriteLine("Enabled KEM mechanisms: ");
            foreach (string alg in KEM.EnabledMechanisms)
            {
                Console.WriteLine(" - " + alg);
            }
        }

        public static string ByteArrayToString(byte[] bytes)
        {
            if (bytes == null) return "null";
            string joinedBytes = string.Join(", ", bytes.Select(b => b.ToString()));
            return $"new byte[] {{ {joinedBytes} }}";
        }
    }
}