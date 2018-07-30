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
        private long ONE_DAY_SECONDS = 1 * /*24 * 60 * */60 /*测试时5分钟一天*/* 5;
        private long FIVE_DAY_SECONDS = 5 * /*24 * 60 * */60 /*测试时5分钟一天*/ * 5;
        private long DAYS_OF_YEAR = 365;
        public string Notify_mongodbConnStr { set; get; }
        public string Notify_mongodbDatabase { set; get; }
        public mongoHelper mh { set; get; }
        public string Block_mongodbConnStr { get; set; }
        public string Block_mongodbDatabase { get; set; }
        public string queryDomainCollection { get; set; }
        public string queryBidListCollection { get; set; }
        public AuctionRecharge auctionRecharge { get; set; }


        public JArray hasTx(string txid)
        {
            bool issucces = queryHasTxFromBlock("tx", new JObject() { { "txid", txid } }.ToString());

            return new JArray() { { new JObject() { { "issucces", issucces } } } };
        }
        public JArray hasContract(string txid)
        {
            JArray queryRes = queryNotifyFromBlock("notify", new JObject() { { "txid", txid } }.ToString());
            if (queryRes == null || queryRes.Count == 0)
            {
                return new JArray() { new JObject() { { "displayNameList", new JArray() { } } } };
            }
            string[] res = queryRes.Where(p => ((JArray)p["notifications"]).Count() != 0).SelectMany(p =>
            {
                JArray pArr = (JArray)p["notifications"];
                return pArr.Select(pp => pp["state"]["value"][0]["value"].ToString()).Select(pp => pp.Hexstring2String()).ToArray();
            }).ToArray();
            return new JArray() { new JObject() { { "displayNameList", new JArray() { res } } } };
        }
        public JArray rechargeAndTransfer(string txhex1, string txhex2)
        {
            return auctionRecharge.rechargeAndTransfer(txhex1, txhex2);
        }
        public JArray getRechargeAndTransfer(string txid)
        {
            return auctionRecharge.getRechargeAndTransfer(txid);
        }

        public JArray getBidListByAddressLikeDomain(string address, string prefixDomain, int pageNum = 0, int pageSize = 0)
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

        public JArray getBidListByAddress(string address, int pageNum = 0, int pageSize = 0)
        {
            int count;
            JObject[] res = queryDomainList(address, pageNum, pageSize, out count, "", true);
            if (res == null || res.Length == 0)
            {
                return new JArray() { };
            }
            count = res.Count();
            JObject rr = new JObject();
            rr.Add("list", new JArray() { res });
            rr.Add("count", count);


            return new JArray() { rr };
        }
        private JObject[] queryDomainList(string address, int pageNum, int pageSize, out int count, string prefixDomain = "", bool isNeo2=false)
        {
            // 参拍域名
            JObject filter = new JObject() { { "who", address }, { "displayName", "addprice" } };
            if (prefixDomain != "")
            {
                filter.Add("domain", new JObject() { { "$regex", prefixDomain }, { "$options", "i" } });
            }
            JObject fieldFilter = toReturn(new string[] { "domain", "parenthash", "blockindex" });
            JArray queryRes = mh.GetDataWithField(Notify_mongodbConnStr, Notify_mongodbDatabase, isNeo2 ? "0x2788ee1116e47f4a35432d2b6650e48a3d632b97" : queryBidListCollection, fieldFilter.ToString(), filter.ToString());
            if (queryRes == null || queryRes.Count() == 0)
            {
                count = 0;
                return new JObject[0];
            }

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
            JObject multiFilter = new JObject() { { "$or", new JArray() { rr.Select(item => { ((JObject)item).Remove("blockindex"); return item; }).ToArray() } } };
            JArray multiRes = mh.GetData(Notify_mongodbConnStr, Notify_mongodbDatabase, queryBidListCollection, multiFilter.ToString());

            // 所有parenthash
            string[] parenthashArr = multiRes.Select(item => item["parenthash"].ToString()).Distinct().ToArray();
            Dictionary<string, string> parenthashDict = getDomainByHash(parenthashArr);

            // 所有区块索引
            long[] blockindexArr = multiRes.Select(item => long.Parse(item["startBlockSelling"].ToString())).Distinct().ToArray();
            //long[] cc = multiRes.Select(item => long.Parse(item["endBlock"].ToString())).Distinct().ToArray();
            blockindexArr = blockindexArr.Concat(multiRes.Select(item => long.Parse(item["endBlock"].ToString())).Distinct().ToArray()).ToArray();
            blockindexArr = blockindexArr.Concat(multiRes.Select(item => long.Parse(item["blockindex"].ToString())).Distinct().ToArray()).ToArray();
            Dictionary<string, long> blockindexDict = getBlockTimeByIndex(blockindexArr.Distinct().ToArray());

            // 分析结果
            JObject[] res = multiRes.GroupBy(item => item["domain"], (k, g) =>
            {
                string domain = k.ToString();
                return g.GroupBy(pItem => pItem["parenthash"], (kk, gg) =>
                {
                    string parenthash = kk.ToString();
                    // 过期竞拍列表
                    JToken[] expireDomainArr = gg.Where(p =>
                    {
                        long startBlockTime = blockindexDict.GetValueOrDefault(p["startBlockSelling"].ToString());
                        long startBlockSpentTime = getAuctionSpentTime(startBlockTime);
                        if (startBlockSpentTime >= ONE_DAY_SECONDS * DAYS_OF_YEAR)
                        {
                            return true;
                        }
                        return false;
                    }).ToArray();
                   

                    // 未过期竞拍列表
                    JToken[] noExpireDomainArr = gg.Where(p =>
                    {
                        long startBlockTime = blockindexDict.GetValueOrDefault(p["startBlockSelling"].ToString());
                        long startBlockSpentTime = getAuctionSpentTime(startBlockTime);
                        if (startBlockSpentTime < ONE_DAY_SECONDS * DAYS_OF_YEAR)
                        {
                            return true;
                        }
                        return false;
                    }).ToArray();


                    // 待处理竞拍
                    JToken[] normalDomainArr = noExpireDomainArr;
                    if (normalDomainArr == null || normalDomainArr.Count() == 0)
                    {
                        normalDomainArr = expireDomainArr;
                    }
                    // 筛选出最近一批竞拍记录
                    long lastStartBlockSelling = long.Parse(normalDomainArr.Where(p => p["maxPrice"].ToString() == "0").OrderByDescending(p => long.Parse(p["blockindex"].ToString())).Select(p => p["blockindex"].ToString()).ToArray()[0]);
                    //normalDomainArr = normalDomainArr.Where(p => long.Parse(p["blockindex"].ToString()) >= lastStartBlockSelling).ToArray();
                    normalDomainArr = normalDomainArr.Where(p => {
                        long blockindex = long.Parse(p["blockindex"].ToString());
                        long endBlock = long.Parse(p["endBlock"].ToString());
                        if(blockindex == lastStartBlockSelling && endBlock == 0)
                        {
                            return true;
                        }
                        if (blockindex > lastStartBlockSelling)
                        {
                            return true;
                        }
                        return false;
                    }
                    ).ToArray();

                    // 待处理竞拍是否流拍
                    bool noAnyAddPriceFlag = normalDomainArr.All(p =>
                    {
                        long startBlockTime = blockindexDict.GetValueOrDefault(p["startBlockSelling"].ToString());
                        long startBlockSpentTime = getAuctionSpentTime(startBlockTime);
                        if (startBlockSpentTime > THREE_DAY_SECONDS && p["maxPrice"].ToString() == "0")
                        {
                            return true;
                        }
                        return false;
                    });

                    // 流拍竞拍
                    if (noAnyAddPriceFlag)
                    {
                        return new JObject { { "auctionState", noAnyAddPriceState } };
                    }

                    //JToken[] maxPriceArr = gg.OrderByDescending(maxPriceItem => Convert.ToString(maxPriceItem["maxPrice"])).ToArray();
                    JToken maxPriceObj = normalDomainArr.OrderByDescending(maxPriceItem => double.Parse(Convert.ToString(maxPriceItem["maxPrice"]))).ToArray()[0];
                    //JToken maxPriceSlf = gg.Where(addpriceItem => addpriceItem["displayName"].ToString() == "addprice").OrderByDescending(maxPriceItem => Convert.ToString(maxPriceItem["maxPrice"])).ToArray().Where(slfItem => 
                    //    Convert.ToString(slfItem["maxBuyer"]) == address
                    //    || Convert.ToString(slfItem["maxBuyer"]) == null
                    //    || Convert.ToString(slfItem["maxBuyer"]) == ""
                    //    || Convert.ToString(slfItem["who"]) == address
                    //    ).ToArray()[0];

                    JObject obj = new JObject();

                    // 0. 我的竞价
                    //obj.Add("mybidprice", String.Format("{0:N8}", Convert.ToString(maxPriceSlf["maxBuyer"]) == address? maxPriceSlf["maxPrice"]: maxPriceSlf["value"]));

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
                    string maxBuyer = Convert.ToString(maxPriceObj["maxBuyer"]);
                    string maxPrice = Convert.ToString(maxPriceObj["maxPrice"]);
                    bool hasOnlyBidOpen = (maxBuyer == "" && maxPrice == "0");
                    List<JToken> endBlockToken = normalDomainArr.Where(pp => {
                        return Convert.ToString(pp["maxPrice"]) == maxPrice
                        && Convert.ToString(pp["displayName"]) == "domainstate"
                        && Convert.ToString(pp["endBlock"]) != "0";
                    }).ToList();
                    if (endBlockToken != null && endBlockToken.Count > 0)
                    {
                        blockHeightStrEd = Convert.ToString(endBlockToken[0]["endBlock"]);
                    }
                    // 最后出价者的出价时间
                    string lastAddPriceBlockIndexStr = normalDomainArr.OrderByDescending(p => p["blockindex"].ToString()).ToArray()[0]["blockindex"].ToString();
                    long lastAddPriceBlockIndex = blockindexDict.GetValueOrDefault(lastAddPriceBlockIndexStr);
                    long lastAddPriceBlockIndexSpentTime = getAuctionSpentTime(lastAddPriceBlockIndex);
                    auctionState = getAuctionState(auctionSpentTime, blockHeightStrEd, hasOnlyBidOpen, lastAddPriceBlockIndexSpentTime);
                    obj.Add("auctionState", auctionState);

                    // 4.竞拍最高价
                    obj.Add("maxPrice", maxPrice);

                    // 5.竞拍最高价地址
                    obj.Add("maxBuyer", maxBuyer);
                    // 6.竞拍已耗时(竞拍中显示)
                    if (auctionState == "1") ;
                    else if (auctionState == "2") auctionSpentTime -= THREE_DAY_SECONDS;
                    else auctionSpentTime = TWO_DAY_SECONDS;

                    obj.Add("domainsub", domain);
                    obj.Add("parenthash", parenthash);
                    obj.Add("endBlock", blockHeightStrEd);
                    //
                    obj.Add("blockindex", Convert.ToString(maxPriceObj["blockindex"]));
                    obj.Add("id", Convert.ToString(maxPriceObj["id"]));

                    return obj;
                }).ToArray();
            }).SelectMany(pItem => pItem).Where(p => Convert.ToString(p["auctionState"]) != noAnyAddPriceState).OrderByDescending(q => q["blockindex"]).ToArray(); ;

            // 中标域名添加owner
            JObject[] needAddOwnerArr = res.Where(item => item["auctionState"].ToString() == "0").Select(pItem => new JObject() { { "domain", pItem["domainsub"].ToString() }, { "parenthash", pItem["parenthash"].ToString() } }).ToArray();
            bool hasEndBlock = needAddOwnerArr.Count() != 0;
            Dictionary<string, string> ownerDict = getOwnerByDomainAndParentHash(needAddOwnerArr);
            return res.Select(item => {
                if (hasEndBlock && item["auctionState"].ToString() == "0")
                {
                    string domainsub = item["domainsub"].ToString();
                    string parenthash = item["parenthash"].ToString();
                    item.Remove("owner");
                    string owner = "";
                    if (ownerDict != null && ownerDict.Count() != 0)
                    {
                        owner = ownerDict.GetValueOrDefault(domainsub + parenthash);
                    }
                    item.Add("owner", owner == null ? "" : owner);

                    // 竞拍结束，更新已过时间
                    long startAuctionTime = int.Parse(Convert.ToString(item["startAuctionTime"]));
                    string endBlock = item["endBlock"].ToString();

                    long auctionSpentTime = !isEndAuction(endBlock) ? TWO_DAY_SECONDS : blockindexDict.GetValueOrDefault(endBlock) - startAuctionTime - THREE_DAY_SECONDS;
                    item.Remove("auctionSpentTime");
                    item.Add("auctionSpentTime", auctionSpentTime);
                }
                item.Remove("domainsub");
                item.Remove("parenthash");
                return item;
            }).ToArray();
        }


        public JArray getDomainInfoByAddress(string address, /*string domain*/ string auctionid="")
        {
            //string[] domainArr = domain.Split(".");
            JObject filter = new JObject();
            //filter.Add("domain", domainArr[0]);
            //filter.Add("parenthash", getNameHash(domainArr[1]));
            filter.Add("id", auctionid);
            filter.Add("displayName", "addprice");

            JObject sortBy = new JObject() { { "maxPrice", -1 } };
            JObject fieldFilter = new JObject() { { "maxBuyer", 1 }, { "maxPrice", 1 }, { "startBlockSelling", 1 } };
            JArray maxPriceObj = mh.GetDataPagesWithField(Notify_mongodbConnStr, Notify_mongodbDatabase, queryBidListCollection, fieldFilter.ToString(), 1, 1, sortBy.ToString(), filter.ToString());
            if (maxPriceObj != null && maxPriceObj.Count != 0)
            {
                JObject obj = (JObject)maxPriceObj[0];
                string maxBuyer = obj["maxBuyer"].ToString();
                string maxPrice = obj["maxPrice"].ToString();
                string mybidprice = "";
                if (address == maxBuyer)
                {
                    mybidprice = maxPrice;
                }
                else
                {
                    filter.Add("who", address);
                    fieldFilter.Add("value", 1);
                    JArray maxPriceSlf = mh.GetDataWithField(Notify_mongodbConnStr, Notify_mongodbDatabase, queryBidListCollection, fieldFilter.ToString(), filter.ToString());
                    mybidprice = maxPriceSlf.Sum(p => double.Parse(p["value"].ToString())).ToString();
                }
                return new JArray() { { new JObject() { { "id", auctionid }, { "maxBuyer", maxBuyer }, { "maxPrice", maxPrice }, { "mybidprice", mybidprice } } } };
            }
            return new JArray() { };

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
            // 最近一次开拍时间开始之后的竞拍记录
            long lastStartBlockSelling = queryRes.Where(p => p["maxPrice"].ToString() == "0").Select(p => long.Parse(p["blockindex"].ToString())).OrderByDescending(p => p).ToArray()[0];
            JToken[] queryArr = queryRes.Where(p => long.Parse(p["blockindex"].ToString()) >= lastStartBlockSelling).ToArray();
            // 分页
            JObject[] arr = queryArr.Select(item =>
            {
                string maxPrice = item["maxPrice"].ToString();
                string maxBuyer = item["maxBuyer"].ToString();
                string who = item["who"].ToString();
                if (maxBuyer != who)
                {
                    maxBuyer = who;
                    maxPrice = Convert.ToString(queryArr.Where(pItem => pItem["who"].ToString() == who && int.Parse(pItem["blockindex"].ToString()) <= int.Parse(item["blockindex"].ToString())).Sum(ppItem => double.Parse(ppItem["value"].ToString())));
                }
                long addPriceTime = blocktimeDict.GetValueOrDefault(Convert.ToString(item["blockindex"]));
                // 新增txid +出价人 +当笔出价金额
                string txid = item["txid"].ToString();
                string bidder = item["who"].ToString();
                double raisebid = double.Parse(item["value"].ToString());
                return new JObject() { { "maxPrice", maxPrice }, { "maxBuyer", maxBuyer }, { "addPriceTime", addPriceTime }, { "txid", txid }, { "bidder", bidder }, { "raisebid", raisebid } };

            }).Where(p => Convert.ToString(p["maxPrice"]) != "0").OrderByDescending(p => p["addPriceTime"]).ThenByDescending(p => double.Parse(p["maxPrice"].ToString())).Skip(pageSize * (pageNum - 1)).Take(pageSize).ToArray();
            // 总量
            long count = queryArr.Where(p => p["maxPrice"].ToString() != "0").ToArray().Count();
            // 返回
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

        private JObject toFilter(long[] blockindexArr, string field, string logicalOperator = "$or")
        {
            if (blockindexArr.Count() == 1)
            {
                return new JObject() { { field, blockindexArr[0] } };
            }
            return new JObject() { { logicalOperator, new JArray() { blockindexArr.Select(item => new JObject() { { field, item } }).ToArray() } } };
        }
        private JObject toReturn(string[] fieldArr)
        {
            JObject obj = new JObject();
            foreach (var field in fieldArr)
            {
                obj.Add(field, 1);
            }
            return obj;
        }
        private JObject toSort(string[] fieldArr, bool order = false)
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
            JObject fieldFilter = toReturn(new string[] { "maxPrice", "maxBuyer", "blockindex", "startBlockSelling", "endBlock" });
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

        private Dictionary<string, string> getDomainByHash(string[] parentHashArr)
        {
            JObject queryFilter = parentHashArr.Count() == 1
                    ? new JObject() { { "namehash", parentHashArr[0] } }
                    : new JObject() { { "$or", new JArray() { parentHashArr.Select(item => new JObject() { { "namehash", item } }).ToArray() } } };
            JObject fieldFilter = toReturn(new string[] { "namehash", "domain" });
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
        private Dictionary<string, string> getOwnerByDomainAndParentHash(JObject[] domainAndParenthashArr)
        {
            if (domainAndParenthashArr == null || domainAndParenthashArr.Count() == 0)
            {
                return null;
            }
            JObject filter = domainAndParenthashArr.Count() == 1 ? domainAndParenthashArr[0]
                    : new JObject() { { "$or", new JArray() { domainAndParenthashArr } } };
            JObject fieldFilter = new JObject() { { "domain", 1 }, { "parenthash", 1 }, { "owner", 1 }, { "blockindex", 1 } };
            JObject sortBy = new JObject() { { "blockindex", -1 } };
            JArray res = mh.GetDataWithField(Notify_mongodbConnStr, Notify_mongodbDatabase, queryDomainCollection, fieldFilter.ToString(), filter.ToString());
            if (res != null && res.Count > 0)
            {
                return res.GroupBy(item => item["domain"], (k, g) => {
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
        private string getAuctionState(long auctionSpentTime, string blockHeightStrEd, bool noAnyAddPriceFlag, long lastAddPriceAuctionSpentTime = 0)
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

                    if (noAnyAddPriceFlag)
                    {
                        // 超过三天无任何人出价则流拍
                        auctionState = noAnyAddPriceState;
                    }
                    else if (auctionSpentTime - lastAddPriceAuctionSpentTime <= TWO_DAY_SECONDS)
                    {
                        // 超过三天且第三天无出价则结束
                        auctionState = "0";
                    }
                    else if (auctionSpentTime > FIVE_DAY_SECONDS)
                    {
                        // 超过三天且第三天有出价且开拍时间超过5天时间则结束
                        auctionState = "0";
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
        private string noAnyAddPriceState = "noAnyAddPriceState";
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
        private bool queryHasTxFromBlock(string coll, string filter)
        {
            return mh.GetDataCount(Block_mongodbConnStr, Block_mongodbDatabase, coll, filter) >= 1;
        }
        private JArray queryNotifyFromBlock(string coll, string filter)
        {
            return mh.GetData(Block_mongodbConnStr, Block_mongodbDatabase, coll, filter);
        }

    }
}
