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

            //  var wallet = Blockcore.Features.ZeevWallet.WalletManager.GenerateWalletFile(name, encryptedSeed, extendedKey.ChainCode, coinType: coinType);

            Console.ReadKey();
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
    }
}