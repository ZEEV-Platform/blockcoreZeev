using System.Collections.Generic;
using Newtonsoft.Json;

namespace Blockcore.Utilities.JsonConverters
{
    public class ErrorModel
    {
        [JsonProperty(PropertyName = "code")]
        public string Code { get; set; }

        [JsonProperty(PropertyName = "message")]
        public string Message { get; set; }
    }

    public class ResultModel
    {
        [JsonProperty(PropertyName = "result")]
        public object Result { get; set; }

        [JsonProperty(PropertyName = "error")]
        public object Error { get; set; }

        [JsonProperty(PropertyName = "id")]
        public int Id { get; set; }
    }

    public static class ResultHelper
    {
        public static ResultModel BuildResultResponse(object obj)
        {
            return BuildResultResponse(obj, string.Empty, 0);
        }

        public static ResultModel BuildResultResponse(object obj, string error, int id)
        {
            ResultModel resultModel = new ResultModel
            {
                Result = obj,
                Error = new List<ErrorModel>(),
                Id = id
            };

            if (string.IsNullOrEmpty(error))
            {
                resultModel.Error = null;
            }

            return resultModel;
        }

        public static ResultModel BuildResultResponse(object obj, ErrorModel error, int id)
        {
            ResultModel resultModel = new ResultModel
            {
                Result = obj,
                Error = error,
                Id = id
            };

            return resultModel;
        }
    }
}
