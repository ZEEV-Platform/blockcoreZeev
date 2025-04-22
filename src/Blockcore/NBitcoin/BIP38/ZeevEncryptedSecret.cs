using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using Blockcore.NBitcoin.DataEncoders;
using Blockcore.NBitcoin;
using Blockcore.Networks;
using DBreeze.Utils;
using System.Security.Cryptography;
using Isopoh.Cryptography.Argon2;
using Blockcore.NBitcoin.Crypto;

namespace Blockcore.NBitcoin.BIP38
{
    public class ZeevEncryptedSecretEC : ZeevEncryptedSecretNoEC
    {
        public ZeevEncryptedSecretEC(string wif, Network expectedNetwork = null) : base(wif, expectedNetwork)
        {
        }

        public ZeevEncryptedSecretEC(byte[] raw, Network network) : base(raw, network)
        {
        }

        public ZeevEncryptedSecretEC(Key key, string password, Network network) : base(key, password, network)
        {
        }
    }

    public class ZeevEncryptedSecretNoEC : ZeevEncryptedSecret
    {
        public ZeevEncryptedSecretNoEC(string wif, Network expectedNetwork = null)
            : base(wif, expectedNetwork)
        {
        }

        public ZeevEncryptedSecretNoEC(byte[] raw, Network network)
            : base(raw, network)
        {
        }

        public ZeevEncryptedSecretNoEC(Key key, string password, Network network)
            : base(GenerateWif(key, password, network), network)
        {

        }

        private static string GenerateWif(Key key, string password, Network network)
        {
            var vch = key.ToBytes();
            var vPassword = DeriveKey(password);
            byte[] encrypted = EncryptKey(vch, vPassword);

            var address = key.PubKey.GetAddress(network).ToString();
            byte[] addressBytes = Encoders.ASCII.DecodeData(address);

            byte[] addresshash = new Hashes().Hash256(addressBytes).ToBytes().SafeSubarray(0, 4);

            byte[] version = network.GetVersionBytes(Base58Type.ENCRYPTED_SECRET_KEY_NO_EC, true);
            byte flagByte = 0;
            flagByte |= 0x0C0;
            flagByte |= (byte)0x00;

            byte[] bytes = version
                            .Concat(new[] { flagByte })
                            .Concat(addresshash)
                            .Concat(encrypted).ToArray();
            return Encoders.Base58Check.EncodeData(bytes);
        }

        public override Key GetKey(string password)
        {
            throw new NotImplementedException();
        }

        private byte[] _FirstHalf;
        public byte[] EncryptedHalf1
        {
            get
            {
                return this._FirstHalf ?? (this._FirstHalf = this.vchData.SafeSubarray(this.ValidLength - 32, 16));
            }
        }

        private byte[] _Encrypted;
        public byte[] Encrypted
        {
            get
            {
                return this._Encrypted ?? (this._Encrypted = this.EncryptedHalf1.Concat(this.EncryptedHalf2).ToArray());
            }
        }

        public override Base58Type Type
        {
            get
            {
                return Base58Type.ENCRYPTED_SECRET_KEY_NO_EC;
            }
        }
    }

    public abstract class ZeevEncryptedSecret : Base58Data
    {
        public static ZeevEncryptedSecret Create(string wif, Network expectedNetwork = null)
        {
            return Network.Parse<ZeevEncryptedSecret>(wif, expectedNetwork);
        }

        public static ZeevEncryptedSecret Generate(Key key, string password, Network network)
        {
            return new ZeevEncryptedSecretNoEC(key, password, network);
        }


        protected ZeevEncryptedSecret(byte[] raw, Network network)
            : base(raw, network)
        {
        }

        protected ZeevEncryptedSecret(string wif, Network network)
            : base(wif, network)
        {
        }

        private byte[] _AddressHash;
        public byte[] AddressHash
        {
            get
            {
                return this._AddressHash ?? (this._AddressHash = this.vchData.SafeSubarray(1, 4));
            }
        }
        public bool IsCompressed
        {
            get
            {
                return (this.vchData[0] & 0x20) != 0;
            }
        }

        private byte[] _LastHalf;
        public byte[] EncryptedHalf2
        {
            get
            {
                return this._LastHalf ?? (this._LastHalf = this.vchData.Skip(this.ValidLength - 16).ToArray());
            }
        }
        protected int ValidLength = (1 + 4 + 16 + 16);


        protected override bool IsValid
        {
            get
            {
                bool lenOk = this.vchData.Length == this.ValidLength;
                if (!lenOk)
                    return false;
                bool reserved = (this.vchData[0] & 0x10) == 0 && (this.vchData[0] & 0x08) == 0;
                return reserved;
            }
        }

        public abstract Key GetKey(string password);
        public ZeevSecret GetSecret(string password)
        {
            return new ZeevSecret(GetKey(password), this.Network);
        }

        internal static byte[] DeriveKey(string password)
        {
            var config = new Argon2Config
            {
                Type = Argon2Type.DataDependentAddressing,
                Version = Argon2Version.Nineteen,
                TimeCost = 3,
                MemoryCost = 65536,
                Threads = 4,
                Password = Encoding.UTF8.GetBytes(password),
                Salt = Encoding.UTF8.GetBytes("+.-)(42sáq?:p{]%"),
                HashLength = 32           
            };

            using (var argon2 = new Argon2(config))
            {
                return argon2.Hash().Buffer;
            }
        }

        internal static byte[] EncryptKey(byte[] key, byte[] password)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = password;
                aes.GenerateIV();
                byte[] iv = aes.IV;

                using (var encryptor = aes.CreateEncryptor())
                using (var ms = new System.IO.MemoryStream())
                {
                    ms.Write(iv, 0, iv.Length);
                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    using (var writer = new System.IO.StreamWriter(cs))
                    {
                        writer.Write(key);
                    }
                    return ms.ToArray();
                }
            }
        }

        internal string DecryptKey(byte[] encryptedKey, byte[] password)
        {
            byte[] iv = new byte[16];
            Array.Copy(encryptedKey, 0, iv, 0, iv.Length);

            using (Aes aes = Aes.Create())
            {
                aes.Key = password;
                aes.IV = iv;

                using (var decryptor = aes.CreateDecryptor())
                using (var ms = new System.IO.MemoryStream(encryptedKey, iv.Length, encryptedKey.Length - iv.Length))
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                using (var reader = new System.IO.StreamReader(cs))
                {
                    return reader.ReadToEnd();
                }
            }
        }
    }
}
