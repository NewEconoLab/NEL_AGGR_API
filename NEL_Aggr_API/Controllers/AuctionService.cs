using NEL_Agency_API.lib;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThinNeo;

namespace NEL_Agency_API.Controllers
{
    public class AuctionService
    {
        private long THREE_DAY_SECONDS = 3 * /*24 * 60 * */60 /*测试时5分钟一天*/* 5;
        private long TWO_DAY_SECONDS = 2 * /*24 * 60 * */60 /*测试时5分钟一天*/* 5;
        private long FIVE_DAY_SECONDS = 5 * /*24 * 60 * */60 /*测试时5分钟一天*/ * 5;
        public string Notify_mongodbConnStr { set; get; }
        public string Notify_mongodbDatabase { set; get; }
        public mongoHelper mh { set; get; }
        public string Block_mongodbConnStr { get; set; }
        public string Block_mongodbDatabase { get; set; }
        public string queryDomainCollection { get; set; }
        public string queryBidListCollection { get; set; }
        public AuctionRecharge auctionRecharge { get; set; }

        public JArray rechargeAndTransfer(string txhex1, string txhex2)
        {
            return auctionRecharge.rechargeAndTransfer(txhex1, txhex2);
        }
        public JArray getRechargeAndTransfer(string txid)
        {
            return auctionRecharge.getRechargeAndTransfer(txid);
        }
        
        public JArray getBidListByAddressLikeDomain(string address, string prefixDomain, int pageNum=0, int pageSize=0)
        {
            int count;
            JObject[] res = queryDomainList(address, pageNum, pageSize, out count, prefixDomain);
            if (res == null || res.Length == 0)
            {
                return new JArray() { };
            }
            JObject rr = new JObject();
            rr.Add("list", new JArray() { res });
            rr.Add("count", count);
            return new JArray() { rr };
        }

