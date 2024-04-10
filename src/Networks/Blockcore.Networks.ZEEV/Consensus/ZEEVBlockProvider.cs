using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Blockcore.Consensus.BlockInfo;
using Blockcore.Consensus.Chain;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.Features.Miner;
using Blockcore.Mining;

namespace Blockcore.Networks.ZEEV.Consensus
{
    public class ZEEVBlockProvider : IBlockProvider
    {
        private readonly Network network;

        /// <summary>Defines how proof of work blocks are built.</summary>
        private readonly ZEEVPowBlockDefinition powBlockDefinition;

        /// <summary>Defines how proof of stake blocks are built.</summary>
        private readonly PosBlockDefinition posBlockDefinition;

        /// <summary>Defines how proof of work blocks are built on a Proof-of-Stake network.</summary>
        private readonly PosPowBlockDefinition posPowBlockDefinition;

        /// <param name="definitions">A list of block definitions that the builder can utilize.</param>
        public ZEEVBlockProvider(Network network, IEnumerable<BlockDefinition> definitions)
        {
            this.network = network;

            this.powBlockDefinition = definitions.OfType<ZEEVPowBlockDefinition>().FirstOrDefault();
            this.posBlockDefinition = definitions.OfType<PosBlockDefinition>().FirstOrDefault();
            this.posPowBlockDefinition = definitions.OfType<PosPowBlockDefinition>().FirstOrDefault();
        }

        /// <inheritdoc/>
        public BlockTemplate BuildPosBlock(ChainedHeader chainTip, Script script)
        {
            return this.posBlockDefinition.Build(chainTip, script);
        }

        /// <inheritdoc/>
        public BlockTemplate BuildPowBlock(ChainedHeader chainTip, Script script)
        {
            if (this.network.Consensus.IsProofOfStake)
                return this.posPowBlockDefinition.Build(chainTip, script);

            return this.powBlockDefinition.Build(chainTip, script);
        }

        /// <inheritdoc/>
        public void BlockModified(ChainedHeader chainTip, Block block)
        {
            if (this.network.Consensus.IsProofOfStake)
            {
                if (BlockStake.IsProofOfStake(block))
                {
                    this.posBlockDefinition.BlockModified(chainTip, block);
                }
                else
                {
                    this.posPowBlockDefinition.BlockModified(chainTip, block);
                }
            }

            this.powBlockDefinition.BlockModified(chainTip, block);
        }

    }
}