using System;
using System.Linq;
using System.Text;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.Crypto;
using Blockcore.NBitcoin.DataEncoders;
using Blockcore.Networks;
using Org.BouncyCastle.Math;

namespace Blockcore.NBitcoin
{
    public class PubKey : IBitcoinSerializable, IDestination
    {
        /// <summary>
        /// Create a new Public key from string
        /// </summary>
        public PubKey(string hex)
            : this(Encoders.Hex.DecodeData(hex))
        {
        }

        /// <summary>
        /// Create a new Public key from byte array
        /// </summary>
        public PubKey(byte[] bytes)
            : this(bytes, false)
        {
        }

        /// <summary>
        /// Create a new Public key from byte array
        /// </summary>
        /// <param name="bytes">byte array</param>
        /// <param name="unsafe">If false, make internal copy of bytes and does perform only a costly check for PubKey format. If true, the bytes array is used as is and only PubKey.Check is used for validating the format. </param>
        public PubKey(byte[] bytes, bool @unsafe)
        {
            if (bytes == null)
                throw new ArgumentNullException("bytes");

            if (!Check(bytes, false))
            {
                throw new FormatException("Invalid public key");
            }

            if (@unsafe)
                this._falconPkBytes = bytes;
            else
            {
                this._falconPkBytes = bytes.ToArray();
                try
                {
                    this._FalconKey = new FalconKey(bytes, false);
                }
                catch (Exception ex)
                {
                    throw new FormatException("Invalid public key", ex);
                }
            }
        }

        private FalconKey _FalconKey;

        internal FalconKey FalconKey
        {
            get
            {
                if (this._FalconKey == null) this._FalconKey = new FalconKey(this._falconPkBytes, false);
                return this._FalconKey;
            }
        }

        public PubKey Compress()
        {
            if (this.IsCompressed)
                return this;
            return this.FalconKey.GetPubKey();
        }

        public PubKey Decompress()
        {
            if (!this.IsCompressed)
                return this;
            return this.FalconKey.GetPubKey();
        }

        /// <summary>
        /// Check on public key format.
        /// </summary>
        /// <param name="data">bytes array</param>
        /// <param name="deep">If false, will only check the first byte and length of the array. If true, will also check that the ECC coordinates are correct.</param>
        /// <returns>true if byte array is valid</returns>
        public static bool Check(byte[] data, bool deep)
        {
            return Check(data, 0, data.Length, deep);
        }
        public static bool Check(byte[] data, int offset, int count, bool deep)
        {
            bool quick = data != null && count == 896 && data.Length >= 896;

            if (!deep || !quick)
                return quick;
            try
            {
                new FalconKey(data.SafeSubarray(offset, count), false);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private byte[] _falconPkBytes = new byte[0];
        private KeyId _ID;

        public KeyId Hash
        {
            get
            {
                if (this._ID == null)
                {
                    this._ID = new KeyId(new Hashes().Hash160(this._falconPkBytes, 0, this._falconPkBytes.Length));
                }
                return this._ID;
            }
        }

        private WitKeyId _WitID;

        public WitKeyId WitHash
        {
            get
            {
                if (this._WitID == null)
                {
                    this._WitID = new WitKeyId(new Hashes().Hash160(this._falconPkBytes, 0, this._falconPkBytes.Length));
                }
                return this._WitID;
            }
        }

        public bool IsCompressed
        {
            get
          {
                return false;
            }
        }

        public BitcoinPubKeyAddress GetAddress(Network network)
        {
            return network.CreateBitcoinPubKeyAddress(this.Hash);
        }

        public BitcoinScriptAddress GetScriptAddress(Network network)
        {
            Script redeem = PayToPubkeyTemplate.Instance.GenerateScriptPubKey(this);
            return new BitcoinScriptAddress(redeem.Hash, network);
        }

        public bool Verify(uint256 hash, FalconSignature sig)
        {
            if (sig == null)
                throw new ArgumentNullException(nameof(sig));
            if (hash == null)
                throw new ArgumentNullException(nameof(hash));

            return this.FalconKey.Verify(hash, sig);
        }

        public bool Verify(uint256 hash, byte[] sig)
        {
            return Verify(hash, FalconSignature.FromDER(sig));
        }

        public string ToHex()
        {
            return Encoders.Hex.EncodeData(this._falconPkBytes);
        }

        #region IBitcoinSerializable Members

        public void ReadWrite(BitcoinStream stream)
        {
            stream.ReadWrite(ref this._falconPkBytes);
            if (!stream.Serializing) this._FalconKey = new FalconKey(this._falconPkBytes, false);
        }

        #endregion IBitcoinSerializable Members

        public byte[] ToBytes()
        {
            return this._falconPkBytes.ToArray();
        }

        public byte[] ToBytes(bool @unsafe)
        {
            if (@unsafe)
                return this._falconPkBytes;
            else
                return this._falconPkBytes.ToArray();
        }

        public override string ToString()
        {
            return ToHex();
        }

        public override bool Equals(object obj)
        {
            var item = obj as PubKey;
            if (item == null)
                return false;
            return ToHex().Equals(item.ToHex());
        }

        public static bool operator ==(PubKey a, PubKey b)
        {
            if (ReferenceEquals(a, b))
                return true;
            if (((object)a == null) || ((object)b == null))
                return false;
            return a.ToHex() == b.ToHex();
        }

        public static bool operator !=(PubKey a, PubKey b)
        {
            return !(a == b);
        }

        public override int GetHashCode()
        {
            return ToHex().GetHashCode();
        }

        public string ToString(Network network)
        {
            return new BitcoinPubKeyAddress(this.Hash, network).ToString();
        }

        #region IDestination Members

        private Script _ScriptPubKey;

        public Script ScriptPubKey
        {
            get
            {
                if (this._ScriptPubKey == null)
                {
                    this._ScriptPubKey = PayToPubkeyTemplate.Instance.GenerateScriptPubKey(this);
                }
                return this._ScriptPubKey;
            }
        }

        public BitcoinWitPubKeyAddress GetSegwitAddress(Network network)
        {
            return new BitcoinWitPubKeyAddress(this.WitHash, network);
        }

        #endregion IDestination Members
    }
}