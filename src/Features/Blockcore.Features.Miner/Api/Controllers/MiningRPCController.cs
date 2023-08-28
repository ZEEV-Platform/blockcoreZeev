using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Blockcore.Consensus;
using Blockcore.Consensus.BlockInfo;
using Blockcore.Consensus.Chain;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.Controllers;
using Blockcore.Controllers.Models;
using Blockcore.Features.Consensus.Rules.UtxosetRules;
using Blockcore.Features.MemoryPool.Interfaces;
using Blockcore.Features.Miner.Api.Models;
using Blockcore.Features.Miner.Interfaces;
using Blockcore.Features.RPC;
using Blockcore.Features.RPC.Exceptions;
using Blockcore.Features.Wallet.Interfaces;
using Blockcore.Features.Wallet.Types;
using Blockcore.NBitcoin;
using Blockcore.Interfaces;
using Blockcore.Mining;
using Blockcore.Networks;
using Blockcore.NBitcoin.DataEncoders;
using Blockcore.NBitcoin.Protocol;
using Blockcore.Utilities;
using Blockcore.Utilities.Extensions;
using Blockcore.Utilities.JsonErrors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Polly;
using Script = Blockcore.Consensus.ScriptInfo.Script;

namespace Blockcore.Features.Miner.Api.Controllers
{
    /// <summary>
    /// RPC controller for calls related to PoW mining and PoS minting.
    /// </summary>
    public class MiningRpcController : FeatureController
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>PoW miner.</summary>
        private readonly IPowMining powMining;

        /// <summary>Full node.</summary>
        private readonly IFullNode fullNode;

        /// <summary>Wallet manager.</summary>
        private readonly IWalletManager walletManager;

        /// <summary>An interface implementation used to retrieve the network difficulty target.</summary>
        private readonly INetworkDifficulty networkDifficulty;

        private readonly IBlockProvider blockProvider;

        private readonly ITxMempool txMempool;

        private static object lockGetBlockTemplate = new object();
        private static object lockSubmitBlock = new object();

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="powMining">PoW miner.</param>
        /// <param name="fullNode">Full node to offer mining RPC.</param>
        /// <param name="loggerFactory">Factory to be used to create logger for the node.</param>
        /// <param name="walletManager">The wallet manager.</param>
        public MiningRpcController(
            IPowMining powMining, 
            IFullNode fullNode, 
            ILoggerFactory loggerFactory, 
            IWalletManager walletManager,
            INetworkDifficulty networkDifficulty = null,
            IBlockProvider blockProvider = null,
            Network network = null,
            ITxMempool txMempool = null,
            ChainIndexer chainIndexer = null,
            IConsensusManager consensusManager = null) : base(fullNode: fullNode, network: network, chainIndexer: chainIndexer, consensusManager: consensusManager)
        {
            Guard.NotNull(powMining, nameof(powMining));
            Guard.NotNull(fullNode, nameof(fullNode));
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(walletManager, nameof(walletManager));

            this.fullNode = fullNode;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.walletManager = walletManager;
            this.powMining = powMining;
            this.networkDifficulty = networkDifficulty;
            this.blockProvider = blockProvider;
            this.txMempool = txMempool;
        }

        /// <summary>
        /// Tries to mine one or more blocks.
        /// </summary>
        /// <param name="blockCount">Number of blocks to mine.</param>
        /// <returns>List of block header hashes of newly mined blocks.</returns>
        /// <remarks>It is possible that less than the required number of blocks will be mined because the generating function only
        /// tries all possible header nonces values.</remarks>
        [ActionName("generate")]
        [ActionDescription("Tries to mine a given number of blocks and returns a list of block header hashes.")]
        public List<uint256> Generate(int blockCount)
        {
            if (blockCount <= 0)
            {
                throw new RPCServerException(RPCErrorCode.RPC_INVALID_REQUEST, "The number of blocks to mine must be higher than zero.");
            }

            WalletAccountReference accountReference = this.GetAccount();
            HdAddress address = this.walletManager.GetUnusedAddress(accountReference);

            List<uint256> res = this.powMining.GenerateBlocks(new ReserveScript(address.Pubkey), (ulong)blockCount, int.MaxValue);
            return res;
        }

