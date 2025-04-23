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
            var vPassword = DeriveKey(password);
            var s = this.Encrypted;
            var privkey = DecryptKey(this.Encrypted, vPassword);

            var key = new Key(privkey);

            byte[] addressBytes = Encoders.ASCII.DecodeData(key.PubKey.GetAddress(this.Network).ToString());
            byte[] salt = new Hashes().Hash256(addressBytes).ToBytes().SafeSubarray(0, 4);

            if (!Utils.ArrayEqual(salt, this.AddressHash))
                throw new SecurityException("Invalid password (or invalid Network)");

            return key;
        }

        private byte[] _FirstHalf;
        public byte[] EncryptedHalf1
        {
            get
            {
                return this._FirstHalf ?? (this._FirstHalf = this.vchData.SafeSubarray(this.ValidLength - 60, 30));
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
                return this._LastHalf ?? (this._LastHalf = this.vchData.Skip(this.ValidLength - 30).ToArray());
            }
        }
        protected int ValidLength = (1 + 4 + 30 + 30);


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
                TimeCost = 5,         
                MemoryCost = 262144,
                Threads = 6,       
                Password = Encoding.UTF8.GetBytes(password),
                Salt = Encoding.UTF8.GetBytes("+.-)(42sáq?:p{]%"),
                HashLength = 32           
            };

            using (var argon2 = new Argon2(config))
            {
                return argon2.Hash().Buffer;
            }
        }

        internal static byte[] EncryptKey(byte[] data, byte[] key)
        {
            byte[] nonce = RandomNumberGenerator.GetBytes(12);
            byte[] ciphertext = new byte[data.Length];
            byte[] tag = new byte[16];

            using (var chacha = new ChaCha20Poly1305(key))
            {
                chacha.Encrypt(nonce, data, ciphertext, tag);
            }

            byte[] result = new byte[nonce.Length + tag.Length + ciphertext.Length];
            Array.Copy(nonce, 0, result, 0, nonce.Length);
            Array.Copy(tag, 0, result, nonce.Length, tag.Length);
            Array.Copy(ciphertext, 0, result, nonce.Length + tag.Length, ciphertext.Length);

            return result;
        }

        internal static byte[] DecryptKey(byte[] encryptedData, byte[] key)
        {
            byte[] nonce = new byte[12];
            byte[] tag = new byte[16];
            byte[] ciphertext = new byte[encryptedData.Length - nonce.Length - tag.Length];

            Array.Copy(encryptedData, 0, nonce, 0, nonce.Length);
            Array.Copy(encryptedData, nonce.Length, tag, 0, tag.Length);
            Array.Copy(encryptedData, nonce.Length + tag.Length, ciphertext, 0, ciphertext.Length);

            byte[] data = new byte[ciphertext.Length];
            using (var chacha = new ChaCha20Poly1305(key))
            {
                chacha.Decrypt(nonce, ciphertext, tag, data);
            }

            return data;
        }
    }
}

