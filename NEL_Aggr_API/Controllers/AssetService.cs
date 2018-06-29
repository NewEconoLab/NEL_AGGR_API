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
            // 转义
            JArray res1 = search("asset", name, pageNum, pageSize);
            // 非转义
            JArray res2 = search("asset", transferName(name), pageNum, pageSize);

            // 转义
            JArray res3 = search("NEP5asset", name, pageNum, pageSize);
            // 非转义
            JArray res4 = search("NEP5asset", transferName(name), pageNum, pageSize);

            List<JToken> list = new List<JToken>();
            foreach (var item in res1)
            {
                list.Add(item);
            }
            foreach (var item in res2)
            {
                list.Add(item);
            }
            foreach (var item in res3)
            {
                list.Add(item);
            }
            foreach (var item in res4)
            {
                list.Add(item);
            }

            return new JArray()
            {
                list.GroupBy(item => item["assetid"], (k, g) =>
                {
                    JObject obj = new JObject();
                    obj.Add("assetid", k);
                    obj.Add("name", Convert.ToString(g.ToArray()[0]["name"]));
                    return obj;
                }).Take(pageSize).ToArray()
            };
        /*
            list.GroupBy(item => item["assetid"], (k, g) =>
            {
                JObject obj = new JObject();
                obj.Add("assetid", k);
                obj.Add("name", Convert.ToString(g.ToArray()[0]["name"]));
                return obj;
            }).ToArray();
            return new JArray() { list.Distinct().Take(pageSize).ToArray() };
            */

            /*

            JObject nameFilter = new JObject();
            JObject subNameFilter = new JObject();
            subNameFilter.Add("$regex", transferName(name));
            subNameFilter.Add("$options", "i");
            nameFilter.Add("name.name", subNameFilter);
            JArray res = mh.GetDataPages(mongodbConnStr, mongodbDatabase, "asset", "{}", pageSize, pageNum, nameFilter.ToString());
            if (res == null || res.Count == 0)
            {
                return new JArray() { };
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
                }).ToArray()
            } };




           */
         
        }
        
        private JArray search(string coll, string name, int pageNum =1, int pageSize = 6)
        {
            JObject nameFilter = new JObject();
            JObject subNameFilter = new JObject();
            subNameFilter.Add("$regex", name);
            subNameFilter.Add("$options", "i");
            nameFilter.Add("name.name", subNameFilter);
            JArray res = mh.GetDataPages(mongodbConnStr, mongodbDatabase, coll, "{}", pageSize, pageNum, nameFilter.ToString());
            if (res == null || res.Count == 0)
            {
                return new JArray() { };
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
                }).ToArray()
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
        private string id_neo = "0xc56f33fc6ecfcd0c225c4ab356fee59390af8560be0e930faebe74a6daff7c9b";
        private string id_gas = "0x602c79718b16e442de58778e148d0b1084e3b2dffd5de6b7b16cee7969282de7";

    }
}
