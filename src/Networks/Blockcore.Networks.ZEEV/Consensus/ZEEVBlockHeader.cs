using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using Blockcore.Consensus;
using Blockcore.Consensus.BlockInfo;
using Blockcore.Consensus.Chain;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.Crypto;
using Blockcore.Networks.ZEEV.Components;
using Blockcore.Networks.ZEEV.Crypto;
using Blockcore.Networks.ZEEV.Crypto.Blake2b;
using DBreeze.Utils;
using HashLib;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Polly;
using static System.Net.Mime.MediaTypeNames;
using static Blockcore.Features.Consensus.CoinViews.Coindb.FasterCoindb;

namespace Blockcore.Networks.ZEEV.Consensus
{
    public class ZEEVBlockHeader : BlockHeader
    {
        protected byte[] timeBytes = new byte[8];
        private byte[] extraNonceBytes = new byte[24];
        public byte[] ExtraNonce
        {
            get { return this.extraNonceBytes; }
            set { this.extraNonceBytes = value; }
        }

        private byte[] hashTreeRootBytes = new byte[32];
        private uint256 hashTreeRoot;
        public uint256 HashTreeRoot
        {
            get { return this.hashTreeRoot; }
            set
            {
                this.hashTreeRoot = value;
                this.hashTreeRootBytes = value.ToBytes(false);
            }
        }

        private byte[] hashReservedRootBytes = new byte[32];
        private uint256 hashReservedRoot;
        public uint256 HashReservedRoot
        {
            get { return this.hashReservedRoot; }
            set
            {
                this.hashReservedRoot = value;
                this.hashReservedRootBytes = value.ToBytes(false);
            }
        }

        private byte[] hashWitnessRootBytes = new byte[32];
        private uint256 hashWitnessRoot;
        public uint256 HashWitnessRoot
        {
            get { return this.hashWitnessRoot; }
            set
            {
                this.hashWitnessRoot = value;
                this.hashWitnessRootBytes = value.ToBytes(false);
            }
        }

        private byte[] hashMerkleRootBytes = new byte[32];
        public override uint256 HashMerkleRoot
        {
            get { return this.hashMerkleRoot; }
            set
            {
                this.hashMerkleRoot = value;
                this.hashMerkleRootBytes = value.ToBytes(false);
            }
        }

        private byte[] hashPrevBlockBytes = new byte[32];
        public override uint256 HashPrevBlock
        {
            get { return this.hashPrevBlock; }
            set
            {
                this.hashPrevBlock = value;
                this.hashPrevBlockBytes = value.ToBytes(false);
            }
        }

        private byte[] hashCommitBytes = new byte[32];

        private byte[] hashMaskBytes = new byte[32];
        private uint256 hashMask;
        public uint256 HashMask
        {
            get { return this.hashMask; }
            set { 
                this.hashMask = value;
                this.hashMaskBytes = value.ToBytes(false);
            }
        }

        private byte[] paddingBytes = new byte[20];
        public override long HeaderSize => 256;
        public new int CurrentVersion = 0;
        public ConsensusProtocol Consensus { get; set; }

        public ZEEVBlockHeader(ConsensusProtocol consensus)
        {
            this.version = 0;
            this.CurrentVersion = 0;
            this.Consensus = consensus;
            this.HashReservedRoot = new uint256();
            this.HashWitnessRoot = new uint256();
            this.HashTreeRoot = new uint256();
            this.HashMask = new uint256();
            this.HashPrevBlock = new uint256();
            this.HashMerkleRoot = new uint256();
        }

        public override uint256 GetHash()
        {
            uint256 hash = null;
            uint256[] innerHashes = this.hashes;

            if (innerHashes != null)
                hash = innerHashes[0];

            if (hash != null)
                return hash;

            hash = this.GetPoWHash();

            innerHashes = this.hashes;
            if (innerHashes != null)
            {
                innerHashes[0] = hash;
            }

            return hash;
        }

        public override uint256 GetPoWHash()
        {
            using (var ms = new MemoryStream())
            {
                this.ReadWriteHashingStream(new BitcoinStream(ms, true));

                var shareHash = HandShake.Instance.Hash(this.ToMiner());
                var shareHashBytes = shareHash.ToBytes();
                var hashPowBytes = new byte[32];
                for (int i = 0; i < hashPowBytes.Length; i++)
                {
                    hashPowBytes[i] = (byte)(shareHashBytes[i % 32] ^ this.hashMaskBytes[i % 32]);
                }

                var powHash = new uint256(hashPowBytes);

                return powHash;
            }
        }