        public JArray getBidListByAddress(string address, int pageNum=0, int pageSize=0)
        {
            int count;
            JObject[] res = queryDomainList(address, pageNum, pageSize, out count);
            if (res == null || res.Length == 0)
            {
                return new JArray() { };
            }
            JObject rr = new JObject();
            rr.Add("list", new JArray() { res });
            rr.Add("count", count);


            return new JArray() { rr }; 
        }
        private JObject[] queryDomainList(string address, int pageNum, int pageSize, out int count, string prefixDomain = "")
        {
            // 参拍域名
            JObject filter = new JObject() { { "who", address }, { "displayName", "addprice" } };
            if (prefixDomain != "")
            {
                filter.Add("domain", new JObject() { { "$regex", prefixDomain }, { "$options", "i" } });
            }
            JObject fieldFilter = toReturn(new string[] { "domain", "parenthash", "blockindex" });
            JArray queryRes = mh.GetDataWithField(Notify_mongodbConnStr, Notify_mongodbDatabase, queryBidListCollection, fieldFilter.ToString(), filter.ToString());

            //去重
            JArray rr = new JArray() { queryRes.GroupBy(item => item["domain"], (k, g) => g.ToArray()[0]).OrderByDescending(pItem => pItem["blockindex"]).ToArray() };
            count = rr.Count();
            if (count > pageSize)
            {
                int skip = pageSize * (pageNum - 1);
                for (int i = 0; i < skip; ++i)
                {
                    rr.Remove(i);
                }
                for (int i = pageSize; i < rr.Count(); ++i)
                {
                    rr.Remove(i);
                }
            }


            // 域名终值
            JObject multiFilter = new JObject() { { "$or", new JArray() { rr.Select(item => { ((JObject)item).Remove("blockindex"); return item; }).ToArray()}
        } };
            JArray multiRes = mh.GetData(Notify_mongodbConnStr, Notify_mongodbDatabase, queryBidListCollection, multiFilter.ToString());

            // 所有parenthash
            string[] parenthashArr = multiRes.Select(item => item["parenthash"].ToString()).Distinct().ToArray();
            Dictionary<string,string> parenthashDict = getDomainByHash(parenthashArr);

            // 所有区块索引
            long[] blockindexArr = multiRes.Select(item => long.Parse(item["startBlockSelling"].ToString())).Distinct().ToArray();
            long[] cc = multiRes.Select(item => long.Parse(item["endBlock"].ToString())).Distinct().ToArray();
            blockindexArr = blockindexArr.Concat(multiRes.Select(item => long.Parse(item["endBlock"].ToString())).Distinct().ToArray()).ToArray();
            Dictionary<string, long> blockindexDict = getBlockTimeByIndex(blockindexArr.Distinct().ToArray());

            JObject[] res = multiRes.GroupBy(item => item["domain"], (k, g) =>
            {
                string domain = k.ToString();
                return g.GroupBy(pItem => pItem["parenthash"], (kk, gg) => 
                {
                    string parenthash = kk.ToString();
                    JToken[] maxPriceArr = gg.OrderByDescending(maxPriceItem => Convert.ToString(maxPriceItem["maxPrice"])).ToArray();
                    JToken maxPriceObj = gg.OrderByDescending(maxPriceItem => Convert.ToString(maxPriceItem["maxPrice"])).ToArray()[0];
                    JToken maxPriceSlf = gg.Where(addpriceItem => addpriceItem["displayName"].ToString() == "addprice").OrderByDescending(maxPriceItem => int.Parse(Convert.ToString(maxPriceItem["maxPrice"]))).ToArray().Where(slfItem => 
                        Convert.ToString(slfItem["maxBuyer"]) == address
                        || Convert.ToString(slfItem["maxBuyer"]) == null
                        || Convert.ToString(slfItem["maxBuyer"]) == ""
                        || Convert.ToString(slfItem["who"]) == address
                        ).ToArray()[0];

                    JObject obj = new JObject();

                    // 0. 我的竞价
                    obj.Add("mybidprice", String.Format("{0:N8}", Convert.ToString(maxPriceSlf["maxBuyer"]) == address? maxPriceSlf["maxPrice"]: maxPriceSlf["value"]));

                    // 1. 域名
                    string fullDomain = domain + parenthashDict.GetValueOrDefault(parenthash); // 父域名 + 子域名
                    obj.Add("domain", fullDomain);

                    // 2. 开标时间
                    long startAuctionTime = blockindexDict.GetValueOrDefault(Convert.ToString(maxPriceObj["startBlockSelling"]));
                    obj.Add("startAuctionTime", startAuctionTime);


                    // 3.状态(取值：竞拍中、随机中、结束三种，需根据开标时间计算，其中竞拍中为0~3天)
                    // NotSelling           
                    // SellingStepFix01     0~2天
                    // SellingStepFix02     第三天
                    // SellingStepRan       随机
                    // EndSelling           结束-------endBlock
                    string auctionState;
                    long auctionSpentTime = getAuctionSpentTime(startAuctionTime);
                    string blockHeightStrEd = Convert.ToString(maxPriceObj["endBlock"]);
                    bool hasOnlyBidOpen = Convert.ToString(maxPriceObj["maxBuyer"]) == "";

                    string maxPrice = Convert.ToString(maxPriceObj["maxPrice"]);
                    List<JToken> endBlockToken = gg.Where(pp => {
                        return Convert.ToString(pp["maxPrice"]) == maxPrice
                        && Convert.ToString(pp["displayName"]) == "domainstate"
                        && Convert.ToString(pp["endBlock"]) != "0";
                    }).ToList();
                    if (endBlockToken != null && endBlockToken.Count > 0)
                    {
                        blockHeightStrEd = Convert.ToString(endBlockToken[0]["endBlock"]);
                    }
                    auctionState = getAuctionState(auctionSpentTime, blockHeightStrEd, hasOnlyBidOpen);
                    obj.Add("auctionState", auctionState);

                    // 4.竞拍最高价
                    obj.Add("maxPrice", maxPrice);

                    // 5.竞拍最高价地址
                    obj.Add("maxBuyer", Convert.ToString(maxPriceObj["maxBuyer"]));
                    // 6.竞拍已耗时(竞拍中显示)
                    if (auctionSpentTime < THREE_DAY_SECONDS)
                    {
                        obj.Add("auctionSpentTime", auctionSpentTime);
                    }
                    else
                    {
                        obj.Add("auctionSpentTime", auctionSpentTime - THREE_DAY_SECONDS);
                    }

                    obj.Add("domainsub", domain);
                    obj.Add("parenthash", parenthash);
                    obj.Add("endBlock", blockHeightStrEd);
                    //
                    obj.Add("blockindex", Convert.ToString(maxPriceObj["blockindex"]));
                    obj.Add("id", Convert.ToString(maxPriceObj["id"]));

                    return obj;
                }).ToArray();
            }).SelectMany(pItem => pItem).Where(p => Convert.ToString(p["auctionState"]) != canTryAgainBidFlag).OrderByDescending(q => q["blockindex"]).ToArray(); ;

            // 中标域名添加owner
            JObject[] needAddOwnerArr = res.Where(item => item["auctionState"].ToString() == "0").Select(pItem => new JObject() { { "domain", pItem["domainsub"].ToString() }, { "parenthash", pItem["parenthash"].ToString() } }).ToArray();
            bool hasEndBlock = needAddOwnerArr.Count() != 0;
            Dictionary<string,string> ownerDict = getOwnerByDomainAndParentHash(needAddOwnerArr);
            return res.Select(item => {
                if(hasEndBlock && item["auctionState"].ToString() == "0")
                {
                    string domainsub = item["domainsub"].ToString();
                    string parenthash = item["parenthash"].ToString();
                    item.Remove("owner");
                    item.Add("owner", ownerDict.GetValueOrDefault(domainsub+parenthash));

                    // 竞拍结束，更新已过时间
                    long startAuctionTime = int.Parse(Convert.ToString(item["startAuctionTime"]));
                    string endBlock = item["endBlock"].ToString();
                    
                    long auctionSpentTime = !isEndAuction(endBlock) ? TWO_DAY_SECONDS :blockindexDict.GetValueOrDefault(endBlock) - startAuctionTime - THREE_DAY_SECONDS;
                    item.Remove("auctionSpentTime");
                    item.Add("auctionSpentTime", auctionSpentTime);
                } 
                item.Remove("domainsub");
                item.Remove("parenthash");
                return item;
            }).ToArray();
        }

