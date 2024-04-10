using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace Blockcore.Features.MemoryPool.Api.Models
{
    public class GetMemPoolEntryModel
    {
        [JsonProperty(PropertyName = "vsize")]
        public long Size { get; set; }

        [JsonProperty(PropertyName = "weight")]
        public long Weight { get; set; }

        [JsonProperty(PropertyName = "time")]
        public long Time { get; set; }

        [JsonProperty(PropertyName = "height")]
        public int Height { get; set; }

        [JsonProperty(PropertyName = "descendantcount")]
        public long DescendantCount { get; set; }

        [JsonProperty(PropertyName = "descendantsize")]
        public long DescendantSize { get; set; }

        [JsonProperty(PropertyName = "ancestorcount")]
        public long AncestorCount { get; set; }

        [JsonProperty(PropertyName = "ancestorsize")]
        public long AncestorSize { get; set; }

        [JsonProperty(PropertyName = "wtxid")]
        public string WTXID { get; set; }

        [JsonProperty(PropertyName = "fees")]
        public GetMemPoolEntryFeeModel Fees { get; set; }

        [JsonProperty(PropertyName = "depends")]
        public List<string> Depends { get; set; }
    }

    public class GetMemPoolEntryFeeModel
    {
        [JsonProperty(PropertyName = "base")]
        public decimal Base { get; set; }

        [JsonProperty(PropertyName = "modified")]
        public decimal Modified { get; set; }

        [JsonProperty(PropertyName = "ancestor")]
        public decimal Ancestor { get; set; }

        [JsonProperty(PropertyName = "descendant")]
        public decimal Descendant { get; set; }
    }
}
