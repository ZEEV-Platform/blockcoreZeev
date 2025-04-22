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
    internal class FalconKey
    {
        public FalconPrivateKeyParameters PrivateKey
        {
            get
            {
                return this._Key as FalconPrivateKeyParameters;
            }
        }

        private readonly FalconKeyParameters _Key;
        //public static readonly int HALF_CURVE_ORDER = null;
        public static int CURVE_ORDER;
        //public static readonly ECDomainParameters CURVE = null;
        //public static readonly X9ECParameters _Secp256k1;
        public static readonly FalconKeyGenerationParameters _KParam;
        public static readonly FalconParameters _FParameters = FalconParameters.falcon_512;
        public static readonly string _FDerObject = "1.3.9999.3.1";
        static FalconKey()
        {
            //Security.SecureRandom random = new Security.SecureRandom();
            //FalconKeyGenerationParameters _KParam = new FalconKeyGenerationParameters(random, _FParam);

            //Org.BouncyCastle.Security.SecureRandom random = new Org.BouncyCastle.Security.SecureRandom();
            //_KParam = new FalconKeyGenerationParameters(random, _FParam);

            // _Secp256k1 = CustomNamedCurves.Secp256k1;
            //CURVE = new ECDomainParameters(_Secp256k1.Curve, _Secp256k1.G, _Secp256k1.N, _Secp256k1.H);
            //HALF_CURVE_ORDER = _Secp256k1.N.ShiftRight(1);
            //CURVE_ORDER = _Secp256k1.N;
        }

        public FalconKey(byte[] vch, bool isPrivate)
        {
            var _FParam = FalconParameters.falcon_512;
            var random = new SecureRandom(new FakeSeedGenerator(vch));
            var _KParam = new FalconKeyGenerationParameters(random, _FParam);

            if (isPrivate)
            {
                var nist = new FalconNist(_KParam.Random, (uint)_KParam.Parameters.LogN, (uint)_KParam.Parameters.NonceLength);
                CURVE_ORDER = 1 << _KParam.Parameters.LogN;

                nist.crypto_sign_keypair(out byte[] pk, out byte[] f, out byte[] g, out byte[] F);

                this._Key = new FalconPrivateKeyParameters(_KParam.Parameters, f, g, F, pk);

                //var bcAns1 = Asn1Object.FromByteArray(vch);
                //var bcPKInfo = new PrivateKeyInfo(new AlgorithmIdentifier(new DerObjectIdentifier(_FDerObject)), bcAns1);
                //this._Key = (FalconPrivateKeyParameters)PqcPrivateKeyFactory.CreateKey(bcPKInfo);
            }
            else
            {
                this._Key = new FalconPublicKeyParameters(_FParameters, vch);
            }

            //if (isPrivate)
            //{
            //    var bcAns1 = Asn1Object.FromByteArray(vch);
            //    var bcPKInfo = new PrivateKeyInfo(new AlgorithmIdentifier(new DerObjectIdentifier(_FDerObject)), bcAns1);
            //    this._Key = (FalconPrivateKeyParameters)PqcPrivateKeyFactory.CreateKey(bcPKInfo);
            //}
            //else
            //{
            //    this._Key = new FalconPublicKeyParameters(_FParameters, vch);
            //}
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

        //public static ECKey RecoverFromSignature(int recId, FalconSignature sig, uint256 message, bool compressed)
        //{
        //    if (recId < 0)
        //        throw new ArgumentException("recId should be positive");
        //    if (sig.R.SignValue < 0)
        //        throw new ArgumentException("r should be positive");
        //    if (sig.S.SignValue < 0)
        //        throw new ArgumentException("s should be positive");
        //    if (message == null)
        //        throw new ArgumentNullException("message");


        //    X9ECParameters curve = Secp256k1;

        //    // 1.0 For j from 0 to h   (h == recId here and the loop is outside this function)
        //    //   1.1 Let x = r + jn

        //    BigInteger n = curve.N;
        //    BigInteger i = BigInteger.ValueOf((long)recId / 2);
        //    BigInteger x = sig.R.Add(i.Multiply(n));

        //    //   1.2. Convert the integer x to an octet string X of length mlen using the conversion routine
        //    //        specified in Section 2.3.7, where mlen = ⌈(log2 p)/8⌉ or mlen = ⌈m/8⌉.
        //    //   1.3. Convert the octet string (16 set binary digits)||X to an elliptic curve point R using the
        //    //        conversion routine specified in Section 2.3.4. If this conversion routine outputs “invalid”, then
        //    //        do another iteration of Step 1.
        //    //
        //    // More concisely, what these points mean is to use X as a compressed public key.
        //    BigInteger prime = ((SecP256K1Curve)curve.Curve).QQ;
        //    if (x.CompareTo(prime) >= 0)
        //    {
        //        return null;
        //    }

        //    // Compressed keys require you to know an extra bit of data about the y-coord as there are two possibilities.
        //    // So it's encoded in the recId.
        //    ECPoint R = DecompressKey(x, (recId & 1) == 1);
        //    //   1.4. If nR != point at infinity, then do another iteration of Step 1 (callers responsibility).

        //    if (!R.Multiply(n).IsInfinity)
        //        return null;

        //    //   1.5. Compute e from M using Steps 2 and 3 of ECDSA signature verification.
        //    var e = new BigInteger(1, message.ToBytes());
        //    //   1.6. For k from 1 to 2 do the following.   (loop is outside this function via iterating recId)
        //    //   1.6.1. Compute a candidate public key as:
        //    //               Q = mi(r) * (sR - eG)
        //    //
        //    // Where mi(x) is the modular multiplicative inverse. We transform this into the following:
        //    //               Q = (mi(r) * s ** R) + (mi(r) * -e ** G)
        //    // Where -e is the modular additive inverse of e, that is z such that z + e = 0 (mod n). In the above equation
        //    // ** is point multiplication and + is point addition (the EC group operator).
        //    //
        //    // We can find the additive inverse by subtracting e from zero then taking the mod. For example the additive
        //    // inverse of 3 modulo 11 is 8 because 3 + 8 mod 11 = 0, and -3 mod 11 = 8.

        //    BigInteger eInv = BigInteger.Zero.Subtract(e).Mod(n);
        //    BigInteger rInv = sig.R.ModInverse(n);
        //    BigInteger srInv = rInv.Multiply(sig.S).Mod(n);
        //    BigInteger eInvrInv = rInv.Multiply(eInv).Mod(n);
        //    ECPoint q = ECAlgorithms.SumOfTwoMultiplies(curve.G, eInvrInv, R, srInv);
        //    q = q.Normalize();
        //    if (compressed)
        //    {
        //        q = new SecP256K1Point(curve.Curve, q.XCoord, q.YCoord, true);
        //    }
        //    return new ECKey(q.GetEncoded(), false);
        //}

        //private static ECPoint DecompressKey(BigInteger xBN, bool yBit)
        //{
        //    ECCurve curve = Secp256k1.Curve;
        //    byte[] compEnc = X9IntegerConverter.IntegerToBytes(xBN, 1 + X9IntegerConverter.GetByteLength(curve));
        //    compEnc[0] = (byte)(yBit ? 0x03 : 0x02);
        //    return curve.DecodePoint(compEnc);
        //}

    }
}