        /// <summary>
        /// Tries to mine one or more blocks, with their resulting rewards being assigned to a given addess scriptPubKey.
        /// </summary>
        /// <param name="blockCount">Number of blocks to mine.</param>
        /// /// <param name="address">The address that block rewards should be assigned to, in base58 format.</param>
        /// <returns>List of block header hashes of newly mined blocks.</returns>
        /// <remarks>It is possible that less than the required number of blocks will be mined because the generating function only
        /// tries all possible header nonces values.</remarks>
        [ActionName("generatetoaddress")]
        [ActionDescription("Tries to mine a given number of blocks to a specified address, and returns a list of block header hashes.")]
        public List<uint256> GenerateToAddress(int blockCount, string address)
        {
            if (blockCount <= 0)
            {
                throw new RPCServerException(RPCErrorCode.RPC_INVALID_REQUEST, "The number of blocks to mine must be higher than zero.");
            }

            var parsedAddress = BitcoinAddress.Create(address, this.Network);

            List<uint256> res = this.powMining.GenerateBlocks(new ReserveScript(parsedAddress.ScriptPubKey), (ulong)blockCount, int.MaxValue);
            return res;
        }

        /// <summary>
        /// Finds first available wallet and its account.
        /// </summary>
        /// <returns>Reference to wallet account.</returns>
        private WalletAccountReference GetAccount()
        {
            string walletName = this.walletManager.GetWalletsNames().FirstOrDefault();
            if (walletName == null)
                throw new RPCServerException(RPCErrorCode.RPC_INVALID_REQUEST, "No wallet found");

            IHdAccount account = this.walletManager.GetAccounts(walletName).FirstOrDefault();
            if (account == null)
                throw new RPCServerException(RPCErrorCode.RPC_INVALID_REQUEST, "No account found on wallet");

            var res = new WalletAccountReference(walletName, account.Name);

            return res;
        }

        /// <summary>
        /// Gets the difficulty.
        /// </summary>
        /// <param name="hash">The hash.</param>
        /// <returns>(Target) Object with difficult of network.</returns>
        [ActionName("getdifficulty")]
        [ActionDescription("Result—the current difficulty.")]
        public double GetDifficulty()
        {
            return this.networkDifficulty.GetNetworkDifficulty().DifficultySafe();
        }

        /// <summary>
        /// Returns the estimated network hashes per second based on the last n blocks.
        /// Pass in [nblocks] to override # of blocks, -1 specifies since last difficulty change.
        /// Pass in [height] to estimate the network speed at the time when a certain block was found.
        /// </summary>
        /// <param name="nblocks">The nblocks.</param>
        /// <param name="height">The height.</param>
        /// <returns>(double) Return hashes per second estimated.</returns>
        [ActionName("getnetworkhashps")]
        [ActionDescription("Returns the estimated network hashes per second based on the last n blocks.")]
        public double GetNetworkHashPS(int nblocks = 120, int height = -1)
        {
            return GetNetworkHash(nblocks, height);
        }

        /// <summary>
        /// Gets the network hash ps.
        /// </summary>
        /// <param name="lookup">The lookup.</param>
        /// <param name="height">The height.</param>
        /// <returns>(double) Return hashes per second estimated.</returns>
        private double GetNetworkHash(int lookup, int height)
        {
            var pb = this.ChainIndexer.Tip;

            if (height >= 0 && height < this.ChainIndexer.Height)
                pb = this.ChainIndexer.GetHeader(height);

            if (pb == null) return 0;

            if (lookup > pb.Height)
                lookup = pb.Height;

            var pb0 = pb;
            var minTime = pb0.Header.Time;
            var maxTime = minTime;
            for (int i = 0; i < lookup; i++)
            {
                pb0 = pb0.Previous;
                var time = pb0.Header.Time;
                minTime = Math.Min(time, minTime);
                maxTime = Math.Max(time, maxTime);
            }

            // In case there's a situation where minTime == maxTime, we don't want a divide by zero exception.
            if (minTime == maxTime)
                return 0;

            var workDiff = pb.ChainWork - pb0.ChainWork;
            var timeDiff = maxTime - minTime;
            return workDiff.GetLow64() / timeDiff;
        }