        public JArray getBidDetailByDomain(string domain, int pageNum = 1, int pageSize = 10)
        {
            string[] domainArr = domain.Split(".");
            JObject filter = new JObject();
            filter.Add("domain", domainArr[0]);
            filter.Add("parenthash", getNameHash(domainArr[1]));
            filter.Add("displayName", "addprice");
            // 累加value需要查询所有记录
            JArray queryRes = mh.GetData(Notify_mongodbConnStr, Notify_mongodbDatabase, queryBidListCollection, filter.ToString()); 
            if (queryRes == null || queryRes.Count == 0)
            {
                return new JArray() { };
            }
            // 批量查询blockindex对应的时间
            long[] blockindexArr = queryRes.Select(item => long.Parse(item["blockindex"].ToString())).ToArray();
            Dictionary<string, long> blocktimeDict = getBlockTimeByIndex(blockindexArr);
            //
            JObject[] arr = queryRes.Select(item =>
            {
                string maxPrice = item["maxPrice"].ToString();
                string maxBuyer = item["maxBuyer"].ToString();
                string who = item["who"].ToString();
                if (maxBuyer != who)
                {
                    maxBuyer = who;
                    maxPrice = Convert.ToString(queryRes.Where(pItem => pItem["who"].ToString() == who && int.Parse(pItem["blockindex"].ToString()) <= int.Parse(item["blockindex"].ToString())).Sum(ppItem => int.Parse(ppItem["value"].ToString())));
                }
                long addPriceTime = blocktimeDict.GetValueOrDefault(Convert.ToString(item["blockindex"]));
                return new JObject() { { "maxPrice", maxPrice }, { "maxBuyer", maxBuyer }, { "addPriceTime", addPriceTime } };

            }).Where(p => Convert.ToString(p["maxPrice"]) != "0").OrderByDescending(p => p["addPriceTime"]).ThenByDescending(p => int.Parse(p["maxPrice"].ToString())).Skip(pageSize*(pageNum-1)).Take(pageSize).ToArray();
            long count = queryRes.Count;
            JObject res = new JObject();
            res.Add("list", new JArray() { arr });
            res.Add("count", count);
            return new JArray() { res };
        }
        private Dictionary<string, long> getBlockTimeByIndex(long[] blockindexArr)
        {
            JObject queryFilter = toFilter(blockindexArr, "index", "$or");
            JObject returnFilter = toReturn(new string[] { "index", "time" });
            JArray blocktimeRes = mh.GetDataWithField(Block_mongodbConnStr, Block_mongodbDatabase, "block", returnFilter.ToString(), queryFilter.ToString());
            return blocktimeRes.ToDictionary(key => key["index"].ToString(), val => long.Parse(val["time"].ToString()));
        }
            
