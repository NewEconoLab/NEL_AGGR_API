﻿using NEL_Agency_API.lib;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ThinNeo;

namespace NEL_Agency_API.Controllers
{
    public class AuctionService
    {
        private long THREE_DAY_SECONDS = 3 * 24 * 60 * 60;
        public string Notify_mongodbConnStr { set; get; }
        public string Notify_mongodbDatabase { set; get; }
        public mongoHelper mh { set; get; }
        public string Block_mongodbConnStr { get; set; }
        public string Block_mongodbDatabase { get; set; }

        public JArray getBidListByAddress(string address)
        {
            MyJson.JsonNode_Object req = new MyJson.JsonNode_Object();
            req.Add("who", new MyJson.JsonNode_ValueString(address));
            req.Add("displayName", new MyJson.JsonNode_ValueString("addprice"));
            JArray arr = queryNofity("0x505d66281afad9b78b73b84584e3d345463866f4", req.ToString());   // 第三个Coll
            
            //
            JObject[] res = arr.Select(s => new
            {
                domain = Convert.ToString(((JObject)s)["domain"]),
                parenthash = Convert.ToString(((JObject)s)["parenthash"])
            }).Distinct().Select(item =>
            {
                // 获取这些域名此时状态数据
                MyJson.JsonNode_Object queyBidDetailFilter = new MyJson.JsonNode_Object();
                queyBidDetailFilter.Add("domain", new MyJson.JsonNode_ValueString(item.domain));
                queyBidDetailFilter.Add("parenthash", new MyJson.JsonNode_ValueString(item.parenthash));
                queyBidDetailFilter.Add("displayName", new MyJson.JsonNode_ValueString("addprice"));
                JArray queyBidDetailRes = queryNofity("0x505d66281afad9b78b73b84584e3d345463866f4", queyBidDetailFilter.ToString());
                
                // 按出价人分组并计算出价总额，然后选出最高出价人
                var maxPriceItem = queyBidDetailRes.GroupBy(p => p["maxBuyer"], (k, g) => new
                {
                    maxBuyer = k,
                    maxPrice = g.Sum(q => double.Parse(Convert.ToString(q["maxPrice"]))),
                    token = g.OrderByDescending(c => c["blockindex"]).ToList()[0]
                }).OrderByDescending(k => k.maxPrice).ToList()[0];
                JToken token = maxPriceItem.token;

                JObject obj = new JObject();
                // 1. 域名
                string fullDomain = getParentDomainByParentHash(item.parenthash) + item.domain; // 父域名 + 子域名
                obj.Add("domain", fullDomain);

                // 2. 开标时间
                long startAuctionTime;
                String blockHeightStrSt = Convert.ToString(token["startBlockSelling"]);
                startAuctionTime = getStartAuctionTime(blockHeightStrSt);
                obj.Add("startAuctionTime", startAuctionTime);


                // 3.状态(取值：竞拍中、随机中、结束三种，需根据开标时间计算，其中竞拍中为0~3天)
                // NotSelling           
                // SellingStepFix01     0~2天
                // SellingStepFix02     第三天
                // SellingStepRan       随机
                // EndSelling           结束-------endBlock
                string auctionState;
                long auctionSpentTime = getAuctionSpentTime(startAuctionTime);
                string blockHeightStrEd = Convert.ToString(token["endBlock"]);
                auctionState = getAuctionState(auctionSpentTime, blockHeightStrEd);
                obj.Add("auctionState", auctionState);

                // 4.竞拍最高价
                obj.Add("maxPrice", String.Format("{0:N8}", maxPriceItem.maxPrice));
                
                // 5.竞拍最高价地址
                string maxBuyer = Convert.ToString(token["maxBuyer"]);
                obj.Add("maxBuyer", maxBuyer);

                // 6.竞拍已耗时(竞拍中显示)
                if (auctionSpentTime < THREE_DAY_SECONDS)
                {
                    obj.Add("auctionSpentTime", auctionSpentTime);
                }
                else
                {
                    obj.Add("auctionSpentTime", "");
                }

                // 7.域名所有者(竞拍结束显示)根据子域名和父域名哈希查询(从第一个Coll中查询)
                string owner = "";//
                if (isEndAuction(blockHeightStrEd))
                {
                    owner = getOwnerByDomainAndParentHash(item.domain, item.parenthash);
                }
                obj.Add("owner", owner);

                string blockindexStr = Convert.ToString(token["blockindex"]);
                obj.Add("blockindex", blockindexStr);
                return obj;
            }).OrderByDescending(q => q["blockindex"]).ToArray();
            
            return new JArray() { res }; 
        }


