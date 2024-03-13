using Newtonsoft.Json;

namespace Blockcore.Features.MemoryPool.Api.Models
{
    public class GetMemPoolInfoModel
    {
        [JsonProperty(PropertyName = "size")]
        public long Size { get; set; }

        [JsonProperty(PropertyName = "bytes")]
        public long Bytes { get; set; }

        [JsonProperty(PropertyName = "usage")]
        public long Usage { get; set; }

        [JsonProperty(PropertyName = "maxmempool")]
        public long Maxmempool { get; set; }

        [JsonProperty(PropertyName = "mempoolminfee")]
        public decimal MempoolMinFee { get; set; }

        [JsonProperty(PropertyName = "minrelaytxfee")]
        public decimal? MinRelayTxFee { get; set; }
    }
}
