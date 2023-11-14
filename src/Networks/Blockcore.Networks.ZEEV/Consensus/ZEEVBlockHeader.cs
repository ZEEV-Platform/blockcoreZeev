using System;
using System.IO;
using Blockcore.Consensus;
using Blockcore.Consensus.BlockInfo;
using Blockcore.Consensus.Chain;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.Crypto;
using Blockcore.Networks.ZEEV.Components;
using Blockcore.Networks.ZEEV.Crypto;

namespace Blockcore.Networks.ZEEV.Consensus
{
    public class ZEEVBlockHeader : BlockHeader
    {
        protected byte[] reservedBytes = new byte[20];
        protected byte[] timeBytes = new byte[8];
        private byte[] extraNonce = new byte[24];
        public byte[] ExtraNonce
        {
            get { return this.extraNonce; }
            set { this.extraNonce = value; }
        }

        private uint256 hashTreeRoot;
        public uint256 HashTreeRoot { get { return this.hashTreeRoot; } set { this.hashTreeRoot = value; } }

        private uint256 hashCommitHash;
        public uint256 HashCommitHash { get { return this.hashCommitHash; } set { this.hashCommitHash = value; } }

        private uint256 hashReservedRoot;
        public uint256 HashReservedRoot { get { return this.hashReservedRoot; } set { this.hashReservedRoot = value; } }

        private uint256 hashWitnessRoot;
        public uint256 HashWitnessRoot { get { return this.hashWitnessRoot; } set { this.hashWitnessRoot = value; } } 

        public override long HeaderSize => 256;

        public ConsensusProtocol Consensus { get; set; }

        public ZEEVBlockHeader(ConsensusProtocol consensus)
        {
            this.Consensus = consensus;
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

                string hex = BitConverter.ToString(this.ToBytes()).Replace("-", string.Empty);
                return HandShake.Instance.Hash(this.ToBytes());
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
            }
            else
            {
                stream.ReadWriteBytes(ref this.timeBytes);
                this.time = (uint)BitConverter.ToUInt64(this.timeBytes, 0);
            }

            stream.ReadWriteBytes(ref this.reservedBytes);
            stream.ReadWrite(ref this.hashPrevBlock);
            stream.ReadWrite(ref this.hashTreeRoot);
            stream.ReadWrite(ref this.hashCommitHash);
            stream.ReadWriteBytes(ref this.extraNonce);
            stream.ReadWrite(ref this.hashReservedRoot);
            stream.ReadWrite(ref this.hashWitnessRoot);
            stream.ReadWrite(ref this.hashMerkleRoot);
            stream.ReadWrite(ref this.version);
            stream.ReadWrite(ref this.bits);
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
            }
            else
            {
                stream.ReadWriteBytes(ref this.timeBytes);
                this.time = (uint)BitConverter.ToUInt64(this.timeBytes, 0);
            }
            
            stream.ReadWriteBytes(ref this.reservedBytes);
            stream.ReadWrite(ref this.hashPrevBlock);
            stream.ReadWrite(ref this.hashTreeRoot);
            stream.ReadWrite(ref this.hashCommitHash);
            stream.ReadWriteBytes(ref this.extraNonce);
            stream.ReadWrite(ref this.hashReservedRoot);
            stream.ReadWrite(ref this.hashWitnessRoot);
            stream.ReadWrite(ref this.hashMerkleRoot);
            stream.ReadWrite(ref this.version);
            stream.ReadWrite(ref this.bits);
        }
    }
}