        private JObject toFilter(long[] blockindexArr, string field, string logicalOperator="$or")
        {
            if(blockindexArr.Count() == 1)
            {
                return new JObject() { { field, blockindexArr[0] } };
            }
            return new JObject() { { logicalOperator, new JArray() { blockindexArr.Select(item => new JObject() { { field, item } }).ToArray() } } };
        }
        private JObject toReturn(string[] fieldArr)
        {
            JObject obj = new JObject();
            foreach(var field in fieldArr)
            {
                obj.Add(field, 1);
            }
            return obj;
        }
        private JObject toSort(string[] fieldArr, bool order=false)
        {
            int flag = order ? 1 : -1;
            JObject obj = new JObject();
            foreach (var field in fieldArr)
            {
                obj.Add(field, flag);
            }
            return obj;
        }

        public JArray getBidResByDomain(string domain)
        {
            string[] domainArr = domain.Split(".");
            JObject filter = new JObject();
            filter.Add("domain", domainArr[0]);
            filter.Add("parenthash", getNameHash(domainArr[1]).ToString());
            JObject fieldFilter = toReturn(new string[] { "maxPrice", "maxBuyer", "blockindex", "startBlockSelling", "endBlock"});
            JObject sortBy = toSort(new string[] { "blockindex", "getTime" });
            JArray queryRes = mh.GetDataPagesWithField(Notify_mongodbConnStr, Notify_mongodbDatabase, queryBidListCollection, fieldFilter.ToString(), 1, 1, sortBy.ToString(), filter.ToString());
            if (queryRes == null || queryRes.Count == 0)
            {
                return new JArray() { };
            }
            JObject res = (JObject)queryRes[0];
            string auctionState = getAuctionState(res["startBlockSelling"].ToString(), res["endBlock"].ToString());
            res.Add("auctionState", auctionState);
            res.Remove("startBlockSelling");
            res.Remove("endBlock");

            return new JArray() { res };
        }

        private long getBlockTimeByBlokcIndex(string blockHeightStr)
        {
            string blockHeightFilter = "{\"index\":" + long.Parse(blockHeightStr) + "}";
            JArray queryBlockRes = queryBlock("block", blockHeightFilter);
            long blockTime = long.Parse(Convert.ToString(queryBlockRes[0]["time"]));
            return blockTime;
        }

