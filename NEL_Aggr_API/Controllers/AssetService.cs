using NEL_Agency_API.lib;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NEL_Agency_API.Controllers
{
    public class AssetService
    {
        public string mongodbConnStr { set; get; }
        public string mongodbDatabase { set; get; }
        public mongoHelper mh { set; get; }


        public JArray fuzzySearchAsset(string name, int pageNum = 1, int pageSize = 6)
        {
            JArray res1 = search(false, "asset", name, pageNum, pageSize);
            JArray res2 = search(true, "NEP5asset", name, pageNum, pageSize);
            List<JToken> list7 = new List<JToken>();
            foreach (var item in res1)
            {
                list7.Add(item);
            }
            foreach (var item in res2)
            {
                list7.Add(item);
            }
            return new JArray()
            {
                list7.Take(pageSize).ToArray()
            };
        }
        
        private JObject newOrFilter(string key, string regex)
        {
            JObject obj = new JObject();
            JObject subobj = new JObject();
            subobj.Add("$regex", regex);
            subobj.Add("$options", "i");
            obj.Add(key, subobj);
            return obj;
        }
        private JArray search(bool isNep5, string coll, string name, int pageNum = 1, int pageSize = 6)
        {
            string key = isNep5 ? "name" : "name.name";
            JObject orFilter = new JObject();
            JArray orFilterSub = new JArray();
            orFilterSub.Add(newOrFilter(key, name));
            orFilterSub.Add(newOrFilter(key, transferName(name)));
            orFilter.Add("$or", orFilterSub);

            JArray res = mh.GetDataPages(mongodbConnStr, mongodbDatabase, coll, "{}", pageSize, pageNum, orFilter.ToString());
            if (res == null || res.Count == 0)
            {
                return new JArray() { };
            }

            if (isNep5)
            {
                return new JArray() {{
                res.Select(item => {
                    string id = Convert.ToString(item["assetid"]);
                    JObject obj = new JObject();
                    obj.Add("assetid", id);
                    obj.Add("name", transferRes(id, Convert.ToString(item["name"])));
                    return obj;
                }).GroupBy(pItem => pItem["assetid"], (k,g) => g.ToArray()[0]).Where(pp => !isNeoOrGas(pp["assetid"].ToString())).ToArray()
            } };
            }
            return new JArray() {{
                res.SelectMany(item => {
                    string id = Convert.ToString(item["id"]);
                    return item["name"].Select(subItem =>
                    {
                        JObject obj = new JObject();
                        obj.Add("assetid", id);
                        obj.Add("name", transferRes(id, Convert.ToString(subItem["name"])));
                        return obj;
                    }).ToArray();
                }).GroupBy(pItem => pItem["assetid"], (k,g) => g.ToArray()[0]).ToArray()
            } };
        }
        
        private string transferName(string name)
        {
            if(neoFilter.Contains(name.ToLower()))
            {
                return "AntShare";
            }
            if(gasFilter.Contains(name.ToLower()))
            { 
                return "AntCoin";
            }
            return name;
        }
        private string[] neoFilter = new string[]
        {
            "n","ne","neo"
        };

        private string[] gasFilter = new string[]
        {
            "g","ga","gas"
        };

        private string transferRes(string id, string name)
        {
            if(id == id_neo)
            {
                return "NEO";
            }
            if(id == id_gas)
            {
                return "GAS";
            }
            return name;
        }
        private bool isNeoOrGas(string assetid)
        {
            if(assetid == id_gas || assetid == id_neo)
            {
                return true;
            }
            return false;
        }
        private string id_neo = "0xc56f33fc6ecfcd0c225c4ab356fee59390af8560be0e930faebe74a6daff7c9b";
        private string id_gas = "0x602c79718b16e442de58778e148d0b1084e3b2dffd5de6b7b16cee7969282de7";

    }
}
