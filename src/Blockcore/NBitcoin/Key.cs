using System;
using System.Linq;
using System.Text;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.BIP38;
using Blockcore.NBitcoin.Crypto;
using Blockcore.Networks;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Math;

namespace Blockcore.NBitcoin
{
    public class Key : IBitcoinSerializable, IDestination
    {
        private const int SEEDKEY_SIZE = 32;
        private readonly static uint256 N = uint256.Parse("fffffffffffffffffffffffffffffffebaaedce6af48a03bbfd25e8cd0364141");

        public static Key Parse(string wif, Network network = null)
        {
            return Network.Parse<ZeevSecret>(wif, network).PrivateKey;
        }

        public static Key Parse(string wif, string password, Network network = null)
        {
            return Network.Parse<ZeevEncryptedSecret>(wif, network).GetKey(password);
        }

        private byte[] _vectorBytes = new byte[0];
        private FalconKey _FalconKey;

        public Key()
        {
            var data = new byte[SEEDKEY_SIZE];
            do
            {
                RandomUtils.GetBytes(data);
            } while (!Check(data));

            SetBytes(data, data.Length);
        }
        public Key(byte[] data, int count = -1)
        {
            if (count == -1)
                count = data.Length;
            if (count != SEEDKEY_SIZE)
            {
                throw new FormatException("The size of an EC key should be 32");
            }
            if (Check(data))
            {
                SetBytes(data, count);
            }
            else
                throw new FormatException("Invalid EC key");
        }

        private void SetBytes(byte[] data, int count)
        {
            this._vectorBytes = data.SafeSubarray(0, count);
            this._FalconKey = new FalconKey(this._vectorBytes);
        }

        private static bool Check(byte[] vch)
        {
            var candidateKey = new uint256(vch.SafeSubarray(0, SEEDKEY_SIZE));
            return candidateKey > 0 && candidateKey < N;
        }

        private PubKey _PubKey;

        public PubKey PubKey
        {
            get
            {
                if (this._PubKey == null)
                {
                    if (this._FalconKey != null)
                    {
                        this._PubKey = this._FalconKey.GetPubKey();
                    } 
                }
                return this._PubKey;
            }
        }

        public FalconSignature Sign(uint256 hash)
        {
            var sign = this._FalconKey.Sign(hash);
            return sign;
        }

        /// <summary>
        /// Hashes and signs a message, returning the signature.
        /// </summary>
        /// <param name="messageBytes">The message to hash then sign.</param>
        /// <returns>The signature of the hashed and signed message.</returns>
        public FalconSignature SignMessageBytes(byte[] messageBytes)
        {
            byte[] data = Utils.FormatMessageForSigning(messageBytes);

            uint256 hash = new Hashes().Hash256(data);

            var sign = this._FalconKey.Sign(hash);
            return sign;
        }

        public string SignMessage(string message)
        {
            return SignMessage(Encoding.UTF8.GetBytes(message));
        }

        public string SignMessage(byte[] messageBytes)
        {
            byte[] data = Utils.FormatMessageForSigning(messageBytes);

            uint256 hash = new Hashes().Hash256(data);
            var sign = this._FalconKey.Sign(hash);
            return sign.GetSignatureBase64();
        }

        #region IBitcoinSerializable Members

        public void ReadWrite(BitcoinStream stream)
        {
            stream.ReadWrite(ref this._vectorBytes);
            if (!stream.Serializing)
            {
                this._FalconKey = new FalconKey(this._vectorBytes);
            }
        }

        #endregion

        public Key Derivate(byte[] cc, uint nChild, out byte[] ccChild)
        {
            byte[] l = null;
            var hasher = new Hashes();

            if ((nChild >> 31) == 0)
            {
                byte[] pubKey = this.PubKey.ToBytes();
                l = hasher.BIP32Hash(cc, nChild, pubKey[0], pubKey.SafeSubarray(1));
            }
            else
            {
                l = hasher.BIP32Hash(cc, nChild, 0, this.ToBytes());
            }

            var shake = new ShakeDigest(256);
            shake.BlockUpdate(l, 0, l.Length);
            byte[] seed = new byte[32];
            shake.OutputFinal(seed, 0, seed.Length);

            ccChild = seed;

            return new Key(seed);
        }

        public ZeevSecret GetBitcoinSecret(Network network)
        {
            return new ZeevSecret(this, network);
        }

        /// <summary>
        /// Same than GetBitcoinSecret
        /// </summary>
        /// <param name="network"></param>
        /// <returns></returns>
        public ZeevSecret GetWif(Network network)
        {
            return new ZeevSecret(this, network);
        }

        public ZeevEncryptedSecretNoEC GetEncryptedZeevSecret(string password, Network network)
        {
            return new ZeevEncryptedSecretNoEC(this, password, network);
        }

        public string ToString(Network network)
        {
            return new ZeevSecret(this, network).ToString();
        }

        #region IDestination Members

        public Script ScriptPubKey
        {
            get
            {
                return this.PubKey.Hash.ScriptPubKey;
            }
        }

        #endregion

        public TransactionSignature Sign(uint256 hash, SigHash sigHash)
        {
            var sign = this._FalconKey.Sign(hash);

            return new TransactionSignature(sign, sigHash);
        }

        public override bool Equals(object obj)
        {
            var item = obj as Key;
            if ((item == null) || (item._vectorBytes == null))
                return false;
            return this._vectorBytes.SequenceEqual(item._vectorBytes);
            //return this.PubKey.Equals(item.PubKey);
        }
        public static bool operator ==(Key a, Key b)
        {
            if (ReferenceEquals(a, b))
                return true;
            if (((object)a == null) || ((object)b == null))
                return false;
            return a.PubKey == b.PubKey;
        }

        public static bool operator !=(Key a, Key b)
        {
            return !(a == b);
        }

        public override int GetHashCode()
        {
            return this.PubKey.GetHashCode();
        }
    }
}