        /// <summary>
        /// Gets the block template.
        /// </summary>
        /// <param name="template_request">(json object, optional) A json object in the following spec.</param>
        /// <returns>(GetBlockTemplateModel) It returns data needed to construct a block to work on. GetBlockTemplateModel RPC.</returns>
        [ActionName("getblocktemplate")]
        [ActionDescription("Gets the block template.")]
        public GetBlockTemplateModel GetBlockTemplate(string template_request)
        {
            var blockTemplate = new GetBlockTemplateModel();

            //generate template
            ChainedHeader chainTip = this.ChainIndexer.Tip;
            BlockTemplate pblockTemplate;

            lock (lockGetBlockTemplate)
            {
                pblockTemplate = this.blockProvider.BuildPowBlock(chainTip, new Script());
                var block = pblockTemplate.Block;
                var powCoinviewRule = this.ConsensusManager.ConsensusRules.GetRule<CheckPowUtxosetPowRule>();

                if (block != null)
                {
                    blockTemplate.Bits = string.Format("{0:x8}", block.Header.Bits.ToCompact());
                    blockTemplate.Curtime = (uint)DateTime.UtcNow.ToUnixTimestamp();
                    blockTemplate.PreviousBlockHash = block.Header.HashPrevBlock.ToString();
                    blockTemplate.Target = block.Header.Bits.ToString();

                    blockTemplate.Transactions = new List<TransactionContractModel>();

                    if (block.Transactions != null)
                    {
                        for (int i = 0; i < block.Transactions.Count; i++)
                        {
                            var item = block.Transactions[i];
                            if (!item.IsCoinBase)
                            {
                                var transaction = new TransactionContractModel();

                                transaction.Data = Encoders.Hex.EncodeData(item.ToBytes(this.Network.Consensus.ConsensusFactory.Protocol.ProtocolVersion));
                                transaction.Hash = item.GetWitHash().ToString();
                                transaction.Txid = item.GetHash().ToString();

                                long nSigOps = 0;
                                foreach (TxIn txin in item.Inputs)
                                    nSigOps += txin.ScriptSig.GetSigOpCount(false);

                                foreach (TxOut txout in item.Outputs)
                                    nSigOps += txout.ScriptPubKey.GetSigOpCount(false);

                                transaction.Fee = (long)pblockTemplate.TotalFee.ToUnit(MoneyUnit.Satoshi);
                                transaction.Sigops = nSigOps;
                                transaction.Weight = item.GetSerializedSize();

                                blockTemplate.Transactions.Add(transaction);
                            }
                        }
                    }

                    blockTemplate.Height = chainTip.Height + 1;
                    blockTemplate.Version = block.Header.Version;

                    blockTemplate.Coinbaseaux = new CoinbaseauxFlagsContractModel();
                    blockTemplate.Coinbaseaux.Flags = "062f503253482f";
                    blockTemplate.CoinbaseValue = powCoinviewRule.GetProofOfWorkReward(blockTemplate.Height).Satoshi;

                    var mutable = new List<string>();
                    mutable.Add("nonces");
                    mutable.Add("time");
                    mutable.Add("time/decrement");
                    mutable.Add("time/increment");
                    mutable.Add("transactions");
                    mutable.Add("coinbase");
                    mutable.Add("coinbase/create");
                    mutable.Add("coinbase/append");
                    mutable.Add("generation");
                    mutable.Add("version/reduce");
                    mutable.Add("prevblock");
                    blockTemplate.Mutable = mutable;
                    blockTemplate.NonceRange = "00000000ffffffff";

                    var rules = new List<string>();
                    rules.Add("csv");
                    blockTemplate.Rules = rules;

                    var capabilities = new List<string>();

                    blockTemplate.Capabilities = capabilities;

                    blockTemplate.Vbavailable = new List<string>();
                    blockTemplate.Vbrequired = 0;
                    blockTemplate.Weightlimit = this.Network.Consensus.Options.MaxBlockWeight;
                    blockTemplate.Sigoplimit = this.Network.Consensus.Options.MaxBlockSigopsCost;
                    blockTemplate.Sizelimit = this.Network.Consensus.Options.MaxBlockSerializedSize;

                    blockTemplate.Mintime = chainTip.GetMedianTimePast().AddHours(2).ToUnixTimeSeconds();
                }
            }

            return blockTemplate;
        }

        /// <summary>
        /// Attempts to submit new block to network.
        /// See https://en.bitcoin.it/wiki/BIP_0022 for full specification.
        /// </summary>
        /// <param name="hex">The hex-encoded block data to submit.</param>
        /// <param name="dummy">Dummy value, for compatibility with BIP22. This value is ignored.</param>
        /// <returns>(SubmitBlockModel) Object with result information.</returns>
        [ActionName("submitblock")]
        [ActionDescription("Attempts to submit new block to network.")]
        public string SubmitBlock(string hex, string dummy = null)
        {
            if (string.IsNullOrEmpty(hex))
            {
                throw new RPCServerException(RPCErrorCode.RPC_MISC_ERROR, "Empty block hex supplied");
            }

            lock (lockSubmitBlock)
            {
                var hexBytes = Encoders.Hex.DecodeData(hex);
                var pblock = Block.Load(hexBytes, this.Network.Consensus.ConsensusFactory);

                if (pblock == null)
                {
                    throw new RPCServerException(RPCErrorCode.RPC_DESERIALIZATION_ERROR, "Empty block hex supplied");
                }

                var chainTip = this.ChainIndexer.Tip;
                var newChain = new ChainedHeader(pblock.Header, pblock.GetHash(), chainTip);

                if (newChain.ChainWork <= chainTip.ChainWork)
                {
                    throw new RPCServerException(RPCErrorCode.RPC_MISC_ERROR, "Wrong chain work");
                }

                this.ConsensusManager.BlockMinedAsync(pblock).GetAwaiter().GetResult();
            }

            return null;
        }

