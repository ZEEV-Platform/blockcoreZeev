using Blockcore.Consensus;
using Blockcore.Consensus.BlockInfo;

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
    }
}
