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
            JObject[] res = queryDomainList2(address, pageNum, pageSize, prefixDomain);
            if (res == null || res.Length == 0)
            {
                return new JArray() { };
            }
            JObject rr = new JObject();
            rr.Add("list", new JArray() { res });
            rr.Add("count", "");
            return new JArray() { rr };
        }
        
        public JArray getBidListByAddress(string address, int pageNum=0, int pageSize=0)
        {
            JObject[] res = queryDomainList2(address, pageNum, pageSize);
            if(res == null || res.Length == 0)
            {
                return new JArray() { };
            }
            JObject rr = new JObject();
            rr.Add("list", new JArray() { res });
            rr.Add("count", "");


            return new JArray() { rr }; 
        }

        private JObject[] queryDomainList2(string address, int pageNum, int pageSize, string prefixDomain = "")
        {
            //breakPoint();   // add time point
            JObject filter = new JObject();
            filter.Add("who", address);
            filter.Add("displayName", "addprice");
            if(prefixDomain == "")
            {
                JObject likeFilter = new JObject();
                likeFilter.Add("$regex", prefixDomain);
                likeFilter.Add("$options", "i");
                filter.Add("domain", likeFilter);
            }
            //breakPoint();   // add time point
            JArray arr = null;
            if (pageNum < 1 || pageSize < 1)
            {
                arr = mh.GetData(Notify_mongodbConnStr, Notify_mongodbDatabase, queryBidListCollection, filter.ToString());
            }
            else
            {
                arr = mh.GetDataPages(Notify_mongodbConnStr, Notify_mongodbDatabase, queryBidListCollection, "{}", pageSize, pageNum, filter.ToString());
            }
            if(arr.Count == 0)
            {
                return new JObject[0];
            }
            //breakPoint();   // add time point
            var cc = arr.Select(s => new
            {
                domain = Convert.ToString(s["domain"]),
                parenthash = Convert.ToString(s["parenthash"])
            }).GroupBy(p => p.domain, (k,g) => g.ToArray()[0]).ToArray();
            //breakPoint();   // add time point
            JObject multiFilter = new JObject();
            JArray multiFilterSub = new JArray();
            foreach (var c in cc)
            {
                JObject obj = new JObject();
                obj.Add("domain", c.domain);
                obj.Add("parenthash", c.parenthash);
                multiFilterSub.Add(obj);
            }
            multiFilter.Add("$or", multiFilterSub);
            //breakPoint();   // add time point
            //JArray multiRes = queryNofity(queryBidListCollection, multiFilter.ToString());
            JArray multiRes = mh.GetData(Notify_mongodbConnStr, Notify_mongodbDatabase, queryBidListCollection, multiFilter.ToString());
            //breakPoint();
            JObject[] res = multiRes.GroupBy(sp => sp["domain"], (k, g) =>
            {
                string domain = k.ToString();
                string[] parenthashArr = g.Select(phItem => phItem["parenthash"].ToString()).Distinct().ToArray();
                //breakPoint2();
                Dictionary<string, string> phdict = getParentDomainByParentHash(parenthashArr);
                //breakPoint2();
                var rr= g.GroupBy(sp2 => sp2["parenthash"], (gk, gg) =>
                {
                    //TimeCollector tt = new TimeCollector();
                    string parenthash = gk.ToString();
                    /*
                    // *. 获取最大出价者
                    JToken maxPriceObj = gg.OrderByDescending(maxPriceItem => int.Parse(Convert.ToString(maxPriceItem["maxPrice"]))).ToArray()[0];
                    // *. 获取自己最高出价
                    JToken maxPriceSlf = gg.Where(
                        slfItem => Convert.ToString(slfItem["maxBuyer"]) == address
                        || Convert.ToString(slfItem["maxBuyer"]) == null
                        || Convert.ToString(slfItem["maxBuyer"]) == ""
                        ).OrderByDescending(maxPriceItem => int.Parse(Convert.ToString(maxPriceItem["maxPrice"]))).ToArray()[0];
                    */
                    //tt.breakPoint();
                    JToken[] maxPriceArr = gg.OrderByDescending(maxPriceItem => int.Parse(Convert.ToString(maxPriceItem["maxPrice"]))).ToArray();
                    //tt.breakPoint();
                    JToken maxPriceObj = maxPriceArr[0];
                    JToken maxPriceSlf = maxPriceArr.Where(
                        slfItem => Convert.ToString(slfItem["maxBuyer"]) == address
                        || Convert.ToString(slfItem["maxBuyer"]) == null
                        || Convert.ToString(slfItem["maxBuyer"]) == ""
                        ).ToArray()[0];

                    //tt.breakPoint();
                    JObject domainLastStateObj = new JObject();
                    // 1. 域名
                    string fullDomain = domain + phdict.GetValueOrDefault(parenthash); // 父域名 + 子域名
                    domainLastStateObj.Add("domain", fullDomain);
                    //tt.breakPoint();
                    // 2. 开标时间
                    long startAuctionTime;
                    String blockHeightStrSt = Convert.ToString(maxPriceSlf["startBlockSelling"]);
                    startAuctionTime = getStartAuctionTime(blockHeightStrSt);
                    domainLastStateObj.Add("startAuctionTime", startAuctionTime);
                    //tt.breakPoint();
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
                    //tt.breakPoint();
                    auctionState = getAuctionState(auctionSpentTime, blockHeightStrEd, hasOnlyBidOpen);
                    domainLastStateObj.Add("auctionState", auctionState);
                    //tt.breakPoint();
                    // 4.竞拍最高价
                    domainLastStateObj.Add("maxPrice", Convert.ToString(maxPriceObj["maxPrice"]));

                    // 5.竞拍最高价地址
                    domainLastStateObj.Add("maxBuyer", Convert.ToString(maxPriceObj["maxBuyer"]));
                    // 6.竞拍已耗时(竞拍中显示)
                    if (auctionSpentTime < THREE_DAY_SECONDS)
                    {
                        domainLastStateObj.Add("auctionSpentTime", auctionSpentTime);
                    }
                    else
                    {
                        domainLastStateObj.Add("auctionSpentTime", auctionSpentTime - THREE_DAY_SECONDS);
                    }
                    //tt.breakPoint();
                    // 7.域名所有者(竞拍结束显示)根据子域名和父域名哈希查询(从第一个Coll中查询)
                    string owner = "";//
                    if (isEndAuction(blockHeightStrEd))
                    {
                        owner = getOwnerByDomainAndParentHash(domain, parenthash);
                        //tt.breakPoint();
                        auctionSpentTime = getBlockTimeByBlokcIndex(blockHeightStrEd) - startAuctionTime - THREE_DAY_SECONDS;
                        domainLastStateObj.Remove("auctionSpentTime");
                        //tt.breakPoint();
                        domainLastStateObj.Add("auctionSpentTime", auctionSpentTime);
                    }
                    //tt.breakPoint();

                    domainLastStateObj.Add("owner", owner);

                    domainLastStateObj.Add("blockindex", Convert.ToString(maxPriceObj["blockindex"]));
                    domainLastStateObj.Add("id", Convert.ToString(maxPriceObj["id"]));
                    //tt.breakPointEnd();
                    //timeCollectorList.Add(tt.getRes());
                    return domainLastStateObj;

                }).ToArray();
                //breakPoint2();
                //breakPointEnd2();
                return rr;
            }).SelectMany(pItem => pItem).Where(p => Convert.ToString(p["auctionState"]) != canTryAgainBidFlag).OrderByDescending(q => q["blockindex"]).ToArray();
            //breakPoint();   // add time point
            //breakPointEnd();   // add time point
            return res;
        }
        private JObject[] queryDomainList(string address)
        {
            MyJson.JsonNode_Object req = new MyJson.JsonNode_Object();
            req.Add("who", new MyJson.JsonNode_ValueString(address));
            req.Add("displayName", new MyJson.JsonNode_ValueString("addprice"));
            JArray arr = queryNofity(queryBidListCollection, req.ToString());  
            
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
                JArray queyBidDetailRes = queryNofity(queryBidListCollection, queyBidDetailFilter.ToString());

                // ChangeLog-st-20180628
                // 1. 获取最大出价者
                JToken maxPriceObj = queyBidDetailRes.OrderByDescending(maxPriceItem => int.Parse(Convert.ToString(maxPriceItem["maxPrice"])))/*.OrderBy(minBlockindexItem => Convert.ToString(minBlockindexItem["getTime"]))*/.ToArray()[0];
                

                // 2. 获取自己最高出价
                JToken maxPriceSlf = queyBidDetailRes.Where(
                    slfItem => Convert.ToString(slfItem["maxBuyer"]) == address
                    || Convert.ToString(slfItem["maxBuyer"]) == null
                    || Convert.ToString(slfItem["maxBuyer"]) == ""
                    ).OrderByDescending(maxPriceItem => Convert.ToString(maxPriceItem["maxPrice"])).ToArray()[0];
                JObject domainLastStateObj = new JObject();
                // 1. 域名
                string fullDomain = item.domain + getParentDomainByParentHash(item.parenthash); // 父域名 + 子域名
                domainLastStateObj.Add("domain", fullDomain);
                // 2. 开标时间
                long startAuctionTime;
                String blockHeightStrSt = Convert.ToString(maxPriceSlf["startBlockSelling"]);
                startAuctionTime = getStartAuctionTime(blockHeightStrSt);
                domainLastStateObj.Add("startAuctionTime", startAuctionTime);
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
                List<JToken> endBlockToken = queyBidDetailRes.Where(pp => {
                    return Convert.ToString(pp["maxPrice"]) == maxPrice 
                    && Convert.ToString(pp["displayName"]) == "domainstate"
                    && Convert.ToString(pp["endBlock"]) != "0"; }).ToList();
                if(endBlockToken != null && endBlockToken.Count > 0)
                {
                    blockHeightStrEd = Convert.ToString(endBlockToken[0]["endBlock"]);
                }

                auctionState = getAuctionState(auctionSpentTime, blockHeightStrEd, hasOnlyBidOpen);
                domainLastStateObj.Add("auctionState", auctionState);
                // 4.竞拍最高价
                domainLastStateObj.Add("maxPrice", Convert.ToString(maxPriceObj["maxPrice"]));

                // 5.竞拍最高价地址
                domainLastStateObj.Add("maxBuyer", Convert.ToString(maxPriceObj["maxBuyer"]));
                // 6.竞拍已耗时(竞拍中显示)
                if (auctionSpentTime < THREE_DAY_SECONDS)
                {
                    domainLastStateObj.Add("auctionSpentTime", auctionSpentTime);
                }
                else
                {
                    domainLastStateObj.Add("auctionSpentTime", auctionSpentTime - THREE_DAY_SECONDS);
                }
                // 7.域名所有者(竞拍结束显示)根据子域名和父域名哈希查询(从第一个Coll中查询)
                string owner = "";//
                if (isEndAuction(blockHeightStrEd))
                {
                    owner = getOwnerByDomainAndParentHash(item.domain, item.parenthash);
                    auctionSpentTime = getBlockTimeByBlokcIndex(blockHeightStrEd) - startAuctionTime - THREE_DAY_SECONDS;
                    domainLastStateObj.Remove("auctionSpentTime");
                    domainLastStateObj.Add("auctionSpentTime", auctionSpentTime);
                }
                domainLastStateObj.Add("owner", owner);

                string blockindexStr = Convert.ToString(maxPriceObj["blockindex"]);
                domainLastStateObj.Add("blockindex", blockindexStr);

                domainLastStateObj.Add("id", Convert.ToString(maxPriceObj["id"]));

                return domainLastStateObj;
                // ChangeLog-ed-20180628
                // ChangeLog-ed


                /*
                // 按出价人分组并计算出价总额，然后选出最高出价人
                var maxPriceList = queyBidDetailRes.GroupBy(p => p["maxBuyer"], (k, g) => new
                {
                    maxBuyer = k,
                    maxPrice = g.Sum(q => double.Parse(Convert.ToString(q["maxPrice"]))),
                    token = g.OrderByDescending(c => c["blockindex"]).ToList()[0]
                }).OrderByDescending(k => k.maxPrice).ToList();
                JToken token = maxPriceList[0].token;
                bool hasOnlyBidOpen = maxPriceList.Count == 1 && Convert.ToString(token["maxBuyer"]) == "";
                double mybidprice = hasOnlyBidOpen ? 0:maxPriceList.Where(p => p.maxBuyer.ToString().Equals(address)).First().maxPrice;

                JObject obj = new JObject();
                obj.Add("mybidprice", String.Format("{0:N8}", mybidprice));

                // 1. 域名
                string fullDomain = item.domain + getParentDomainByParentHash(item.parenthash); // 父域名 + 子域名
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
                auctionState = getAuctionState(auctionSpentTime, blockHeightStrEd, hasOnlyBidOpen);
                obj.Add("auctionState", auctionState);

                // 4.竞拍最高价
                obj.Add("maxPrice", String.Format("{0:N8}", maxPriceList[0].maxPrice));

                // 5.竞拍最高价地址
                string maxBuyer = hasOnlyBidOpen ? "":Convert.ToString(token["maxBuyer"]);
                obj.Add("maxBuyer", maxBuyer);

                // 6.竞拍已耗时(竞拍中显示)
                if (auctionSpentTime < THREE_DAY_SECONDS)
                {
                    obj.Add("auctionSpentTime", auctionSpentTime);
                }
                else
                {
                    obj.Add("auctionSpentTime", auctionSpentTime - THREE_DAY_SECONDS);
                }

                // 7.域名所有者(竞拍结束显示)根据子域名和父域名哈希查询(从第一个Coll中查询)
                string owner = "";//
                if (isEndAuction(blockHeightStrEd))
                {
                    owner = getOwnerByDomainAndParentHash(item.domain, item.parenthash);
                    auctionSpentTime = getBlockTimeByBlokcIndex(blockHeightStrEd) - startAuctionTime - THREE_DAY_SECONDS;
                    obj.Remove("auctionSpentTime");
                    obj.Add("auctionSpentTime", auctionSpentTime);
                }
                obj.Add("owner", owner);

                string blockindexStr = Convert.ToString(token["blockindex"]);
                obj.Add("blockindex", blockindexStr);
                return obj;
                */
            }).Where(p => Convert.ToString(p["auctionState"]) != canTryAgainBidFlag).OrderByDescending(q => q["blockindex"]).ToArray();
            return res;
        }
        
        public JArray getBidDetailByDomain(string domain, int pageNum=0, int pageSize=0)
        {
            // 计算slfDomain + parentHash
            string[] domainArr = domain.Split(".");
            MyJson.JsonNode_Object queyBidDetailFilter = new MyJson.JsonNode_Object();
            queyBidDetailFilter.Add("domain", new MyJson.JsonNode_ValueString(domainArr[0]));
            queyBidDetailFilter.Add("parenthash", new MyJson.JsonNode_ValueString(getNameHash(domainArr[1])));
            queyBidDetailFilter.Add("displayName", new MyJson.JsonNode_ValueString("addprice"));
            //JArray queyBidDetailRes = queryNofity(queryBidListCollection, queyBidDetailFilter.ToString());
            JArray queryBidDetailRes = mh.GetDataPages(Notify_mongodbConnStr, Notify_mongodbDatabase, queryBidListCollection, "{}", pageSize, pageNum, queyBidDetailFilter.ToString());

            // 获取与该域名相关数据
            /*
            JObject[] arr = queryBidDetailRes.Where(pItem => 
                pItem["maxBuyer"].ToString() == pItem["who"].ToString()
                || pItem["maxBuyer"].ToString() == ""
                || pItem["maxBuyer"].ToString() == null).Select(item =>
                */
            JObject[] arr = queryBidDetailRes.Select(item => {
                string maxPrice = item["maxPrice"].ToString();
                string maxBuyer = item["maxBuyer"].ToString();
                string who = item["who"].ToString();
                if (maxBuyer != who)
                {
                    maxBuyer = who;
                    maxPrice = Convert.ToString(queryBidDetailRes.Where(pItem => pItem["who"].ToString() == who && int.Parse(pItem["blockindex"].ToString()) <= int.Parse(item["blockindex"].ToString())).Sum(ppItem => int.Parse(ppItem["value"].ToString())));

                }
                JObject obj = new JObject();
                obj.Add("maxPrice", maxPrice);
                obj.Add("maxBuyer", maxBuyer);

                // 根据blockindex获取block.time即为加价时间
                String blockHeightStrSt = Convert.ToString(((JObject)item)["blockindex"]);
                long addPriceTime = getBlockTimeByBlokcIndex(blockHeightStrSt);
                obj.Add("addPriceTime", addPriceTime);
                return obj;
            }).OrderByDescending(p => p["addPriceTime"]).ToArray();

            long count = mh.GetDataCount(Notify_mongodbConnStr, Notify_mongodbDatabase, queryBidListCollection, queyBidDetailFilter.ToString());
            JObject rr = new JObject();
            rr.Add("list", new JArray() { arr });
            rr.Add("count", count);
            return new JArray() { rr };
        }

        public JArray getBidResByDomain(string domain)
        {
            // 计算slfDomain + parentHash
            string[] domainArr = domain.Split(".");
            MyJson.JsonNode_Object queyBidDetailFilter = new MyJson.JsonNode_Object();
            queyBidDetailFilter.Add("domain", new MyJson.JsonNode_ValueString(domainArr[0]));
            queyBidDetailFilter.Add("parenthash", new MyJson.JsonNode_ValueString(getNameHash(domainArr[1])));
            JArray queyBidDetailRes = queryNofity(queryBidListCollection, queyBidDetailFilter.ToString());

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
            
            res.Remove("startBlockSelling");
            res.Remove("endBlock");

            return new JArray() { res };
        }


        private Dictionary<string,string> getParentDomainByParentHash(string[] parentHashArr)
        {
            JObject filter = new JObject();
            JArray filterSub = new JArray();
            foreach (string parentHash in parentHashArr)
            {
                JObject obj = new JObject();
                obj.Add("namehash", parentHash);
                filterSub.Add(obj);
            }
            filter.Add("$or", filterSub);

            JArray res = queryNofity(queryDomainCollection, filter.ToString());
            return res.GroupBy(item => item["namehash"], (k, g) =>
            {
                JObject obj = new JObject();
                obj.Add("namehash", k.ToString());
                obj.Add("domain", "." + g.ToArray()[0]["domain"].ToString());
                return obj;
            }).ToArray().ToDictionary(key => key["namehash"].ToString(), val => val["domain"].ToString());
        }
        private string getParentDomainByParentHash(string parentHash)
        {
            string parentDomain = "";
            if (parentHash != "")
            {
                string queryNameHash = "{\"namehash\":\"" + parentHash + "\"}";
                JArray queryDomainResSub = queryNofity(queryDomainCollection, queryNameHash);
                foreach (var dd in queryDomainResSub)
                {
                    // 父域名只有一个
                    parentDomain += "."+Convert.ToString(((JObject)dd)["domain"]);
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
            JArray queryOwnerRes = queryNofity(queryDomainCollection, queyOwnerFilter.ToString());
            if(queryOwnerRes != null && queryOwnerRes.Count > 0)
            {
                owner = queryOwnerRes[0]["owner"].ToString();
            }
            return owner;
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
                auctionState = "Fixed period";
            } 
            else
            {
                if (!isEndAuction(blockHeightStrEd))
                {
                    // 随机
                    auctionState = "Random period";

                    if (hasOnlyBidOpen &&auctionSpentTime > FIVE_DAY_SECONDS)
                    {
                        // 流拍
                        auctionState = canTryAgainBidFlag;
                    }
                }
                else
                {
                    // 结束
                    auctionState = "Ended";
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
}