        public JArray getBidDetailByDomain(string domain)
        {
            return getBidDetailByDomain(domain, 0, 0);
        }
        public JArray getBidDetailByDomain(string domain, int pageNum, int pageSize)
        {
            // 计算slfDomain + parentHash
            string[] domainArr = domain.Split(".");
            MyJson.JsonNode_Object queyBidDetailFilter = new MyJson.JsonNode_Object();
            queyBidDetailFilter.Add("domain", new MyJson.JsonNode_ValueString(domainArr[0]));
            queyBidDetailFilter.Add("parenthash", new MyJson.JsonNode_ValueString(getNameHash(domainArr[1])));
            JArray queyBidDetailRes = queryNofity("0x505d66281afad9b78b73b84584e3d345463866f4", queyBidDetailFilter.ToString());
            
            // 获取与该域名相关数据
            JObject[] arr = queyBidDetailRes.Select(item => {
                JObject obj = new JObject();
                obj.Add("maxPrice", Convert.ToString(((JObject)item)["maxPrice"]));
                obj.Add("maxBuyer", Convert.ToString(((JObject)item)["maxBuyer"]));

                // 根据blockindex获取block.time即为加价时间
                String blockHeightStrSt = Convert.ToString(((JObject)item)["blockindex"]);
                long addPriceTime = getBlockTimeByBlokcIndex(blockHeightStrSt);
                obj.Add("addPriceTime", addPriceTime);
                return obj;
            }).ToArray();

            // 阶梯计算出价总和
            JObject[][] arrSecond = arr.GroupBy(p => p["maxBuyer"], (k, g) =>
            {
                JObject[] groupArr = g.OrderBy(q => q["addPriceTime"]).ToArray();
                double st = 0;
                for(int i=0; i<groupArr.Length; ++i)
                {
                    st += double.Parse(Convert.ToString(groupArr[i]["maxPrice"])) ;
                    groupArr[i].Remove("maxPrice");
                    groupArr[i].Add("maxPrice", String.Format("{0:N8}", st));
                }
                return groupArr;
            }).ToArray();
            List<JObject> listNew = new List<JObject>();
            foreach (var item in arrSecond)
            {
                listNew.AddRange(item);
            }
            // 倒叙排列
            JObject[] res = listNew.OrderByDescending(p => p["addPriceTime"]).ToArray();

            // 分页处理
            if (pageNum > 0 && pageSize > 0)
            {
                int st = (pageNum - 1) * pageSize;
                int ed = pageSize;
                return new JArray() { res.Skip(st).Take(pageSize).ToArray(), new JObject() { { "sumCount", listNew.Count()} } };
            }
            return new JArray() { res, new JObject() { { "sumCount", listNew.Count() } } }; 
        }

        public JArray getBidResByDomain(string domain)
        {
            // 计算slfDomain + parentHash
            string[] domainArr = domain.Split(".");
            MyJson.JsonNode_Object queyBidDetailFilter = new MyJson.JsonNode_Object();
            queyBidDetailFilter.Add("domain", new MyJson.JsonNode_ValueString(domainArr[0]));
            queyBidDetailFilter.Add("parenthash", new MyJson.JsonNode_ValueString(getNameHash(domainArr[1])));
            JArray queyBidDetailRes = queryNofity("0x505d66281afad9b78b73b84584e3d345463866f4", queyBidDetailFilter.ToString());

            JObject res = queyBidDetailRes.Select(item =>
            {
                JObject obj = new JObject();
                obj.Add("maxPrice", Convert.ToString(((JObject)item)["maxPrice"]));
                obj.Add("maxBuyer", Convert.ToString(((JObject)item)["maxBuyer"]));

                // 根据blockindex获取block.time即为加价时间
                String blockHeightStrSt = Convert.ToString(((JObject)item)["blockindex"]);
                string blockHeightFilter = "{\"index\":" + long.Parse(blockHeightStrSt) + "}";
                JArray queryBlockRes = queryBlock("block", blockHeightFilter);
                long addPriceTime = long.Parse(Convert.ToString(queryBlockRes[0]["time"]));
                obj.Add("addPriceTime", addPriceTime);
                // 用户获取状态值判断条件
                obj.Add("startBlockSelling", Convert.ToString(((JObject)item)["startBlockSelling"]));
                obj.Add("endBlock", Convert.ToString(((JObject)item)["endBlock"]));
                return obj;
            }).OrderByDescending(p => p["addPriceTime"]).ToArray()[0];

            string auctionState = getAuctionState(res["startBlockSelling"].ToString(), res["endBlock"].ToString());
            res.Add("auctionState", auctionState);

            // 更新出价总额
            double maxPrice = queyBidDetailRes.Where(p => p["maxBuyer"].Equals(res["maxBuyer"])).Sum(p => double.Parse(Convert.ToString(p["maxPrice"])));
            res.Remove("maxPrice");
            res.Add("maxPrice", String.Format("{0:N8}", maxPrice));
            res.Remove("startBlockSelling");
            res.Remove("endBlock");

            return new JArray() { res };
        }



