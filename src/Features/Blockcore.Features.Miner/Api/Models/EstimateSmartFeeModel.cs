using Newtonsoft.Json;

namespace Blockcore.Features.Miner.Api.Models
{
    public class EstimateSmartFeeModel
    {
        [JsonProperty(PropertyName = "feerate")]
        public decimal FeeRate { get; set; }

        [JsonProperty(PropertyName = "blocks")]
        public int Blocks { get; set; }
    }
}
