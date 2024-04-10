using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Blockcore.Features.Wallet.Api.Models
{
    public class GetIndexInfoDetailModel
    {
        [JsonProperty(Order = 1, PropertyName = "synced")]
        public bool Synced { get; set; }

        [JsonProperty(Order = 2, PropertyName = "best_block_height")]
        public int BestBlockHeight { get; set; }
    }
}
