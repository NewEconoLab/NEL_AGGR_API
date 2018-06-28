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
            JObject nameFilter = new JObject();
            JObject subNameFilter = new JObject();
            subNameFilter.Add("$regex", name);
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
                        obj.Add("name", Convert.ToString(subItem["name"]));
                        return obj;
                    }).ToArray();
                }).ToArray()
            } };
         
        }

    }
}