        /// <summary>
        /// Returns a json object containing mining-related information.
        /// </summary>
        /// <returns>(GetMiningInfo) Object with informatin about mining.</returns>
        [ActionName("getmininginfo")]
        [ActionDescription("Returns a json object containing mining-related information.")]
        public GetMiningInfoModel GetMiningInfo()
        {
            var miningInfo = new GetMiningInfoModel();

            miningInfo.Chain = this.Network.Name.ToLower();
            miningInfo.Difficulty = this.GetDifficulty();
            miningInfo.PooledTx = this.txMempool.MapTx.Count();
            miningInfo.NetworkHashps = GetNetworkHashPS();
            miningInfo.Blocks = this.ChainState?.ConsensusTip?.Height ?? 0;

            ChainedHeader chainTip = this.ChainIndexer.Tip;
            BlockTemplate pblockTemplate = this.blockProvider.BuildPowBlock(chainTip, new Script());
            var block = pblockTemplate.Block;
            
            if (block != null)
            {
                miningInfo.CurrentBlockSize = block.GetSerializedSize();
                miningInfo.CurrentBlockWeight = miningInfo.CurrentBlockWeight;
                miningInfo.CurrentBlockTx = block.Transactions.Count();
            }

            return miningInfo;
        }

        /// <summary>
        /// Estimates the approximate fee per kilobyte needed for a transaction to begin confirmation within nblocks blocks.Uses virtual transaction size of transaction as defined in BIP 141 (witness data is discounted).
        /// </summary>
        /// <param name="nblocks">Confirmation target in blocks.</param>
        /// <returns>Estimated fee-per-kilobyte</returns>
        [ActionName("estimatefee")]
        [ActionDescription("Estimates the approximate fee per kilobyte needed for a transaction to begin confirmation within nblocks blocks.Uses virtual transaction size of transaction as defined in BIP 141 (witness data is discounted).")]
        public string EstimateFee(int nblocks)
        {
            var estimation = this.txMempool.EstimateFee(nblocks);
            return estimation.FeePerK.ToString();
        }

        /// <summary>
        /// Estimates the approximate fee per kilobyte needed for a transaction to begin confirmation within conf_target blocks if possible and return the number of blocks for which the estimate is valid.Uses virtual transaction size as defined in BIP 141 (witness data is discounted).
        /// </summary>
        /// <param name="nblocks">Confirmation target in blocks</param>
        /// <param name="estimate_mode">The fee estimate mode. Whether to return a more conservative estimate which also satisfies a longer history.A conservative estimate potentially returns a higher feerate and is more likely to be sufficient for the desired target, but is not as responsive to short term drops in the prevailing fee market.  Must be one of: "UNSET" (defaults to CONSERVATIVE), "ECONOMICAL", "CONSERVATIVE".</param>
        /// <returns>(EstimateSmartFeeModel) Return model with information about estimate.</returns>
        [ActionName("estimatesmartfee")]
        [ActionDescription("Estimates the approximate fee per kilobyte needed for a transaction to begin confirmation within conf_target blocks if possible and return the number of blocks for which the estimate is valid.Uses virtual transaction size as defined in BIP 141 (witness data is discounted).")]
        public EstimateSmartFeeModel EstimateSmartFee(int nblocks, string estimate_mode)
        {
            var result = new EstimateSmartFeeModel();
            int foundAtBlock = 0;

            var height = this.ChainIndexer.Tip.Height;

            var isConservative = true;
            if ((!string.IsNullOrEmpty(estimate_mode)) && (estimate_mode.ToUpper() == "ECONOMICAL")) isConservative = false;

            var estimation = this.txMempool.EstimateSmartFee(nblocks, out foundAtBlock, height, isConservative);

            result.Blocks = foundAtBlock;
            result.FeeRate = estimation.FeePerK.ToUnit(MoneyUnit.BTC);
            if (result.FeeRate.Equals(0))
            {
                result.FeeRate = new Money(10).ToUnit(MoneyUnit.BTC);
            }

            return result;
        }
    }
}