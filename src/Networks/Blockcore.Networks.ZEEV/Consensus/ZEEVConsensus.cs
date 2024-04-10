using System;
using System.Collections.Generic;
using Blockcore.Base.Deployments;
using Blockcore.Consensus;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.BouncyCastle.math;

namespace Blockcore.Networks.ZEEV.Consensus
{
    public class ZEEVConsensus : IConsensus
    {
        /// <inheritdoc />
        public long CoinbaseMaturity { get; set; }

        /// <inheritdoc />
        public Money PremineReward { get; }

        /// <inheritdoc />
        public long PremineHeight { get; }

        /// <inheritdoc />
        public Money ProofOfWorkReward { get; }

        /// <inheritdoc />
        public Money ProofOfStakeReward { get; }

        /// <inheritdoc />
        public uint MaxReorgLength { get; }

        /// <inheritdoc />
        public long MaxMoney { get; }

        /// <inheritdoc />
        public ConsensusOptions Options { get; set; }

        /// <inheritdoc />
        public BuriedDeploymentsArray BuriedDeployments { get; }

        /// <inheritdoc />
        public IBIP9DeploymentsArray BIP9Deployments { get; }

        /// <inheritdoc />
        public int SubsidyHalvingInterval { get; }

        /// <inheritdoc />
        public int MajorityEnforceBlockUpgrade { get; }

        /// <inheritdoc />
        public int MajorityRejectBlockOutdated { get; }

        /// <inheritdoc />
        public int MajorityWindow { get; }

        /// <inheritdoc />
        public uint256 BIP34Hash { get; }

        /// <inheritdoc />
        public Target PowLimit { get; }

        /// <inheritdoc />
        public TimeSpan TargetTimespan { get; }

        /// <inheritdoc />
        public TimeSpan TargetSpacing { get; }

        public bool PowAllowMinDifficultyBlocks { get; }

        /// <inheritdoc />
        public bool PosNoRetargeting { get; }

        /// <inheritdoc />
        public bool PowNoRetargeting { get; }

        public uint256 HashGenesisBlock { get; }

        /// <inheritdoc />
        public uint256 MinimumChainWork { get; }

        /// <inheritdoc />
        public int MinerConfirmationWindow { get; set; }

        /// <inheritdoc />
        public int CoinType { get; }

        /// <inheritdoc />
        public BigInteger ProofOfStakeLimit { get; }

        /// <inheritdoc />
        public BigInteger ProofOfStakeLimitV2 { get; }

        /// <inheritdoc />
        public int LastPOWBlock { get; set; }

        /// <inheritdoc />
        public bool PosEmptyCoinbase { get; set; }

        /// <inheritdoc />
        public bool PosUseTimeFieldInKernalHash { get; set; }

        /// <inheritdoc />
        public uint ProofOfStakeTimestampMask { get; set; }

        /// <inheritdoc />
        public bool IsProofOfStake { get; }

        /// <inheritdoc />
        public uint256 DefaultAssumeValid { get; }

        /// <inheritdoc />
        public ConsensusFactory ConsensusFactory { get; }

        /// <inheritdoc />
        public ConsensusRules ConsensusRules { get; }

        /// <inheritdoc />
        public List<Type> MempoolRules { get; set; }

        public TimeSpan PowTimeDelay { get; set; }

        public Money SubsidityDecrease { get; set; }

        public ZEEVConsensus(
            ConsensusFactory consensusFactory,
            ConsensusOptions consensusOptions,
            int coinType,
            uint256 hashGenesisBlock,
            int subsidyHalvingInterval,
            int majorityEnforceBlockUpgrade,
            int majorityRejectBlockOutdated,
            int majorityWindow,
            BuriedDeploymentsArray buriedDeployments,
            IBIP9DeploymentsArray bip9Deployments,
            uint256 bip34Hash,
            int minerConfirmationWindow,
            uint maxReorgLength,
            uint256 defaultAssumeValid,
            long maxMoney,
            long coinbaseMaturity,
            long premineHeight,
            Money premineReward,
            Money proofOfWorkReward,
            TimeSpan targetTimespan,
            TimeSpan targetSpacing,
            bool powAllowMinDifficultyBlocks,
            bool posNoRetargeting,
            bool powNoRetargeting,
            Target powLimit,
            uint256 minimumChainWork,
            bool isProofOfStake,
            int lastPowBlock,
            BigInteger proofOfStakeLimit,
            BigInteger proofOfStakeLimitV2,
            Money proofOfStakeReward,
            uint proofOfStakeTimestampMask,
            TimeSpan powTimeDelay,
            Money subsidityDecrease)
        {
            this.CoinbaseMaturity = coinbaseMaturity;
            this.PremineReward = premineReward;
            this.PremineHeight = premineHeight;
            this.ProofOfWorkReward = proofOfWorkReward;
            this.ProofOfStakeReward = proofOfStakeReward;
            this.MaxReorgLength = maxReorgLength;
            this.MaxMoney = maxMoney;
            this.Options = consensusOptions;
            this.BuriedDeployments = buriedDeployments;
            this.BIP9Deployments = bip9Deployments;
            this.SubsidyHalvingInterval = subsidyHalvingInterval;
            this.MajorityEnforceBlockUpgrade = majorityEnforceBlockUpgrade;
            this.MajorityRejectBlockOutdated = majorityRejectBlockOutdated;
            this.MajorityWindow = majorityWindow;
            this.BIP34Hash = bip34Hash;
            this.PowLimit = powLimit;
            this.TargetTimespan = targetTimespan;
            this.TargetSpacing = targetSpacing;
            this.PowAllowMinDifficultyBlocks = powAllowMinDifficultyBlocks;
            this.PosNoRetargeting = posNoRetargeting;
            this.PowNoRetargeting = powNoRetargeting;
            this.HashGenesisBlock = hashGenesisBlock;
            this.MinimumChainWork = minimumChainWork;
            this.MinerConfirmationWindow = minerConfirmationWindow;
            this.CoinType = coinType;
            this.ProofOfStakeLimit = proofOfStakeLimit;
            this.ProofOfStakeLimitV2 = proofOfStakeLimitV2;
            this.LastPOWBlock = lastPowBlock;
            this.IsProofOfStake = isProofOfStake;
            this.DefaultAssumeValid = defaultAssumeValid;
            this.ConsensusFactory = consensusFactory;
            this.ConsensusRules = new ConsensusRules();
            this.MempoolRules = new List<Type>();
            this.ProofOfStakeTimestampMask = proofOfStakeTimestampMask;
            this.PowTimeDelay = powTimeDelay;
            this.SubsidityDecrease = subsidityDecrease;
        }
    }
}