        public new Target GetWorkRequired(Network network, ChainedHeader prev)
        {
            return GetWorkRequired(new ChainedHeader(this, this.GetHash(), prev), (ZEEVConsensus)network.Consensus);
        }

        public Target GetWorkRequired(ChainedHeader chainedHeaderToValidate, ZEEVConsensus consensus)
        {
            ZEEVDigiShield digiShield = new ZEEVDigiShield();

            return digiShield.GetWorkRequired(chainedHeaderToValidate, consensus);
        }

        /*
## BLOCK HNS HEADERS
----------------

size:   data:
4       nonce
8       timestamp
32      prevBlock
32      treeRoot
24      extraNonce
32      reservedRoot
32      witnessRoot
32      merkleRoot
4       version
4       bits
32      mask
*/

        #region IBitcoinSerializable Members
        public override void ReadWrite(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.nonce);

            if (stream.Serializing)
            {
                var longTime = Convert.ToUInt64(this.time);
                this.timeBytes = BitConverter.GetBytes(longTime);
                stream.ReadWriteBytes(ref this.timeBytes);
                stream.ReadWriteBytes(ref this.hashPrevBlockBytes);
                stream.ReadWriteBytes(ref this.hashTreeRootBytes);
                stream.ReadWriteBytes(ref this.extraNonceBytes);
                stream.ReadWriteBytes(ref this.hashReservedRootBytes);
                stream.ReadWriteBytes(ref this.hashWitnessRootBytes);
                stream.ReadWriteBytes(ref this.hashMerkleRootBytes);

                stream.ReadWrite(ref this.version);

               // stream.IsBigEndian = true;
                stream.ReadWrite(ref this.bits);
               // stream.IsBigEndian = false;

                //var reversedBitBytes = new byte[4];
                //this.bitsBytes.CopyTo(reversedBitBytes, 0);
                //Array.Reverse(reversedBitBytes);
                //stream.ReadWriteBytes(ref reversedBitBytes);

                stream.ReadWriteBytes(ref this.hashMaskBytes);
            }
            else
            {
                stream.ReadWriteBytes(ref this.timeBytes);
                this.time = (uint)BitConverter.ToUInt64(this.timeBytes, 0);

                stream.ReadWriteBytes(ref this.hashPrevBlockBytes);
                this.hashPrevBlock = Uint256String(ref this.hashPrevBlockBytes);

                stream.ReadWriteBytes(ref this.hashTreeRootBytes);
                this.hashTreeRoot = Uint256String(ref this.hashTreeRootBytes);

                stream.ReadWriteBytes(ref this.extraNonceBytes);

                stream.ReadWriteBytes(ref this.hashReservedRootBytes);
                this.hashReservedRoot = Uint256String(ref this.hashReservedRootBytes);

                stream.ReadWriteBytes(ref this.hashWitnessRootBytes);
                this.hashWitnessRoot = Uint256String(ref this.hashWitnessRootBytes);

                stream.ReadWriteBytes(ref this.hashMerkleRootBytes);
                this.hashMerkleRoot = Uint256String(ref this.hashMerkleRootBytes);

                stream.ReadWrite(ref this.version);
                this.CurrentVersion = this.version;

           //     stream.IsBigEndian = true;
                stream.ReadWrite(ref this.bits);
            //    stream.IsBigEndian = false;

               // var sss = this.Bits;
               // var bitsBytes = new byte[4];
               // stream.ReadWriteBytes(ref bitsBytes);
               //// Array.Reverse(bitsBytes);
               // this.bits = BitConverter.ToUInt32(bitsBytes, 0);
               // StringBuilder hex2 = new StringBuilder(bitsBytes.Length * 2);
               // foreach (byte b in bitsBytes)
               //     hex2.AppendFormat("{0:x2}", b);
               // var sfff2 = hex2.ToString();

                //var s = new Target(this.bits);
                stream.ReadWriteBytes(ref this.hashMaskBytes);
                this.hashMask = Uint256String(ref this.hashMaskBytes);
            }
        }

