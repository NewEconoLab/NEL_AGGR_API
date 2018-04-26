using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace NEL_Agency_API.RPC
{
    public class JsonRPCrequest
    {
        //public JsonRPCrequest(string json)
        //{
        //    try
        //    {
        //        JObject j = JObject.Parse(json);
        //        jsonrpc = (string)j["jsonrpc"];
        //        method = (string)j["method"];
        //        @params = JsonConvert.DeserializeObject<object[]>(JsonConvert.SerializeObject(j["params"]));
        //        id = (long)j["id"];
        //    }
        //    catch { }
        //}

        public string jsonrpc { get; set; }
        public string method { get; set; }
        public object[] @params { get; set; }
        public long id { get; set; }
    }
}
