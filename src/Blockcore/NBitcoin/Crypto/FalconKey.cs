using System;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.Crypto.Cryptsharp;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Pqc.Crypto.Falcon;
using Org.BouncyCastle.Pqc.Crypto.Utilities;
using Org.BouncyCastle.Security;

namespace Blockcore.NBitcoin.Crypto
{
    public class FalconKey
    {
        public FalconPrivateKeyParameters PrivateKey
        {
            get
            {
                return this._Key as FalconPrivateKeyParameters;
            }
        }

        private readonly FalconKeyParameters _Key;
        public static int N_ORDER;
        public static readonly FalconKeyGenerationParameters _KParam;
        public static readonly FalconParameters _FParameters = FalconParameters.falcon_512;
        public static readonly string _FDerObject = "1.3.9999.3.1";
        static FalconKey()
        {
        }

        public FalconKey(byte[] seedkey)
        {
            var _FParam = FalconParameters.falcon_512;
            var random = new SecureRandom(new FakeSeedGenerator(seedkey));
            var _KParam = new FalconKeyGenerationParameters(random, _FParam);

            var nist = new FalconNist(_KParam.Random, (uint)_KParam.Parameters.LogN, (uint)_KParam.Parameters.NonceLength);
            N_ORDER = 1 << _KParam.Parameters.LogN;

            nist.crypto_sign_keypair(out byte[] pk, out byte[] f, out byte[] g, out byte[] F);

            this._Key = new FalconPrivateKeyParameters(_KParam.Parameters, f, g, F, pk);
        }

        public FalconKey(byte[] bytes, bool isDerEncodedPrivateKey)
        {
            if (isDerEncodedPrivateKey)
            {
                Asn1InputStream asn1InputStream = new Asn1InputStream(bytes);
                Asn1Object a = asn1InputStream.ReadObject();
                var toPkcs8Der = a.GetEncoded(Asn1Encodable.Der);

                var newPKInfo = new PrivateKeyInfo(new AlgorithmIdentifier(new DerObjectIdentifier("1.3.9999.3.1")), Asn1Object.FromByteArray(toPkcs8Der));
                this._Key = (FalconPrivateKeyParameters)PqcPrivateKeyFactory.CreateKey(newPKInfo);
            }
            else
            {
                this._Key = new FalconPublicKeyParameters(_FParameters, bytes);
            }
        }

        public static FalconKeyGenerationParameters KParam
        {
            get
            {
                return _KParam;
            }
        }

        public static FalconParameters FParam
        {
            get
            {
                return _FParameters;
            }
        }

        public FalconSignature Sign(uint256 hash)
        {
            AssertPrivateKey();

            var signer = new FalconSigner();
            signer.Init(true, this.PrivateKey);
            byte[] sig = signer.GenerateSignature(hash.ToBytes());
            return new FalconSignature(sig);
        }

        private void AssertPrivateKey()
        {
            if (this.PrivateKey == null)
                throw new InvalidOperationException("This key should be a private key for such operation");
        }

        internal bool Verify(uint256 hash, FalconSignature sig)
        {
            var verifier = new FalconSigner();
            verifier.Init(false, GetPublicKeyParameters());
            return verifier.VerifySignature(hash.ToBytes(), sig.ToDER());
        }

        public PubKey GetPubKey()
        {
            var pKey = GetPublicKeyParameters();
            return new PubKey(pKey.GetEncoded());
        }

        public FalconPublicKeyParameters GetPublicKeyParameters()
        {
            if (this._Key is FalconPublicKeyParameters)
                return (FalconPublicKeyParameters)this._Key;
            else
            {
                var pkeyBytes = this.PrivateKey.GetPublicKey();
                return new FalconPublicKeyParameters(_FParameters, pkeyBytes);
            }
        }
    }
}
