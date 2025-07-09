using System;
using System.Linq;
using Blockcore.NBitcoin.Crypto;
using Blockcore.Networks;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;

namespace Blockcore.NBitcoin.BIP38
{
    public class ZeevConfirmationCode : Base58Data
    {

        public ZeevConfirmationCode(string wif, Network expectedNetwork = null)
            : base(wif, expectedNetwork)
        {
        }
        public ZeevConfirmationCode(byte[] rawBytes, Network network)
            : base(rawBytes, network)
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

        private byte[] _OwnerEntropy;
        public byte[] OwnerEntropy
        {
            get
            {
                return this._OwnerEntropy ?? (this._OwnerEntropy = this.vchData.SafeSubarray(5, 8));
            }
        }

        private LotSequence _LotSequence;
        public LotSequence LotSequence
        {
            get
            {
                bool hasLotSequence = (this.vchData[0] & 0x04) != 0;
                if(!hasLotSequence)
                    return null;
                if(this._LotSequence == null)
                {
                    this._LotSequence = new LotSequence(this.OwnerEntropy.SafeSubarray(4, 4));
                }
                return this._LotSequence;
            }
        }

        private byte[] _EncryptedPointB;

        private byte[] EncryptedPointB
        {
            get
            {
                return this._EncryptedPointB ?? (this._EncryptedPointB = this.vchData.SafeSubarray(13));
            }
        }

        public override Base58Type Type
        {
            get
            {
                return Base58Type.CONFIRMATION_CODE;
            }
        }

        protected override bool IsValid
        {
            get
            {
                return this.vchData.Length == 1 + 4 + 8 + 33;
            }
        }
    }
}