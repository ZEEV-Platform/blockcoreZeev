using System;
using Blockcore.Consensus;
using Blockcore.Consensus.BlockInfo;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin.DataEncoders;
using Blockcore.NBitcoin;

namespace Blockcore.Networks.ZEEV.Consensus
{
    public class ZEEVConsensusFactory : ConsensusFactory
    {
        public ZEEVConsensusFactory() : base()
        {
        }

        public override BlockHeader CreateBlockHeader()
        {
            return new ZEEVBlockHeader(this.Protocol);
        }

        /// <summary>
        /// Create a <see cref="Block"/> instance.
        /// </summary>
        public override Block CreateBlock()
        {
#pragma warning disable CS0618 // Type or member is obsolete
            return new Block(this.CreateBlockHeader());
#pragma warning restore CS0618 // Type or member is obsolete
        }

        public override Transaction CreateTransaction()
        {
            return new ZEEVTransaction();
        }

        public override Transaction CreateTransaction(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            var transaction = new ZEEVTransaction();
            transaction.ReadWrite(bytes, this);
            return transaction;
        }

        public override Transaction CreateTransaction(string hex)
        {
            if (hex == null)
                throw new ArgumentNullException(nameof(hex));

            return CreateTransaction(Encoders.Hex.DecodeData(hex));
        }
    }
}
