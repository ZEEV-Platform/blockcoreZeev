using System;
using System.Linq;
using System.Text;
using Blockcore.NBitcoin.Crypto;
using Blockcore.NBitcoin.Crypto.Cryptsharp;
using Blockcore.NBitcoin.DataEncoders;
using Blockcore.Networks;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;

namespace Blockcore.NBitcoin.BIP38
{
    public class EncryptedKeyResult
    {
        public EncryptedKeyResult(ZeevEncryptedSecretEC key, BitcoinAddress address, byte[] seed, Func<ZeevConfirmationCode> calculateConfirmation)
        {
            this._EncryptedKey = key;
            this._GeneratedAddress = address;
            this._CalculateConfirmation = calculateConfirmation;
            this._Seed = seed;
        }

        private readonly ZeevEncryptedSecretEC _EncryptedKey;
        public ZeevEncryptedSecretEC EncryptedKey
        {
            get
            {
                return this._EncryptedKey;
            }
        }

        private Func<ZeevConfirmationCode> _CalculateConfirmation;
        private ZeevConfirmationCode _ConfirmationCode;
        public ZeevConfirmationCode ConfirmationCode
        {
            get
            {
                if(this._ConfirmationCode == null)
                {
                    this._ConfirmationCode = this._CalculateConfirmation();
                    this._CalculateConfirmation = null;
                }
                return this._ConfirmationCode;
            }
        }
        private readonly BitcoinAddress _GeneratedAddress;
        public BitcoinAddress GeneratedAddress
        {
            get
            {
                return this._GeneratedAddress;
            }
        }

        private readonly byte[] _Seed;
        public byte[] Seed
        {
            get
            {
                return this._Seed;
            }
        }
    }

    public class LotSequence
    {
        public LotSequence(int lot, int sequence)
        {
            if(lot > 1048575 || lot < 0)
                throw new ArgumentOutOfRangeException("lot");
            if(sequence > 1024 || sequence < 0)
                throw new ArgumentOutOfRangeException("sequence");

            this._Lot = lot;
            this._Sequence = sequence;
            uint lotSequence = (uint)lot * 4096 + (uint)sequence;
            this._Bytes =
                new[]
                    {
                        (byte)(lotSequence >> 24),
                        (byte)(lotSequence >> 16),
                        (byte)(lotSequence >> 8),
                        (byte)(lotSequence)
                    };
        }
        public LotSequence(byte[] bytes)
        {
            this._Bytes = bytes.ToArray();
            uint lotSequence =
                ((uint) this._Bytes[0] << 24) +
                ((uint) this._Bytes[1] << 16) +
                ((uint) this._Bytes[2] << 8) +
                ((uint) this._Bytes[3] << 0);

            this._Lot = (int)(lotSequence / 4096);
            this._Sequence = (int)(lotSequence - this._Lot);
        }

        private readonly int _Lot;
        public int Lot
        {
            get
            {
                return this._Lot;
            }
        }
        private readonly int _Sequence;
        public int Sequence
        {
            get
            {
                return this._Sequence;
            }
        }

        private readonly byte[] _Bytes;
        public byte[] ToBytes()
        {
            return this._Bytes.ToArray();
        }

        private int Id
        {
            get
            {
                return Utils.ToInt32(this._Bytes, 0, true);
            }
        }

        public override bool Equals(object obj)
        {
            var item = obj as LotSequence;
            return item != null && this.Id.Equals(item.Id);
        }
        public static bool operator ==(LotSequence a, LotSequence b)
        {
            if(ReferenceEquals(a, b))
                return true;
            if(((object)a == null) || ((object)b == null))
                return false;
            return a.Id == b.Id;
        }

        public static bool operator !=(LotSequence a, LotSequence b)
        {
            return !(a == b);
        }

        public override int GetHashCode()
        {
            return this.Id.GetHashCode();
        }
    }

    public class ZeevPassphraseCode : Base58Data
    {

        public ZeevPassphraseCode(string wif, Network expectedNetwork = null)
            : base(wif, expectedNetwork)
        {
        }

        private LotSequence _LotSequence;
        public LotSequence LotSequence
        {
            get
            {
                bool hasLotSequence = (this.vchData[0]) == 0x51;
                if(!hasLotSequence)
                    return null;
                return this._LotSequence ?? (this._LotSequence = new LotSequence(this.OwnerEntropy.Skip(4).Take(4).ToArray()));
            }
        }

        private byte[] _OwnerEntropy;
        public byte[] OwnerEntropy
        {
            get
            {
                return this._OwnerEntropy ?? (this._OwnerEntropy = this.vchData.Skip(1).Take(8).ToArray());
            }
        }

        private byte[] _Passpoint;
        public byte[] Passpoint
        {
            get
            {
                return this._Passpoint ?? (this._Passpoint = this.vchData.Skip(1).Skip(8).ToArray());
            }
        }

        protected override bool IsValid
        {
            get
            {
                return 1 + 8 + 33 == this.vchData.Length && (this.vchData[0] == 0x53 || this.vchData[0] == 0x51);
            }
        }


        public override Base58Type Type
        {
            get
            {
                return Base58Type.PASSPHRASE_CODE;
            }
        }
    }
}
