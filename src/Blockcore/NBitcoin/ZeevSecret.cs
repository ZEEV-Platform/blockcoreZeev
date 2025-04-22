using System.Collections.Generic;
using System.Linq;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.BIP38;
using Blockcore.NBitcoin.DataEncoders;
using Blockcore.Networks;

namespace Blockcore.NBitcoin
{
    public class ZeevSecret : Base58Data, IDestination, ISecret
    {
        public ZeevSecret(Key key, Network network)
            : base(ToBytes(key), network)
        {
        }

        private static byte[] ToBytes(Key key)
        {
            byte[] keyBytes = key.ToBytes();
 
            return keyBytes;
        }
        public ZeevSecret(string base58, Network expectedAddress = null)
            : base(base58, expectedAddress)
        {
        }

        private BitcoinPubKeyAddress _address;

        public BitcoinPubKeyAddress GetAddress()
        {
            return this._address ?? (this._address = this.PrivateKey.PubKey.GetAddress(this.Network));
        }

        public virtual KeyId PubKeyHash
        {
            get
            {
                return this.PrivateKey.PubKey.Hash;
            }
        }

        public PubKey PubKey
        {
            get
            {
                return this.PrivateKey.PubKey;
            }
        }

        #region ISecret Members

        private Key _Key;
        public Key PrivateKey
        {
            get
            {
                return this._Key ?? (this._Key = new Key(this.vchData, 32));
            }
        }
        #endregion

        protected override bool IsValid
        {
            get
            {
                if (this.vchData.Length != 33 && this.vchData.Length != 32)
                    return false;

                 return false;
            }
        }

        public ZeevEncryptedSecret Encrypt(string password)
        {
            return this.PrivateKey.GetEncryptedZeevSecret(password, this.Network);
        }


        public ZeevSecret Copy()
        {
            byte[] result = Encoders.Base58Check.DecodeData(this.wifData);
            List<byte> resultList = result.ToList();

            resultList.RemoveAt(resultList.Count - 1);
            return new ZeevSecret(Encoders.Base58Check.EncodeData(resultList.ToArray()), this.Network);
        }

        public override Base58Type Type
        {
            get
            {
                return Base58Type.SECRET_KEY;
            }
        }

        #region IDestination Members

        public Script ScriptPubKey
        {
            get
            {
                return GetAddress().ScriptPubKey;
            }
        }

        #endregion


    }
}
