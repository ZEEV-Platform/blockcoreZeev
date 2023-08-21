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
    }
}