        private uint256 Uint256String(ref byte[] buffer)
        {
            string bufferString = BitConverter.ToString(buffer).Replace("-", string.Empty);
            return new uint256(bufferString);
        }

        #endregion IBitcoinSerializable Members

        /// <summary>Populates stream with items that will be used during hash calculation.</summary>
        protected override void ReadWriteHashingStream(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.nonce);

            if (stream.Serializing)
            {
                var longTime = Convert.ToUInt64(this.time);
                this.timeBytes = BitConverter.GetBytes(longTime);
                stream.ReadWriteBytes(ref this.timeBytes);
                stream.ReadWriteBytes(ref this.hashPrevBlockBytes);
                stream.ReadWriteBytes(ref this.hashTreeRootBytes);
                stream.ReadWriteBytes(ref this.extraNonceBytes);
                stream.ReadWriteBytes(ref this.hashReservedRootBytes);
                stream.ReadWriteBytes(ref this.hashWitnessRootBytes);
                stream.ReadWriteBytes(ref this.hashMerkleRootBytes);
                stream.ReadWrite(ref this.version);

            //    stream.IsBigEndian = true;
                stream.ReadWrite(ref this.bits);
            //    stream.IsBigEndian = false;

                //var reversedBitBytes = new byte[4];
                //this.bitsBytes.CopyTo(reversedBitBytes, 0);
                //Array.Reverse(reversedBitBytes);
                //stream.ReadWriteBytes(ref reversedBitBytes);

                stream.ReadWriteBytes(ref this.hashMaskBytes);
            }
            else
            {
                stream.ReadWriteBytes(ref this.timeBytes);
                this.time = (uint)BitConverter.ToUInt64(this.timeBytes, 0);

                stream.ReadWriteBytes(ref this.hashPrevBlockBytes);
                this.hashPrevBlock = Uint256String(ref this.hashPrevBlockBytes);

                stream.ReadWriteBytes(ref this.hashTreeRootBytes);
                this.hashTreeRoot = Uint256String(ref this.hashTreeRootBytes);

                stream.ReadWriteBytes(ref this.extraNonceBytes);

                stream.ReadWriteBytes(ref this.hashReservedRootBytes);
                this.hashReservedRoot = Uint256String(ref this.hashReservedRootBytes);

                stream.ReadWriteBytes(ref this.hashWitnessRootBytes);
                this.hashWitnessRoot = Uint256String(ref this.hashWitnessRootBytes);

                stream.ReadWriteBytes(ref this.hashMerkleRootBytes);
                this.hashMerkleRoot = Uint256String(ref this.hashMerkleRootBytes);

                stream.ReadWrite(ref this.version);
                this.CurrentVersion = this.version;

            //    stream.IsBigEndian = true;
                stream.ReadWrite(ref this.bits);
            //    stream.IsBigEndian = false;

                //stream.ReadWriteBytes(ref this.bitsBytes);
                //Array.Reverse(this.bitsBytes);
                //this.bits = BitConverter.ToUInt32(this.bitsBytes, 0);
                //StringBuilder hex2 = new StringBuilder(this.bitsBytes.Length * 2);
                //foreach (byte b in this.bitsBytes)
                //    hex2.AppendFormat("{0:x2}", b);
                //var sfff2 = hex2.ToString();

                //var s = new Target(this.bits);
                stream.ReadWriteBytes(ref this.hashMaskBytes);
                this.hashMask = Uint256String(ref this.hashMaskBytes);
            }
        }

        public byte[] ToMiner()
        {
            using (var ms = new MemoryStream())
            {
                var stream = new BitcoinStream(ms, true);

                stream.ReadWrite(ref this.nonce);

                var longTime = Convert.ToUInt64(this.time);
                this.timeBytes = BitConverter.GetBytes(longTime);
                stream.ReadWriteBytes(ref this.timeBytes);

                this.paddingBytes = padding(20, this.hashPrevBlockBytes, this.hashTreeRootBytes);
                stream.ReadWriteBytes(ref this.paddingBytes);
                stream.ReadWriteBytes(ref this.hashPrevBlockBytes);
                stream.ReadWriteBytes(ref this.hashTreeRootBytes);

                this.hashCommitBytes = commitHash(this.hashPrevBlockBytes);
                stream.ReadWriteBytes(ref this.hashCommitBytes);

                //StringBuilder hex2 = new StringBuilder(this.hashCommitBytes.Length * 2);
                //foreach (byte b in this.hashCommitBytes)
                //    hex2.AppendFormat("{0:x2}", b);
                //var sfff2 = hex2.ToString();

                stream.ReadWriteBytes(ref this.extraNonceBytes);
                stream.ReadWriteBytes(ref this.hashReservedRootBytes);
                stream.ReadWriteBytes(ref this.hashWitnessRootBytes);
                stream.ReadWriteBytes(ref this.hashMerkleRootBytes);
                stream.ReadWrite(ref this.version);

             //   stream.IsBigEndian = true;
                stream.ReadWrite(ref this.bits);
             //   stream.IsBigEndian = false;

                //var reversedBitBytes = new byte[4];
                //this.bitsBytes.CopyTo(reversedBitBytes, 0);
                //Array.Reverse(reversedBitBytes);
                //stream.ReadWriteBytes(ref reversedBitBytes);

                var bytes = ms.GetBuffer();
                Array.Resize(ref bytes, (int)ms.Length);
                return bytes;
            }
        }

        private byte[] maskHash(byte[] prevBlockHash)
        {
            var blake2bConfig = new Blake2BConfig();
            blake2bConfig.OutputSizeInBytes = 32;
            return Blake2B.ComputeHash(prevBlockHash.Concat(new byte[32]).ToArray(), blake2bConfig);
        }

        private byte[] subHash()
        {
            using (var ms = new MemoryStream())
            {
                var stream = new BitcoinStream(ms, true);

                stream.ReadWriteBytes(ref this.extraNonceBytes);
                stream.ReadWriteBytes(ref this.hashReservedRootBytes);
                stream.ReadWriteBytes(ref this.hashWitnessRootBytes);
                stream.ReadWriteBytes(ref this.hashMerkleRootBytes);
                stream.ReadWrite(ref this.version);

            //    stream.IsBigEndian = true;
                stream.ReadWrite(ref this.bits);
            //    stream.IsBigEndian = false;

                //var reversedBitBytes = new byte[4];
                //this.bitsBytes.CopyTo(reversedBitBytes, 0);
                //Array.Reverse(reversedBitBytes);
                //stream.ReadWriteBytes(ref reversedBitBytes);

                var bytes = ms.GetBuffer();
                Array.Resize(ref bytes, (int)ms.Length);

                var blake2bConfig = new Blake2BConfig();
                blake2bConfig.OutputSizeInBytes = 32;
                return Blake2B.ComputeHash(bytes, blake2bConfig);
            }
        }

        private byte[] commitHash(byte[] prevBlockHash)
        {
            var blake2bConfig = new Blake2BConfig();
            blake2bConfig.OutputSizeInBytes = 32;
            return Blake2B.ComputeHash(subHash().Concat(maskHash(prevBlockHash)), blake2bConfig);
        }

        private byte[] padding(int size, byte[] prevBlock, byte[] treeRoot)
        {
            var pad = new byte[size];

            for (int i = 0; i < size; i++)
            {
                pad[i] = (byte)(prevBlock[i % 32] ^ treeRoot[i % 32]);
            }

            return pad;
        }

        public override bool CheckProofOfWork()
        {
            var target = this.Bits.ToUInt256();

            if (target.CompareTo(uint256.Zero) <= 0) return false;
            if (target.ToBytes(false).Length > 256) return false;

            if (GetPoWHash() > target)
                return false;

            return true;
        }

       // public uint256 GetPowHash

        public bool VerifyPOW(uint256 hash, Target bits)
        {
            //exports.verifyPOW = function verifyPOW(hash, bits) {
            //    const target = exports.fromCompact(bits);

            //    if (target.isNeg() || target.isZero())
            //        return false;

            //    if (target.bitLength() > 256)
            //        return false;

            //    const num = new BN(hash, 'be');

            //    if (num.gt(target))
            //        return false;

            //    return true;
            //};

            var target = bits.ToUInt256();

            if (target.CompareTo(uint256.Zero) <= 0) return false;
            if (target.ToBytes(false).Length > 256) return false;

            if (hash > target)
                return false;

            return true;
        }
    }
}