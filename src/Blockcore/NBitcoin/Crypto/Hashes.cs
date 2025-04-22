using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Blockcore.NBitcoin;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Parameters;
using System.IO;

namespace Blockcore.NBitcoin.Crypto
{
    public class Hashes
    {
        public byte[] HMACSHA3512(byte[] key, byte[] data)
        {
            var mac = new HMac(new Sha3Digest(512));
            mac.Init(new KeyParameter(key));
            mac.BlockUpdate(data, 0, data.Length);
            byte[] result = new byte[mac.GetMacSize()];
            mac.DoFinal(result, 0);
            return result;
        }

        public byte[] BIP32Hash(byte[] chainCode, uint nChild, byte header, byte[] data)
        {
            var num = new byte[4];
            num[0] = (byte)((nChild >> 24) & 0xFF);
            num[1] = (byte)((nChild >> 16) & 0xFF);
            num[2] = (byte)((nChild >> 8) & 0xFF);
            num[3] = (byte)((nChild >> 0) & 0xFF);

            return HMACSHA3512(chainCode,
                new byte[] { header }
                .Concat(data)
                .Concat(num).ToArray());
        }

        internal static byte[] SHA256(byte[] data)
        {
            return SHA256(data, 0, data.Length);
        }

        /// <summary>
        /// Hash Sha3
        /// </summary>
        internal static byte[] SHA256(byte[] data, int offset, int count)
        {
            var sha256 = new Sha3Digest(256);
            sha256.BlockUpdate(data, offset, count);
            var rv = new byte[32];
            sha256.DoFinal(rv, 0);
            return rv.SafeSubarray(0, 32);
        }

        public uint160 Hash160(byte[] data)
        {
            return Hash160(data, 0, data.Length);
        }

        public uint160 Hash160(byte[] data, int count)
        {
            return Hash160(data, 0, count);
        }

        /// <summary>
        /// Hash Sha3 with Blake2b 20 bytes only
        /// </summary>
        public uint160 Hash160(byte[] data, int offset, int count)
        {
            var sha = SHA256(data, offset, count);
            return new uint160(Blake2B.Blake2B160().ComputeHash(sha));
        }

        public uint256 Hash256(byte[] data)
        {
            return Hash256(data, 0, data.Length);
        }

        public uint256 Hash256(byte[] data, int count)
        {
            return Hash256(data, 0, count);
        }

        /// <summary>
        /// Hash Sha3 with double BlockUpdate
        /// </summary>
        public uint256 Hash256(byte[] data, int offset, int count)
        {
            var sha = new Sha3Digest(256);
            sha.BlockUpdate(data, offset, count);
            var rv = new byte[32];
            sha.DoFinal(rv, 0);
            sha.BlockUpdate(rv, 0, rv.Length);
            sha.DoFinal(rv, 0);
            return new uint256(rv.SafeSubarray(0, 32));
        }

        #region OPSha3Hashes

        public byte[] Sha3224(byte[] data)
        {
            return Sha3.Sha3224().ComputeHash(data);
        }
        public byte[] Sha3256(byte[] data)
        {
            return Sha3.Sha3256().ComputeHash(data);
        }
        public byte[] Sha3384(byte[] data)
        {
            return Sha3.Sha3384().ComputeHash(data);
        }
        public byte[] Sha3512(byte[] data)
        {
            return Sha3.Sha3512().ComputeHash(data);
        }

        #endregion OPSha3Hashes

        #region OPBlake2bHashes

        public byte[] Blake2B160(byte[] data)
        {
            return Blake2B.Blake2B160().ComputeHash(data);
        }
        public byte[] Blake2B224(byte[] data)
        {
            return Blake2B.Blake2B224().ComputeHash(data);
        }
        public byte[] Blake2B256(byte[] data)
        {
            return Blake2B.Blake2B256().ComputeHash(data);
        }
        public byte[] Blake2B384(byte[] data)
        {
            return Blake2B.Blake2B384().ComputeHash(data);
        }
        public byte[] Blake2B512(byte[] data)
        {
            return Blake2B.Blake2B512().ComputeHash(data);
        }

        #endregion OPBlake2bHashes

        #region MurmurHash3

        public uint MurmurHash3(uint nHashSeed, byte[] vDataToHash)
        {
            // The following is MurmurHash3 (x86_32), see https://gist.github.com/automatonic/3725443
            const uint c1 = 0xcc9e2d51;
            const uint c2 = 0x1b873593;

            uint h1 = nHashSeed;
            uint k1 = 0;
            uint streamLength = 0;

            using (var reader = new BinaryReader(new MemoryStream(vDataToHash)))
            {
                byte[] chunk = reader.ReadBytes(4);
                while (chunk.Length > 0)
                {
                    streamLength += (uint)chunk.Length;
                    switch (chunk.Length)
                    {
                        case 4:
                            /* Get four bytes from the input into an uint */
                            k1 = (uint)
                            (chunk[0]
                            | chunk[1] << 8
                            | chunk[2] << 16
                            | chunk[3] << 24);

                            /* bitmagic hash */
                            k1 *= c1;
                            k1 = rotl32(k1, 15);
                            k1 *= c2;

                            h1 ^= k1;
                            h1 = rotl32(h1, 13);
                            h1 = h1 * 5 + 0xe6546b64;
                            break;

                        case 3:
                            k1 = (uint)
                            (chunk[0]
                            | chunk[1] << 8
                            | chunk[2] << 16);
                            k1 *= c1;
                            k1 = rotl32(k1, 15);
                            k1 *= c2;
                            h1 ^= k1;
                            break;

                        case 2:
                            k1 = (uint)
                            (chunk[0]
                            | chunk[1] << 8);
                            k1 *= c1;
                            k1 = rotl32(k1, 15);
                            k1 *= c2;
                            h1 ^= k1;
                            break;

                        case 1:
                            k1 = (uint)(chunk[0]);
                            k1 *= c1;
                            k1 = rotl32(k1, 15);
                            k1 *= c2;
                            h1 ^= k1;
                            break;
                    }
                    chunk = reader.ReadBytes(4);
                }
            }
            // finalization, magic chants to wrap it all up
            h1 ^= streamLength;
            h1 = fmix(h1);

            unchecked //ignore overflow
            {
                return h1;
            }
        }

        private uint rotl32(uint x, byte r)
        {
            return (x << r) | (x >> (32 - r));
        }

        private ulong rotl64(ulong x, byte b)
        {
            return (((x) << (b)) | ((x) >> (64 - (b))));
        }

        private uint fmix(uint h)
        {
            h ^= h >> 16;
            h *= 0x85ebca6b;
            h ^= h >> 13;
            h *= 0xc2b2ae35;
            h ^= h >> 16;
            return h;
        }

        #endregion MurmurHash3
    }
}
