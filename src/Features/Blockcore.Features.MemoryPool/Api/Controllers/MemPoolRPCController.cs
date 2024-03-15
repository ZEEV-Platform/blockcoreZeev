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
using Blockcore.Utilities.JsonConverters;

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
        public IActionResult GetMempoolEntry(string txid)
        {
            try
            {
                Guard.NotEmpty(txid, "txid");
                var entry = this.MemPool.GetEntry(new uint256(txid));
                return this.Json(ResultHelper.BuildResultResponse(GetMemPoolEntryFromTx(entry)));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Returns details on the active state of the TX memory pool.
        /// </summary>
        /// <returns>(GetMemPoolInfo) Return object with informations.</returns>
        [ActionName("getmempoolinfo")]
        [ActionDescription("Returns details on the active state of the TX memory pool.")]
        public IActionResult GetMempoolInfo()
        {
            try
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

                return this.Json(ResultHelper.BuildResultResponse(result));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Dumps the mempool to disk.
        /// </summary>
        /// <returns>(bool) True if all ok.</returns>
        [ActionName("savemempool")]
        [ActionDescription("Dumps the mempool to disk.")]
        public IActionResult SaveMemPool()
        {
            try
            {
                this.MempoolManager.SavePool();
                return this.Json(ResultHelper.BuildResultResponse(true));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// If txid is in the mempool, returns all in-mempool ancestors.
        /// </summary>
        /// <param name="txid">The transaction id (must be in mempool).</param>
        /// <param name="verbose">True for a json object, false for array of transaction ids</param>
        /// <returns>(List, GetMemPoolEntry or List, string) Return object with informations.</returns>
        [ActionName("getmempoolancestors")]
        [ActionDescription("If txid is in the mempool, returns all in-mempool ancestors.")]
        public IActionResult GetMempoolAncestors(string txid, bool verbose)
        {
            try
            {
                Guard.NotEmpty(txid, nameof(txid));

                var entryTx = this.MemPool.GetEntry(new uint256(txid));
                Guard.NotNull(entryTx, "entryTx does not exist.");

                var setAncestors = new SetEntries();
                string dummy = string.Empty;
                long nNoLimit = long.MaxValue;
                this.MemPool.CalculateMemPoolAncestors(entryTx, setAncestors, nNoLimit, nNoLimit, nNoLimit, nNoLimit, out dummy, false);

                if (verbose)
                {
                    var result = new List<GetMemPoolEntryModel>();

                    if (setAncestors != null)
                    {
                        foreach (var entry in setAncestors)
                        {
                            result.Add(GetMemPoolEntryFromTx(entry));
                        }
                    }
                    return this.Json(ResultHelper.BuildResultResponse(result));
                }

                var listTxHash = new List<string>();
                if (setAncestors != null)
                {
                    foreach (var entry in setAncestors)
                    {
                        listTxHash.Add(entry.TransactionHash.ToString());
                    }
                }

                return this.Json(ResultHelper.BuildResultResponse(listTxHash));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// If txid is in the mempool, returns all in-mempool descendants.
        /// </summary>
        /// <param name="txid">The transaction id (must be in mempool).</param>
        /// <param name="verbose">True for a json object, false for array of transaction ids.</param>
        /// <returns>(List, GetMemPoolEntry or List, string) Return object with informations.</returns>
        [ActionName("getmempooldescendants")]
        [ActionDescription("If txid is in the mempool, returns all in-mempool descendants.")]
        public IActionResult GetMempoolDescendants(string txid, bool verbose)
        {
            try
            {
                Guard.NotEmpty(txid, nameof(txid));
                var entryTx = this.MemPool.GetEntry(new uint256(txid));
                var setDescendants = new SetEntries();
                this.MemPool.CalculateDescendants(entryTx, setDescendants);

                if (verbose)
                {
                    var result = new List<GetMemPoolEntryModel>();

                    if (setDescendants != null)
                    {
                        foreach (var entry in setDescendants)
                        {
                            result.Add(GetMemPoolEntryFromTx(entry));
                        }
                    }

                    return this.Json(ResultHelper.BuildResultResponse(result));
                }

                var listTxHash = new List<string>();

                if (setDescendants != null)
                {
                    foreach (var entry in setDescendants)
                    {
                        listTxHash.Add(entry.TransactionHash.ToString());
                    }
                }

                return this.Json(ResultHelper.BuildResultResponse(listTxHash));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }
    }
}
