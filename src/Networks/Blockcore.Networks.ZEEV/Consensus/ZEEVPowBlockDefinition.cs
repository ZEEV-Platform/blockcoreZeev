using Blockcore.Base.Deployments;
using Blockcore.Consensus;
using Blockcore.Consensus.BlockInfo;
using Blockcore.Consensus.Chain;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.Features.Consensus.Rules.CommonRules;
using Blockcore.Features.MemoryPool;
using Blockcore.Features.MemoryPool.Interfaces;
using Blockcore.Features.Miner;
using Blockcore.Mining;
using Blockcore.NBitcoin;
using Blockcore.Networks;
using Blockcore.Networks.ZEEV.Rules;
using Blockcore.Utilities;
using Microsoft.Extensions.Logging;

namespace Blockcore.Networks.ZEEV.Consensus
{
    public class ZEEVPowBlockDefinition : BlockDefinition
    {
        private readonly IConsensusRuleEngine consensusRules;
        private readonly ILogger logger;

        public ZEEVPowBlockDefinition(
            IConsensusManager consensusManager,
            IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory,
            ITxMempool mempool,
            MempoolSchedulerLock mempoolLock,
            MinerSettings minerSettings,
            Network network,
            IConsensusRuleEngine consensusRules,
            NodeDeployments nodeDeployments,
            BlockDefinitionOptions options = null)
            : base(consensusManager, dateTimeProvider, loggerFactory, mempool, mempoolLock, minerSettings, network, nodeDeployments)
        {
            this.consensusRules = consensusRules;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public override void AddToBlock(TxMempoolEntry mempoolEntry)
        {
            this.AddTransactionToBlock(mempoolEntry.Transaction);
            this.UpdateBlockStatistics(mempoolEntry);
            this.UpdateTotalFees(mempoolEntry.Fee);
        }

        /// <inheritdoc/>
        public override BlockTemplate Build(ChainedHeader chainTip, Script scriptPubKey)
        {
            base.OnBuild(chainTip, scriptPubKey);

            return this.BlockTemplate;
        }

        /// <inheritdoc/>
        public override void UpdateHeaders()
        {
            base.UpdateBaseHeaders();

            this.block.Header.Bits = ((ZEEVBlockHeader)this.block.Header).GetWorkRequired(this.Network, this.ChainTip);
            ((ZEEVBlockHeader)this.block.Header).HashTreeRoot = GetHashBlockTreeRoot();

            this.block.Header.Version = ((ZEEVBlockHeader)this.block.Header).CurrentVersion;
        }

        private uint256 GetHashBlockTreeRoot()
        {
            var height = this.ChainTip.Height;
            var rootInterval = 36;

            int interval = height / rootInterval;

            if (interval == 0)
                return new uint256();

            var treeHashBlockTreeRoot = this.ChainTip.GetAncestor(interval * rootInterval);

            return treeHashBlockTreeRoot.HashBlock;
        }

        /// <summary>
        /// Adds the coinbase commitment to the coinbase transaction according to  https://github.com/bitcoin/bips/blob/master/bip-0141.mediawiki.
        /// </summary>
        /// <param name="block">The new block that is being mined.</param>
        /// <seealso cref="https://github.com/bitcoin/bitcoin/blob/master/src/validation.cpp"/>
        public override void AddOrUpdateCoinbaseCommitmentToBlock(Block block)
        {
            ZEEVWitnessCommitmentsRule.ClearWitnessCommitment(this.Network, block);
            ZEEVWitnessCommitmentsRule.CreateWitnessCommitment(this.Network, block);
        }
    }
}