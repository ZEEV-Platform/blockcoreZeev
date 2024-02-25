using System;
using System.Collections.Generic;
using Blockcore.Consensus;
using Blockcore.Features.Consensus.Rules.CommonRules;
using Blockcore.Features.Consensus.Rules.UtxosetRules;
using Blockcore.Features.MemoryPool.Rules;
using Blockcore.Networks.ZEEV.Rules;
using Blockcore.Consensus.BlockInfo;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using DBreeze.Utils;

namespace Blockcore.Networks.ZEEV.Consensus
{
    public class ZEEVNetwork : Network
    {
        public Block CreateGenesisBlock(ZEEVConsensusFactory consensusFactory, uint nTime, uint nNonce, uint nBits, int nVersion)
        {
            string pszTimestamp = "Benjamin is a ravenous wolf; in the morning he devours the prey, in the evening he divides the plunder.";
            var genesisOutputScript = new Script(Op.GetPushOp(Encoders.Hex.DecodeData("04678afdb0fe5548271967f1a67130b7105cd6a828e03909a67962e0ea1f61deb649f6bc3f4cef38c4f35504e51ec112de5c384df7ba0b8d578a4c702b6bf11d5f")), OpcodeType.OP_CHECKSIG);

            var txNew = consensusFactory.CreateTransaction();
            txNew.Version = 1;
            txNew.AddInput(new TxIn()
            {
                ScriptSig = new Script(Op.GetPushOp(486604799), new Op()
                {
                    Code = (OpcodeType)0x1,
                    PushData = new[] { (byte)4 }
                }, Op.GetPushOp(Encoders.ASCII.DecodeData(pszTimestamp)))
            });
            txNew.AddOutput(new TxOut()
            {
                Value = Money.Zero,
                ScriptPubKey = genesisOutputScript
            });

            Block genesis = consensusFactory.CreateBlock();
            ((ZEEVBlockHeader)genesis.Header).BlockTime = Utils.UnixTimeToDateTime(nTime);
            ((ZEEVBlockHeader)genesis.Header).Bits = nBits;
            ((ZEEVBlockHeader)genesis.Header).Nonce = nNonce;
            ((ZEEVBlockHeader)genesis.Header).Version = nVersion;
            genesis.Transactions.Add(txNew);
            ((ZEEVBlockHeader)genesis.Header).HashPrevBlock = uint256.Zero;
            ((ZEEVBlockHeader)genesis.Header).HashReservedRoot = uint256.Zero;
            ((ZEEVBlockHeader)genesis.Header).HashMask = uint256.Zero;
            ((ZEEVBlockHeader)genesis.Header).HashTreeRoot = uint256.Zero;
            ((ZEEVBlockHeader)genesis.Header).HashWitnessRoot = uint256.Zero;

            genesis.UpdateMerkleRoot();
            
            return genesis;
        }

        protected void RegisterRules(IConsensus consensus)
        {
            consensus.ConsensusRules
                .Register<HeaderTimeChecksRule>()
                .Register<ZEEVCheckDifficultyPowRule>()
                .Register<ZEEVHeaderVersionRule>()
                .Register<ZEEVCheckTimeDelayPowRule>();

            consensus.ConsensusRules
                .Register<ZEEVBlockMerkleRootRule>();

            consensus.ConsensusRules
                .Register<SetActivationDeploymentsPartialValidationRule>()

                .Register<TransactionLocktimeActivationRule>() // implements BIP113
                .Register<CoinbaseHeightActivationRule>() // implements BIP34
                .Register<ZEEVWitnessCommitmentsRule>() // BIP141, BIP144
                .Register<BlockSizeRule>()

                // rules that are inside the method CheckBlock
                .Register<EnsureCoinbaseRule>()
                .Register<CheckPowTransactionRule>()
                .Register<CheckSigOpsRule>();

            consensus.ConsensusRules
                .Register<SetActivationDeploymentsFullValidationRule>()

                // rules that require the store to be loaded (coinview)
                .Register<FetchUtxosetRule>()
                .Register<TransactionDuplicationActivationRule>() // implements BIP30
                .Register<ZEEVCheckPowUtxosetPowRule>()// implements BIP68, MaxSigOps and BlockReward calculation
                .Register<PushUtxosetRule>()
                .Register<FlushUtxosetRule>();
        }

        protected void RegisterMempoolRules(IConsensus consensus)
        {
            consensus.MempoolRules = new List<Type>()
            {
                typeof(CheckConflictsMempoolRule),
                typeof(CheckCoinViewMempoolRule),
                typeof(CreateMempoolEntryMempoolRule),
                typeof(CheckSigOpsMempoolRule),
                typeof(CheckFeeMempoolRule),
                typeof(CheckRateLimitMempoolRule),
                typeof(CheckAncestorsMempoolRule),
                typeof(CheckReplacementMempoolRule),
                typeof(CheckAllInputsMempoolRule),
                typeof(CheckTxOutDustRule)
            };
        }
    }
}