        private string getParentDomainByParentHash(string parentHash)
        {
            string parentDomain = "";
            if (parentHash != "")
            {
                string queryNameHash = "{\"namehash\":\"" + parentHash + "\"}";
                JArray queryDomainResSub = queryNofity("0x1ff70bb2147cf56c8b1ce0eb09323eb2b3f57916", queryNameHash);
                foreach (var dd in queryDomainResSub)
                {
                    // 父域名只有一个
                    parentDomain += Convert.ToString(((JObject)dd)["domain"]) + ".";
                    break;
                }
            }
            return parentDomain;
        }


        private long getBlockTimeByBlokcIndex(string blockHeightStr)
        {
            string blockHeightFilter = "{\"index\":" + long.Parse(blockHeightStr) + "}";
            JArray queryBlockRes = queryBlock("block", blockHeightFilter);
            long blockTime = long.Parse(Convert.ToString(queryBlockRes[0]["time"]));
            return blockTime;
        }
        private string getOwnerByDomainAndParentHash(string domain, string parenthash)
        {
            string owner = "";
            MyJson.JsonNode_Object queyOwnerFilter = new MyJson.JsonNode_Object();
            queyOwnerFilter.Add("domain", new MyJson.JsonNode_ValueString(domain));
            queyOwnerFilter.Add("parenthash", new MyJson.JsonNode_ValueString(parenthash));
            JArray queryOwnerRes = queryNofity("0x1ff70bb2147cf56c8b1ce0eb09323eb2b3f57916", queyOwnerFilter.ToString());
            owner = queryOwnerRes[0]["owner"].ToString();
            return owner;
        }

        private string getAuctionState(string blockHeightStrSt, string blockHeightStrEd)
        {
            return getAuctionState(getAuctionSpentTime(getStartAuctionTime(blockHeightStrSt)), blockHeightStrEd);
        }
        private string getAuctionState(long auctionSpentTime, string blockHeightStrEd) 
        {
            string auctionState = "";
            if (auctionSpentTime < THREE_DAY_SECONDS)
            {
                // 竞拍中
                auctionState = "Fixed period";
            }
            else
            {
                if (!isEndAuction(blockHeightStrEd))
                {
                    // 随机
                    auctionState = "Random period";
                }
                else
                {
                    // 结束
                    auctionState = "Ended";
                }
            }
            return auctionState;
        }
        private string getNameHash(string domain)
        {
            return "0x" + Helper.Bytes2HexString(new NNSUrl(domain).namehash.Reverse().ToArray());
        }
        private bool isEndAuction(string blockHeightStrEd)
        {
            return blockHeightStrEd != null && blockHeightStrEd.Equals("") && blockHeightStrEd.Equals("0");
        }
        private long getStartAuctionTime(string blockHeightStrSt)
        {
            string blockHeightFilter = "{\"index\":" + long.Parse(blockHeightStrSt) + "}";
            JArray queryBlockRes = queryBlock("block", blockHeightFilter);
            long startAuctionTime = long.Parse(Convert.ToString(queryBlockRes[0]["time"]));
            return startAuctionTime;
        }
        
        private long getAuctionSpentTime(long startAuctionTime)
        {
            System.DateTime startTime = TimeZone.CurrentTimeZone.ToLocalTime(new System.DateTime(1970, 1, 1)); // 当地时区
            long timeStamp = (long)(DateTime.Now - startTime).TotalSeconds; // 相差秒数
            return timeStamp - startAuctionTime;
        }

        private JArray queryNofity(string coll, string filter)
        {
            return mh.GetData(Notify_mongodbConnStr, Notify_mongodbDatabase, coll, filter);
        }

        private JArray queryBlock(string coll, string filter)
        {
            return mh.GetData(Block_mongodbConnStr, Block_mongodbDatabase, coll, filter);
        }

    }
    class JObjectComparer : IComparer<JToken>
    {
        public string key { set; get; }

        public int Compare(JToken x, JToken y)
        {
            return string.Compare(Convert.ToString(y[key]), Convert.ToString(x[key]));
        }
    }
}