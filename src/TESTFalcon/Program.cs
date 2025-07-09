using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Cms;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Pqc.Crypto.Falcon;
using Org.BouncyCastle.Pqc.Crypto.Utilities;
using Org.BouncyCastle.Security;
using Xunit;

namespace FalconTest
{
    public class Program
    {
        public static readonly FalconParameters _FParam = FalconParameters.falcon_512;

        public static async Task Main(string[] args)
        {
            //MNEMONIC TEST
            var password = "ThisIsATest";
            var passphase = "Extra Seed Words";

            TestFalcon();
        }

        public static string ByteArrayToString(byte[] bytes)
        {
            if (bytes == null) return "null";
            string joinedBytes = string.Join(", ", bytes.Select(b => b.ToString()));
            return $"new byte[] {{ {joinedBytes} }}";
        }

        private static string BytesToHex(byte[] b)
        {
            return BitConverter.ToString(b).Replace("-", "");
        }

        private static void TestFalcon()
        {
            FalconKeyGenerationParameters parameters;
            SecureRandom random;
            uint logn;
            uint noncelen;
            int pk_size;

            var bytesSeeed = Convert.FromBase64String("0qKWVWHOxktsmWWBreVSgHM/qy1iiGeYzqG9HHBs8g0=");

            var _FParam = FalconParameters.falcon_512;
            random = new SecureRandom(new FakeSeedGenerator(bytesSeeed));

            var _KParam = new FalconKeyGenerationParameters(random, _FParam);

            parameters = (FalconKeyGenerationParameters)_KParam;
            random = _KParam.Random;
            logn = (uint)((FalconKeyGenerationParameters)_KParam).Parameters.LogN;
            noncelen = (uint)((FalconKeyGenerationParameters)_KParam).Parameters.NonceLength;
            var nist = new FalconNist(random, logn, noncelen);
            int n = 1 << (int)logn;
            pk_size = 1 + (14 * n / 8);

            nist.crypto_sign_keypair(out byte[] pk, out byte[] f, out byte[] g, out byte[] F);
            FalconParameters p = _KParam.Parameters;
            FalconPrivateKeyParameters privk = new FalconPrivateKeyParameters(p, f, g, F, pk);
            FalconPublicKeyParameters pubk = new FalconPublicKeyParameters(p, pk);
            FalconKeyParameters basevalue = privk;
            FalconKeyParameters basevalue2 = pubk;
           
            var s = new AsymmetricCipherKeyPair(pubk, privk);
          
            var pubrestore = new FalconPublicKeyParameters(_FParam, pubk.GetEncoded());
            var altSubPubEnc = pubk.GetEncoded();
            var altPrivEnc = privk.GetEncoded();
            var altPrivEncRestore = new FalconPrivateKeyParameters(_FParam, altPrivEnc);

            var pkInfo = PqcPrivateKeyInfoFactory.CreatePrivateKeyInfo(privk);
            var pkAsn1 = pkInfo.ToAsn1Object();
            var derBytes = pkAsn1.GetDerEncoded();

            //OWN GENERATION
            Asn1EncodableVector vd = new Asn1EncodableVector(3);
            vd.Add(new DerInteger(1));
            vd.Add(new AlgorithmIdentifier(new DerObjectIdentifier("1.3.9999.3.1")));

            var sequ = new DerSequence(
                new DerInteger(1),
                new DerOctetString(privk.GetSpolyLittleF()),
                new DerOctetString(privk.GetG()),
                new DerOctetString(privk.GetSpolyBigF()));

            var ssrrrrr = new DerOctetString(sequ);
            var ssrrrrbytes = ssrrrrr.GetOctets();
            var ssssss = DerOctetString.FromByteArray(ssrrrrr.GetOctets());
            vd.Add(ssrrrrr);

            var testssss = new PrivateKeyInfo(new AlgorithmIdentifier(
                new DerObjectIdentifier("1.3.9999.3.1")), new DerSequence(vd), null);
            FalconPrivateKeyParameters privParamsTestA = (FalconPrivateKeyParameters)PqcPrivateKeyFactory.CreateKey(testssss);
            //END OWN GENERATION

            //DECOMPILE AND COMPILE AGAIN
            Asn1InputStream asn1InputStream = new Asn1InputStream(derBytes);
            Asn1Object a = asn1InputStream.ReadObject();
            var toPkcs8Der = a.GetEncoded(Asn1Encodable.Der);

            var testRestore = Asn1Object.FromByteArray(toPkcs8Der);

            var newPKInfo = new PrivateKeyInfo(new AlgorithmIdentifier(new DerObjectIdentifier("1.3.9999.3.1")), testRestore);
            var test = newPKInfo.ToAsn1Object();
            var test2 = test.GetDerEncoded();
            FalconPrivateKeyParameters privParamsTestB = (FalconPrivateKeyParameters)PqcPrivateKeyFactory.CreateKey(newPKInfo);

            Assert.True(BytesToHex(privParamsTestA.GetSpolyLittleF()) == BytesToHex(privParamsTestB.GetSpolyLittleF()));
            Assert.True(BytesToHex(privParamsTestA.GetSpolyBigF()) == BytesToHex(privParamsTestB.GetSpolyBigF()));
            Assert.True(BytesToHex(privParamsTestA.GetG()) == BytesToHex(privParamsTestB.GetG()));
        }
    }
}