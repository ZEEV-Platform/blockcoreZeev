using System;
using Blockcore.NBitcoin.Crypto;
using Org.BouncyCastle.Asn1;

namespace Blockcore.NBitcoin.Crypto
{
    public class FalconSignature
    {
        private readonly byte[] _signature;

        public FalconSignature(byte[] signature)
        {
            this._signature = signature;
        }

        public FalconSignature(string base64Sign)
        {
            this._signature = Convert.FromBase64String(base64Sign);
        }

        public FalconSignature()
        {
        }

        public string GetSignatureBase64()
        {
            return Convert.ToBase64String(this._signature);
        }

        public byte[] ToDER()
        {
            return this._signature;
        }

        private const string InvalidDERSignature = "Invalid DER signature";
        public static FalconSignature FromDER(byte[] sig)
        {
            return new FalconSignature(sig);
        }

        public static bool IsValidDER(byte[] bytes)
        {
            return true;
        }
    }
}
