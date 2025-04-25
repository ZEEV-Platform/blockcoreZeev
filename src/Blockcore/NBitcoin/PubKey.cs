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

        /// <summary>
        /// Verify message signed using signmessage from bitcoincore
        /// </summary>
        /// <param name="message">The message</param>
        /// <param name="signature">The signature</param>
        /// <returns>True if signatures is valid</returns>
        public bool VerifyMessage(string message, string signature)
        {
            return this.VerifyMessage(Encoding.UTF8.GetBytes(message), signature);
        }

        /// <summary>
        /// Verify message signed using signmessage from bitcoincore
        /// </summary>
        /// <param name="message">The message</param>
        /// <param name="signature">The signature</param>
        /// <returns>True if signatures is valid</returns>
        public bool VerifyMessage(byte[] messageBytes, string signature)
        {
            throw new NotImplementedException("FALCON");
          //  ECDSASignature sig = DecodeSigString(signature);
            //return this.VerifyMessage(messageBytes, sig);
        }

        /// <summary>
        /// Verify message signed using signmessage from bitcoincore.
        /// </summary>
        /// <param name="messageBytes">The message.</param>
        /// <param name="sig">The signature.</param>
        /// <returns>True if signature is valid.</returns>
        public bool VerifyMessage(byte[] messageBytes, FalconSignature sig)
        {
            byte[] messageSigned = Utils.FormatMessageForSigning(messageBytes);
            uint256 hash = new Hashes().Hash256(messageSigned);
            return this.FalconKey.Verify(hash, sig);
        }

        ///// <summary>
        ///// Decode signature from bitcoincore verify/signing rpc methods
        ///// </summary>
        ///// <param name="signature"></param>
        ///// <returns></returns>
        //private static ECDSASignature DecodeSigString(string signature)
        //{
        //    byte[] signatureEncoded = Encoders.Base64.DecodeData(signature);
        //    return DecodeSig(signatureEncoded);
        //}

        //private static ECDSASignature DecodeSig(byte[] signatureEncoded)
        //{
        //    var r = new BigInteger(1, signatureEncoded.SafeSubarray(1, 32));
        //    var s = new BigInteger(1, signatureEncoded.SafeSubarray(33, 32));
        //    var sig = new ECDSASignature(r, s);
        //    return sig;
        //}

        ////Thanks bitcoinj source code
        ////http://bitcoinj.googlecode.com/git-history/keychain/core/src/main/java/com/google/bitcoin/core/Utils.java
        //public static PubKey RecoverFromMessage(string messageText, string signatureText)
        //{
        //    return RecoverFromMessage(Encoding.UTF8.GetBytes(messageText), signatureText);
        //}

        //public static PubKey RecoverFromMessage(byte[] messageBytes, string signatureText)
        //{
        //    byte[] signatureEncoded = Encoders.Base64.DecodeData(signatureText);
        //    byte[] message = Utils.FormatMessageForSigning(messageBytes);
        //    uint256 hash = Hashes.Hash256(message);
        //    return RecoverCompact(hash, signatureEncoded);
        //}

        //public static PubKey RecoverCompact(uint256 hash, byte[] signatureEncoded)
        //{
        //    if (signatureEncoded.Length < 65)
        //        throw new ArgumentException("Signature truncated, expected 65 bytes and got " + signatureEncoded.Length);

        //    int header = signatureEncoded[0];

        //    // The header byte: 0x1B = first key with even y, 0x1C = first key with odd y,
        //    //                  0x1D = second key with even y, 0x1E = second key with odd y

        //    if (header < 27 || header > 34)
        //        throw new ArgumentException("Header byte out of range: " + header);

        //    ECDSASignature sig = DecodeSig(signatureEncoded);
        //    bool compressed = false;

        //    if (header >= 31)
        //    {
        //        compressed = true;
        //        header -= 4;
        //    }
        //    int recId = header - 27;

        //    ECKey key = ECKey.RecoverFromSignature(recId, sig, hash, compressed);
        //    return key.GetPubKey(compressed);
        //}

        //public PubKey Derivate(byte[] cc, uint nChild, out byte[] ccChild)
        //{
        //    byte[] lr = null;
        //    var l = new byte[32];
        //    var r = new byte[32];
        //    if ((nChild >> 31) == 0)
        //    {
        //        byte[] pubKey = ToBytes();
        //        lr = new Hashes().BIP32Hash(cc, nChild, pubKey[0], pubKey.Skip(1).ToArray());
        //    }
        //    else
        //    {
        //        throw new InvalidOperationException("A public key can't derivate an hardened child");
        //    }
        //    Array.Copy(lr, l, 32);
        //    Array.Copy(lr, 32, r, 0, 32);
        //    ccChild = r;

        //    BigInteger N = ECKey.CURVE.N;
        //    var parse256LL = new BigInteger(1, l);

        //    if (parse256LL.CompareTo(N) >= 0)
        //        throw new InvalidOperationException("You won a prize ! this should happen very rarely. Take a screenshot, and roll the dice again.");

        //    ECPoint q = ECKey.CURVE.G.Multiply(parse256LL).Add(this.ECKey.GetPublicKeyParameters().Q);
        //    if (q.IsInfinity)
        //        throw new InvalidOperationException("You won the big prize ! this would happen only 1 in 2^127. Take a screenshot, and roll the dice again.");

        //    q = q.Normalize();
        //    var p = new FpPoint(ECKey.CURVE.Curve, q.XCoord, q.YCoord, true);
        //    return new PubKey(p.GetEncoded());
        //}

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