using NEL_Agency_API.lib;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NEL_Agency_API.Controllers
{
    public class BonusService
    {
        public string Notify_mongodbConnStr { set; get; }
        public string Notify_mongodbDatabase { set; get; }
        public mongoHelper mh { set; get; }
        public string BonusNofityCol { set; get; }
        public string BonusNofityFrom { set; get; }
        public string Block_mongodbConnStr { set; get; }
        public string Block_mongodbDatabase { set; get; }


        public JArray getBonusHistByAddress(string address)
        {
            return getBonusHistByAddress(address, 0, 0);
        }
        public JArray getBonusHistByAddress(string address, int pageNum, int pageSize) 
        {
            MyJson.JsonNode_Object getBonusFilter = new MyJson.JsonNode_Object();
            getBonusFilter.Add("from", new MyJson.JsonNode_ValueString(BonusNofityFrom));
            getBonusFilter.Add("to", new MyJson.JsonNode_ValueString(address));
            string findFliter = getBonusFilter.ToString();

            JArray result = null;
            if ( pageNum <= 0 || pageSize <= 0)
            {
                result = mh.GetData(Notify_mongodbConnStr, Notify_mongodbDatabase, BonusNofityCol, findFliter);
            } else
            {
                string sortStr = "{\"blockindex\":-1,\"txid\":-1}";
                result = mh.GetDataPages(Notify_mongodbConnStr, Notify_mongodbDatabase, BonusNofityCol, sortStr, pageSize, pageNum, findFliter);
            }
            List<JObject> res = null;
            if ( result != null)
            {
                res = result.Select(item =>
                {
                    JObject obj = new JObject();
                    obj.Add("value", Convert.ToString(((JObject)item)["value"]));
                    obj.Add("blocktime", getBlockTime(Convert.ToString(((JObject)item)["blockindex"])));
                    return obj;
                }).ToList();
            } else
            {
                res = new List<JObject>();
            }
            
            JObject rr = new JObject();
            rr.Add("list", new JArray() { res });
            rr.Add("count", res.Count);
            return new JArray() { rr };

            return new JArray();

        }

        private long getBlockTime(string blockHeightStrSt)
        {
            string blockHeightFilter = "{\"index\":" + long.Parse(blockHeightStrSt) + "}";
            JArray queryBlockRes = queryBlock("block", blockHeightFilter);
            long blockTime = long.Parse(Convert.ToString(queryBlockRes[0]["time"]));
            return blockTime;
        }
        private JArray queryBlock(string coll, string filter)
        {
            return mh.GetData(Block_mongodbConnStr, Block_mongodbDatabase, coll, filter);
        }


    }
}
