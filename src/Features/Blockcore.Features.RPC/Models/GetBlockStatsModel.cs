using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Blockcore.Features.RPC.Models
{
    public class GetBlockStatsModel
    {
        [JsonProperty("blockhash")]
        public string BlockHash { get; set; }

        [JsonProperty("height")]
        public int Height { get; set; }

        [JsonProperty("time")]
        public long Time { get; set; } // Unix timestamp

        [JsonProperty("mediantime")]
        public long MedianTime { get; set; } // Unix timestamp

        [JsonProperty("txs")]
        public int Txs { get; set; }

        [JsonProperty("ins")]
        public int Ins { get; set; }

        [JsonProperty("outs")]
        public int Outs { get; set; }

        [JsonProperty("total_size")]
        public long TotalSize { get; set; } // in bytes

        [JsonProperty("total_weight")]
        public long TotalWeight { get; set; } // in weight units

        [JsonProperty("totalfee")]
        public decimal TotalFee { get; set; } // in BTC

        [JsonProperty("avgfee")]
        public decimal AvgFee { get; set; } // in BTC

        [JsonProperty("maxfee")]
        public decimal MaxFee { get; set; } // in BTC

        [JsonProperty("minfee")]
        public decimal MinFee { get; set; } // in BTC

        [JsonProperty("medianfee")]
        public decimal MedianFee { get; set; } // in BTC

        [JsonProperty("avgfeerate")]
        public decimal AvgFeeRate { get; set; } // in sat/vB

        [JsonProperty("maxfeerate")]
        public decimal MaxFeeRate { get; set; } // in sat/vB

        [JsonProperty("minfeerate")]
        public decimal MinFeeRate { get; set; } // in sat/vB

        [JsonProperty("medianfeerate")]
        public decimal MedianFeeRate { get; set; } // in sat/vB

        [JsonProperty("avgtxsize")]
        public decimal AvgTxSize { get; set; } // in bytes

        [JsonProperty("maxtxsize")]
        public int MaxTxSize { get; set; } // in bytes

        [JsonProperty("mintxsize")]
        public int MinTxSize { get; set; } // in bytes

        [JsonProperty("mediantxsize")]
        public int MedianTxSize { get; set; } // in bytes

        [JsonProperty("utxo_increase")]
        public int UtxoIncrease { get; set; }

        [JsonProperty("utxo_size_inc")]
        public long UtxoSizeInc { get; set; } // in bytes

        [JsonProperty("subsidy")]
        public decimal Subsidy { get; set; } // in BTC

        [JsonProperty("feerate_percentiles")]
        public List<decimal> FeeratePercentiles { get; set; }
    }
}