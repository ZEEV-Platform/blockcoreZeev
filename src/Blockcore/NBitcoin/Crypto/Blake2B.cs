using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Org.BouncyCastle.Crypto.Digests;

namespace Blockcore.NBitcoin.Crypto
{
    public class Blake2B : HashAlgorithm
    {
        private readonly Blake2bDigest _digest;
        private readonly int _hashBitLength;

        internal Blake2B(int hashBitLength)
        {
            _hashBitLength = hashBitLength;
            _digest = new Blake2bDigest(_hashBitLength);
        }

        public override int HashSize => _hashBitLength;

        public static Blake2B Blake2B160()
        {
            return new Blake2B(160);
        }

        public static Blake2B Blake2B224()
        {
            return new Blake2B(224);
        }

        public static Blake2B Blake2B256()
        {
            return new Blake2B(256);
        }

        public static Blake2B Blake2B384()
        {
            return new Blake2B(384);
        }

        public static Blake2B Blake2B512()
        {
            return new Blake2B(512);
        }

        public override void Initialize()
        {
            HashValue = new byte[_digest.GetDigestSize()];
        }

        protected override void HashCore(byte[] array, int ibStart, int cbSize)
        {
            if (HashValue == null) Initialize();
            _digest.BlockUpdate(array, ibStart, cbSize);
        }

        protected override byte[] HashFinal()
        {
            _digest.DoFinal(HashValue, 0);
            return HashValue;
        }
    }
}
