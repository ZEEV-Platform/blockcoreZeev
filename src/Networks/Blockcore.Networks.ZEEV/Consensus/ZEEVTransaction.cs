using System;
using System.IO;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin;
using Blockcore.Networks.ZEEV.Crypto.Blake2b;
using DBreeze.Utils;

namespace Blockcore.Networks.ZEEV.Consensus
{
    public class ZEEVTransaction : Transaction
    {
        public override uint256 GetHash()
        {
            uint256 h = null;
            uint256[] hashes = this.hashes;
            if (hashes != null)
            {
                h = hashes[0];
            }
            if (h != null)
                return h;

            using (var ms = new MemoryStream())
            {
                var stream = new BitcoinStream(ms, true)
                {
                    TransactionOptions = TransactionOptions.None
                };

                this.ReadWrite(stream);
                var bytes = ms.GetBuffer();
                Array.Resize(ref bytes, (int)ms.Length);
                var bytesHex = bytes.ToHexFromByteArray();
                var hash = Blake2B.ComputeHash(bytes, new Blake2BConfig() { OutputSizeInBytes = 32 });

                h = new uint256(hash);
            }

            hashes = this.hashes;
            if (hashes != null)
            {
                hashes[0] = h;
            }
            return h;
        }

        public override uint256 GetWitHash()
        {
            if (!this.HasWitness)
                return GetHash();

            uint256 h = null;
            uint256[] hashes = this.hashes;
            if (hashes != null)
            {
                h = hashes[1];
            }
            if (h != null)
                return h;

            using (var ms = new MemoryStream())
            {
                var stream = new BitcoinStream(ms, true)
                {
                    TransactionOptions = TransactionOptions.Witness
                };

                this.ReadWrite(stream);
                var bytes = ms.GetBuffer();
                Array.Resize(ref bytes, (int)ms.Length);
                var bytesHex = bytes.ToHexFromByteArray();
                var hash = Blake2B.ComputeHash(bytes, new Blake2BConfig() { OutputSizeInBytes = 32 });

                h = new uint256(hash);
            }

            hashes = this.hashes;
            if (hashes != null)
            {
                hashes[1] = h;
            }
            return h;
        }
    }
}
