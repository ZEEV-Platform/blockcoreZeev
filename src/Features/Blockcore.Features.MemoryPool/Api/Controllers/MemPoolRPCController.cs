using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Blockcore.Consensus.Chain;
using Blockcore.Consensus;
using Blockcore.Controllers;
using Blockcore.Features.MemoryPool.Interfaces;
using Blockcore.Interfaces;
using Blockcore.Mining;
using Blockcore.Networks;
using Blockcore.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using Blockcore.Features.MemoryPool.Api.Models;
using Blockcore.NBitcoin;
using Blockcore.Base;
using Blockcore.Configuration;
using Blockcore.Consensus.BlockInfo;
using Blockcore.Controllers.Models;
using Blockcore.Features.Consensus;
using Blockcore.Utilities.JsonErrors;
using static Blockcore.Features.MemoryPool.TxMempool;
using System.Net;
using System.Xml.Linq;
using Blockcore.Connection;

namespace Blockcore.Features.MemoryPool.Api.Controllers
{
    public class MemPoolRPCController : FeatureController
    {
        /// <summary>Instance logger</summary>
        private readonly ILogger logger;

        /// <summary>An interface implementation used to retrieve unspent transactions from a pooled source.</summary>
        private readonly IPooledGetUnspentTransaction pooledGetUnspentTransaction;

        /// <summary>An interface implementation used to retrieve unspent transactions.</summary>
        private readonly IGetUnspentTransaction getUnspentTransaction;

        /// <summary>
        /// The mempool manager.
        /// </summary>
        public MempoolManager MempoolManager { get; private set; }

        /// <summary>
        ///  Actual mempool.
        /// </summary>
        public ITxMempool MemPool { get; private set; }

        public MemPoolRPCController(
            ILoggerFactory loggerFactory,
            MempoolManager mempoolManager,
            ITxMempool mempool,
            ChainIndexer chainIndexer,
            IConnectionManager connectionManager,
            IPooledGetUnspentTransaction pooledGetUnspentTransaction = null,
        IGetUnspentTransaction getUnspentTransaction = null,
            IFullNode fullNode = null,
            NodeSettings nodeSettings = null,
            Network network = null,
            IChainState chainState = null)
            : base(
                  fullNode: fullNode,
                  nodeSettings: nodeSettings,
                  network: network,
                  chainIndexer: chainIndexer,
                  chainState: chainState,
                  connectionManager: connectionManager)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.pooledGetUnspentTransaction = pooledGetUnspentTransaction;
            this.getUnspentTransaction = getUnspentTransaction;
            this.MempoolManager = mempoolManager;
            this.MemPool = mempool;
        }

        private GetMemPoolEntryModel GetMemPoolEntryFromTx(TxMempoolEntry entry)
        {
            var fees = new GetMemPoolEntryFeeModel
            {
                Ancestor = entry.ModFeesWithAncestors.ToUnit(MoneyUnit.BTC),
                Descendant = entry.ModFeesWithDescendants.ToUnit(MoneyUnit.BTC),
                Base = entry.Fee.ToUnit(MoneyUnit.BTC),
                Modified = entry.ModifiedFee
            };

            var resultEntry = new GetMemPoolEntryModel
            {
                Size = entry.GetTxSize(),
                Time = entry.Time,
                Height = entry.EntryHeight,
                WTXID = entry.TransactionHash.ToString(),
                DescendantCount = entry.CountWithDescendants,
                DescendantSize = entry.SizeWithDescendants,
                AncestorCount = entry.CountWithAncestors,
                AncestorSize = entry.SizeWithAncestors,
                Fees = fees
            };

            var parents = this.MemPool.GetMemPoolParents(entry);

            if (parents != null)
            {
                resultEntry.Depends = new List<string>();
                foreach (var item in parents)
                {
                    resultEntry.Depends.Add(item.TransactionHash.ToString());
                }
            }

            return resultEntry;
        }

        /// <summary>
        /// Returns mempool data for given transaction.
        /// </summary>
        /// <param name="txid">The transaction id (must be in mempool).</param>
        /// <returns>(GetMemPoolEntry) Return object with informations.</returns>
        [ActionName("getmempoolentry")]
        [ActionDescription("Returns mempool data for given transaction.")]
        public GetMemPoolEntryModel GetMempoolEntry(string txid)
        {
            Guard.NotEmpty(txid, "txid");
            var entry = this.MemPool.GetEntry(new uint256(txid));
            return GetMemPoolEntryFromTx(entry);
        }

        /// <summary>
        /// Returns details on the active state of the TX memory pool.
        /// </summary>
        /// <returns>(GetMemPoolInfo) Return object with informations.</returns>
        [ActionName("getmempoolinfo")]
        [ActionDescription("Returns details on the active state of the TX memory pool.")]
        public GetMemPoolInfoModel GetMempoolInfo()
        {
            var maxmem = this.MempoolManager.mempoolSettings.MaxMempool * 1000000;
            var result = new GetMemPoolInfoModel
            {
                Size = this.MemPool.Size,
                Usage = this.MemPool.DynamicMemoryUsage(),
                Bytes = this.MempoolManager.MempoolSize().Result,
                Maxmempool = maxmem,
                MempoolMinFee = this.MemPool.GetMinFee(maxmem).FeePerK.ToUnit(MoneyUnit.BTC),
                MinRelayTxFee = this.Settings?.MinRelayTxFeeRate?.FeePerK?.ToUnit(MoneyUnit.BTC)
            };

            return result;
        }

        /// <summary>
        /// Dumps the mempool to disk.
        /// </summary>
        /// <returns>(bool) True if all ok.</returns>
        [ActionName("savemempool")]
        [ActionDescription("Dumps the mempool to disk.")]
        public void SaveMemPool()
        {
            this.MempoolManager.SavePool();
        }
    }
}
