using Blockcore.NBitcoin;
using Blockcore.NBitcoin.BIP39;
using Blockcore.NBitcoin.Crypto;
using Blockcore.Networks.ZEEV;
using Xunit;

namespace TESTZEEV
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var seedBytes = TestMnemonic();
            SeekBytesOfSeed(seedBytes);
            //  var wallet = Blockcore.Features.ZeevWallet.WalletManager.GenerateWalletFile(name, encryptedSeed, extendedKey.ChainCode, coinType: coinType);

            Console.ReadKey();
        }

        public static byte[] TestMnemonic()
        {
            //MNEMONIC TEST
            var password = "ThisIsATest";
            var passphase = "Extra Seed Words";

            var mnemonicSHA3 = new Mnemonic("doctor before local return visa gauge verify net unit bunker learn silk", Wordlist.English);
            var seedSHA3 = mnemonicSHA3.DeriveSeed();
            var mnemonic = mnemonicSHA3.ToString();
            var extendedKey = mnemonicSHA3.DeriveExtKey(passphase);
            var seedSHA3PKey = extendedKey.PrivateKey;

            ////regenerate the same 
            var mnemonicIdenticalSHA3 = new Mnemonic(mnemonic, Wordlist.English);
            var seedIdenticalSHA3 = mnemonicIdenticalSHA3.DeriveSeed();
            var mnemonicIdentical = mnemonicIdenticalSHA3.ToString();
            var seedIdenticalSHA3PKey = mnemonicIdenticalSHA3.DeriveExtKey(passphase).PrivateKey;

            Assert.Equal(mnemonic, mnemonicIdentical);
            Assert.Equal(seedSHA3, seedIdenticalSHA3);
            Assert.Equal(seedSHA3PKey, seedIdenticalSHA3PKey);

            Console.WriteLine(mnemonicSHA3.ToString());
            Console.WriteLine("Seed bytes: " + ByteArrayToString(seedSHA3));
            ////END MNEMONIC TEST

            //ENCRYPTED SEED TEST
            var network = new ZEEVMain();
            var encryptedZeevSecret = extendedKey.PrivateKey.GetEncryptedZeevSecret(password, network);
            string encryptedWif = encryptedZeevSecret.ToWif();

            var regeneratedFromWifPk = Key.Parse(encryptedWif, password, network);

            Assert.Equal(seedSHA3PKey, seedIdenticalSHA3PKey);
            Console.WriteLine("Seed Encryption/Decryption OK");
            //END ENCRYPTED SEED TEST

            return extendedKey.ChainCode;
        }

        public static string ByteArrayToString(byte[] bytes)
        {
            if (bytes == null) return "null";
            string joinedBytes = string.Join(", ", bytes.Select(b => b.ToString()));
            return $"new byte[] {{ {joinedBytes} }}";
        }

        private static string BytesToHex(byte[] b)
        {
            return BitConverter.ToString(b).Replace("-", "");
        }

        public static void SeekBytesOfSeed(byte[] array)
        {
            int seed = 42;
            Random random = new Random(seed);

            int iterations = 2;

            Console.WriteLine("Right seed:");
            PrintArray(array);
            for (int iter = 0; iter < iterations; iter++)
            {
                int shiftAmount = random.Next(5);
                array = CircularShift(array, shiftAmount);

                int swapCount = random.Next(2, 5);
                for (int s = 0; s < swapCount; s++)
                {
                    int index1 = random.Next(0, array.Length);
                    int index2 = random.Next(0, array.Length);
                    byte temp = array[index1];
                    array[index1] = array[index2];
                    array[index2] = temp;
                }

                Console.WriteLine($"\n{iter + 1} (shift {shiftAmount}):");
                PrintArray(array);
            }
        }

        static byte[] CircularShift(byte[] array, int shiftAmount)
        {
            int length = array.Length;
            byte[] result = new byte[length];

            shiftAmount = shiftAmount % length;
            if (shiftAmount < 0)
                shiftAmount = length + shiftAmount;

            for (int i = 0; i < length; i++)
            {
                int newIndex = (i + shiftAmount) % length;
                result[newIndex] = array[i];
            }

            return result;
        }

        static void PrintArray(byte[] array)
        {
            foreach (byte b in array)
            {
                Console.Write($"{b:D2} ");
            }
            Console.WriteLine();
        }
    }
}