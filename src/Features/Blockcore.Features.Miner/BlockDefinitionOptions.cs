using System;
using Blockcore.NBitcoin;
using Blockcore.Networks;

namespace Blockcore.Features.Miner
{
    /// <summary>
    /// Semi- immutable settings to be used by <see cref="BlockDefinition"/>.
    /// </summary>
    public sealed class BlockDefinitionOptions
    {
        /// <summary>Maximum block weight (in weight units) for the blocks created by miner.</summary>
        public uint BlockMaxWeight { get; private set; }

        /// <summary>Maximum block size (in bytes) for the blocks created by miner.</summary>
        public uint BlockMaxSize { get; private set; }

        /// <summary>Minimum block size (in bytes) for the blocks created by miner.</summary>
        public uint BlockMinSize { get; private set; }

        /// <summary>Minimum fee rate for transactions to be included in blocks created by miner.</summary>
        public FeeRate BlockMinFeeRate { get; private set; }

        public BlockDefinitionOptions(uint blockMaxWeight, uint blockMaxSize, int blockMinFeeRate)
        {
            this.BlockMaxWeight = blockMaxWeight;
            this.BlockMaxSize = blockMaxSize;
            this.BlockMinFeeRate = new FeeRate(blockMinFeeRate);
        }

        /// <summary>
        /// Restrict the options to within those allowed by network consensus rules.
        /// If set values are outside those allowed by consensus, set to nearest allowed value (minimum or maximum).
        /// </summary>
        public BlockDefinitionOptions RestrictForNetwork(Network network)
        {
            this.BlockMinSize = (uint) network.Consensus.Options.MinBlockSize;
            uint minAllowedBlockWeight = this.BlockMinSize * (uint) network.Consensus.Options.WitnessScaleFactor;
            this.BlockMaxWeight = Math.Max(minAllowedBlockWeight, Math.Min(network.Consensus.Options.MaxBlockWeight, this.BlockMaxWeight));
            this.BlockMaxSize = Math.Max(this.BlockMinSize, Math.Min(network.Consensus.Options.MaxBlockSerializedSize, this.BlockMaxSize));
            this.BlockMinFeeRate = new FeeRate(Math.Max(network.Consensus.Options.MinBlockFeeRate, this.BlockMinFeeRate.FeePerK));

            return this;
        }
    }
}