        private Dictionary<string,string> getDomainByHash(string[] parentHashArr)
        {
            JObject queryFilter = parentHashArr.Count() == 1 
                    ? new JObject() { { "namehash", parentHashArr[0] } }
                    : new JObject() { { "$or", new JArray() { parentHashArr.Select(item => new JObject() { { "namehash", item } }).ToArray() } } };
            JObject fieldFilter = toReturn(new string[] { "namehash", "domain"});
            JArray res = mh.GetDataWithField(Notify_mongodbConnStr, Notify_mongodbDatabase, queryDomainCollection, fieldFilter.ToString(), queryFilter.ToString());
            return res.GroupBy(item => item["namehash"], (k, g) =>
            {
                JObject obj = new JObject();
                obj.Add("namehash", k.ToString());
                obj.Add("domain", "." + g.ToArray()[0]["domain"].ToString());
                return obj;
            }).ToArray().ToDictionary(key => key["namehash"].ToString(), val => val["domain"].ToString());
        }
        private string getOwnerByDomainAndParentHash(string domain, string parenthash)
        {
            string owner = "";
            JObject filter = new JObject() { { "domain", domain }, { "parenthash", parenthash } };
            JArray res = mh.GetData(Notify_mongodbConnStr, Notify_mongodbDatabase, queryDomainCollection, filter.ToString());
            if (res != null && res.Count > 0)
            {
                owner = res[0]["owner"].ToString();
            }
            return owner;
        }
        private Dictionary<string,string> getOwnerByDomainAndParentHash(JObject[] domainAndParenthashArr)
        {
            if(domainAndParenthashArr == null || domainAndParenthashArr.Count() == 0)
            {
                return null;
            }
            JObject filter = domainAndParenthashArr.Count() == 1 ? domainAndParenthashArr[0]
                    : new JObject() { { "$or", new JArray() { domainAndParenthashArr } } };
            JObject fieldFilter = new JObject() { { "domain", 1 }, { "parenthash", 1 }, { "owner", 1 }, { "blockindex", 1 } };
            JObject sortBy = new JObject() { { "blockindex",-1} };
            JArray res = mh.GetDataWithField(Notify_mongodbConnStr, Notify_mongodbDatabase, queryDomainCollection, fieldFilter.ToString(), filter.ToString());
            if (res != null && res.Count > 0)
            {
                return res.GroupBy(item => item["domain"], (k,g)=> {
                    return g.GroupBy(pItem => pItem["parenthash"], (kk, gg) => gg.OrderByDescending(ppItem => ppItem["blockindex"]).ToArray()[0]).ToArray();
                }).SelectMany(qq => qq).Select(item => new JObject() {
                    { "domain", Convert.ToString(item["domain"]) },
                    { "parenthash", Convert.ToString(item["parenthash"]) },
                    { "owner", Convert.ToString(item["owner"]) } }).ToArray().ToDictionary(key => key["domain"].ToString() + key["parenthash"].ToString(), val => val["owner"].ToString());
            }
            return null;
        }

        private string getAuctionState(string blockHeightStrSt, string blockHeightStrEd, bool hasOnlyBidOpen = false)
        {
            return getAuctionState(getAuctionSpentTime(getStartAuctionTime(blockHeightStrSt)), blockHeightStrEd, hasOnlyBidOpen);
        }
        private string getAuctionState(long auctionSpentTime, string blockHeightStrEd, bool hasOnlyBidOpen) 
        {
            string auctionState = "";
            if (auctionSpentTime < THREE_DAY_SECONDS)
            {
                // 竞拍中
                auctionState = "1";// "Fixed period";
            } 
            else
            {
                if (!isEndAuction(blockHeightStrEd))
                {
                    // 随机
                    auctionState = "2";// "Random period";

                    if (hasOnlyBidOpen && auctionSpentTime > FIVE_DAY_SECONDS)
                    {
                        // 流拍
                        auctionState = canTryAgainBidFlag;
                    }
                    if(!hasOnlyBidOpen && auctionSpentTime > FIVE_DAY_SECONDS)
                    {
                        // 结束
                        auctionState = "0";// "Ended";

                    }
                }
                else
                {
                    // 结束
                    auctionState = "0";// "Ended";
                }
            }
            return auctionState;
        }
        private string canTryAgainBidFlag = "CanTryAgainBid";
        private string getNameHash(string domain)
        {
            return "0x" + Helper.Bytes2HexString(new NNSUrl(domain).namehash.Reverse().ToArray());
        }
        private bool isEndAuction(string blockHeightStrEd)
        {
            return blockHeightStrEd != null && !blockHeightStrEd.Equals("") && !blockHeightStrEd.Equals("0");
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
            DateTime startTime = TimeZone.CurrentTimeZone.ToLocalTime(new DateTime(1970, 1, 1)); // 当地时区
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
}
