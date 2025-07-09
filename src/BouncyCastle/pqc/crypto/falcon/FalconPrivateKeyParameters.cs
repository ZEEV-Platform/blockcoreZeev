using System;
using Org.BouncyCastle.Tls.Crypto;
using Org.BouncyCastle.Utilities;


namespace Org.BouncyCastle.Pqc.Crypto.Falcon
{
    public sealed class FalconPrivateKeyParameters
        : FalconKeyParameters
    {
        private readonly byte[] pk;
        private readonly byte[] f;
        private readonly byte[] g;
        private readonly byte[] F;

        public FalconPrivateKeyParameters(FalconParameters parameters, byte[] f, byte[] g, byte[] F, byte[] pk_encoded)
            : base(true, parameters)
        {
            this.f = Arrays.Clone(f);
            this.g = Arrays.Clone(g);
            this.F = Arrays.Clone(F);
            this.pk = Arrays.Clone(pk_encoded);
        }

        public FalconPrivateKeyParameters(FalconParameters parameters, byte[] encoded)
            : base(true, parameters)
        {
            if (encoded.Length != 1280)
                throw new FormatException("The size of an Falcon key should be 1280");

            this.f = new byte[384];
            this.g = new byte[384];
            this.F = new byte[512];

            Buffer.BlockCopy(encoded, 0, this.f, 0, 384);
            Buffer.BlockCopy(encoded, 384, this.g, 0, 384);
            Buffer.BlockCopy(encoded, 768, this.F, 0, 512);
        }

        public byte[] GetEncoded()
        {
            return Arrays.ConcatenateAll(f, g, F);
        }

        public byte[] GetPublicKey()
        {
            return Arrays.Clone(pk);
        }

        public byte[] GetSpolyLittleF()
        {
            return Arrays.Clone(f);
        }

        public byte[] GetG()
        {
            return Arrays.Clone(g);
        }

        public byte[] GetSpolyBigF()
        {
            return Arrays.Clone(F);
        }
    }
}
