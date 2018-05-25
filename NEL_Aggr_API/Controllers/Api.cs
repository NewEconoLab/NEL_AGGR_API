using NEL_Agency_API.lib;
using NEL_Agency_API.RPC;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Bson;
using System.Security.Cryptography;

namespace NEL_Agency_API.Controllers
{
    public class Api
    {
        private string netnode { get; set; }
        private string mongodbConnStr { get; set; }
        private string mongodbDatabase { get; set; }
        private string notify_mongodbConnStr { get; set; }
        private string notify_mongodbDatabase { get; set; }
        private string nelJsonRPCUrl { get; set; }

        httpHelper hh = new httpHelper();
        mongoHelper mh = new mongoHelper();

        public Api(string node) {
            netnode = node;
            switch (netnode)
            {
                case "testnet":
                    mongodbConnStr = mh.mongodbConnStr_testnet;
                    mongodbDatabase = mh.mongodbDatabase_testnet;
                    notify_mongodbConnStr = mh.notify_mongodbConnStr_testnet;
                    notify_mongodbDatabase = mh.notify_mongodbDatabase_testnet;
                    nelJsonRPCUrl = mh.nelJsonRPCUrl_testnet;
                    break;
                case "mainnet":
                    mongodbConnStr = mh.mongodbConnStr_mainnet;
                    mongodbDatabase = mh.mongodbDatabase_mainnet;
                    notify_mongodbConnStr = mh.notify_mongodbConnStr_mainnet;
                    notify_mongodbDatabase = mh.notify_mongodbDatabase_mainnet;
                    nelJsonRPCUrl = mh.nelJsonRPCUrl_mainnet;
                    break;
            }
        }

        private JArray getJAbyKV(string key, object value)
        {
            return  new JArray
                        {
                            new JObject
                            {
                                {
                                    key,
                                    value.ToString()
                                }
                            }
                        };
        }

        private JArray getJAbyJ(JObject J)
        {
            return new JArray
                        {
                            J
                        };
        }

        public object getRes(JsonRPCrequest req,string reqAddr)
        {
            Logger.DebugLog("api.getRes start");
            Logger.DebugLog("api.getRes start:"+req.method);
            JArray result = new JArray();
            string resultStr = string.Empty;
            string findFliter = string.Empty;
            string sortStr = string.Empty;
            try
            {
                switch (req.method)
                {
                    // 根据地址查询域名
                    case "getdomainbyaddress":
                        string queryDomainCol = mh.queryDomainCollection;
                        string queryDomainAddr = "{\"owner\":\"" + req.@params[0] + "\"}";
                        JArray queryDomainRes = mh.GetData(mh.notify_mongodbConnStr_testnet, mh.notify_mongodbDatabase_testnet, queryDomainCol, queryDomainAddr);

                        // 域名列表
                        List<JObject> domainList = new List<JObject>();
                        foreach (var item in queryDomainRes)
                        {
                            // 全域名
                            string fullDomain = null;   // 域名
                            string resolver = null;     // 解析器Hex
                            string resolverAddr = null;     // 解析器地址
                            string ttl = null;      // 到期时间

                            // 本域名
                            string slfDomain = Convert.ToString(((JObject)item)["domain"]);
                            fullDomain += slfDomain;

                            // 父域名
                            string parentDomain = "";
                            string parentHash = Convert.ToString(((JObject)item)["parenthash"]);
                            if (parentHash != "")
                            {
                                string queryNameHash = "{\"namehash\":\"" + parentHash + "\"}";
                                JArray queryDomainResSub = mh.GetData(mh.notify_mongodbConnStr_testnet, mh.notify_mongodbDatabase_testnet, queryDomainCol, queryNameHash);
                                foreach (var dd in queryDomainResSub)
                                {
                                    // 父域名只有一个
                                    parentDomain += "." + Convert.ToString(((JObject)dd)["domain"]);
                                    break;
                                }
                            }
                            fullDomain += parentDomain;

                            // 解析器、解析地址、过期时间
                            resolver = Convert.ToString(((JObject)item)["resolver"]);
                            resolverAddr = "";
                            ttl = Convert.ToString(((JObject)item)["TTL"]);

                            // filter: 过滤掉过期的且被他人再次使用的
                            long expire = long.Parse(Convert.ToString(((JObject)item)["TTL"]));
                            long nowtime = TimeHelper.GetTimeStamp();
                            if (expire != 0 && expire < nowtime)
                            {
                                string filter = "{$and: [{\"domain\":\"" + slfDomain + "\"}" + "," + "{\"parenthash\":\"" + parentHash + "\"}" + "," + "{\"owner\":\"" + "{$not:" + req.@params[0] + "}" + "\"}" + "]}";
                                JArray queryDomainResFilter = mh.GetData(mh.notify_mongodbConnStr_testnet, mh.notify_mongodbDatabase_testnet, queryDomainCol, filter);
                                if (queryDomainResFilter != null && queryDomainResFilter.Count() > 0)
                                {
                                    continue;
                                }
                            }

                            domainList.Add(new JObject { { "domain", fullDomain }, { "resolver", resolver }, { "resolverAddress", resolverAddr }, { "ttl", ttl } });
                        }
                        result = new JArray { domainList.ToArray() };
                        break;

                    // 根据地址查询txid列表
                    case "gettxidsetbyaddress":
                        string queryTxidSetCol = mh.queryTxidSetCollection;
                        string queryTxidSetAddr = "{$or: [{\"from\":\"" + req.@params[0] + "\"}" + "," + "{\"to\":\"" + req.@params[0] + "\"}]}";
                        JArray queryTxidSetRes = mh.GetData(mh.notify_mongodbConnStr_testnet, mh.notify_mongodbDatabase_testnet, queryTxidSetCol, queryTxidSetAddr);
                        if (queryTxidSetRes == null || queryTxidSetRes.Count() == 0)
                        {
                            result = new JArray { };
                            break;
                        }

                        // 交易编号列表
                        List<string> txidList = new List<string>();
                        foreach (var item in queryTxidSetRes)
                        {
                            var txid = ((JObject)item)["txid"];
                            txidList.Add(Convert.ToString(txid));
                        }
                        result = new JArray { txidList.ToArray() };
                        break;

                    case "setnnsinfo":
                        findFliter = "{addr:\"" + req.@params[0].ToString() + "\",name:\"" + req.@params[1].ToString() + "\",time:\"" + req.@params[2].ToString() + "\"}";
                        result = getJAbyKV("result",mh.InsertOneData(mongodbConnStr, mongodbDatabase, "nns", findFliter));
                        break;
                    case "getnnsinfo":
                        string address = req.@params[0].ToString();
                        if (address != null && address != string.Empty)
                        {
                            findFliter = "{addr:\"" + address + "\"}";
                        }
                        result = mh.GetData(mongodbConnStr, mongodbDatabase, "nns", findFliter);
                        break;
                    case "delnnsinfo":
                        var domain = req.@params[0].ToString();
                        findFliter = "{name:\"" + domain + "\"}";
                        result = getJAbyKV("result", mh.DeleteData(mongodbConnStr, mongodbDatabase, "nns", findFliter));
                        break;
                    case "getaddresstxs":
                        
                        byte[] postdata;
                        string url;
                        result = getJAbyKV("result", "debugInfo:second");

                        //Logger.DebugLog("api.getRes getaddresstxs");
                        //Logger.DebugLog("api.getRes getaddresstxs:" + req.@params[0].ToString());
                        try
                        {

                            result = getJAbyKV("result", "debugInfo:first");
                            
                            url = httpHelper.MakeRpcUrlPost(nelJsonRPCUrl, "getaddresstxs", out postdata, new MyJson.JsonNode_ValueString(req.@params[0].ToString()), new MyJson.JsonNode_ValueNumber(int.Parse(req.@params[1].ToString())), new MyJson.JsonNode_ValueNumber(int.Parse(req.@params[2].ToString())));
                            result = (JArray)JObject.Parse(httpHelper.HttpPost(url, postdata))["result"];
                            //bool flag = true; if(flag) { break; }
                            
                             foreach (JObject jo in result)
                             {
                                 url = httpHelper.MakeRpcUrlPost(nelJsonRPCUrl, "getrawtransaction", out postdata, new MyJson.JsonNode_ValueString(jo["txid"].ToString()));
                                 JObject JOresult = (JObject)((JArray)JObject.Parse(httpHelper.HttpPost(url, postdata))["result"])[0];
                                
                                
                                 string type = JOresult["type"].ToString();
                                 jo.Add("type", type);
                                 JArray Vout = (JArray)JOresult["vout"];
                                 jo.Add("vout", Vout);
                                 JArray _Vin = (JArray)JOresult["vin"];
                                 JArray Vin = new JArray();
                                
                                
                                 foreach (JObject vin in _Vin)
                                 {
                                     string txid = vin["txid"].ToString();
                                     int n = (int)vin["vout"];
                                     url = httpHelper.MakeRpcUrlPost(nelJsonRPCUrl, "getrawtransaction", out postdata, new MyJson.JsonNode_ValueString(txid));

                                    /*
                                     JObject JOresult2 = (JObject)((JArray)JObject.Parse(httpHelper.HttpPost(url, postdata))["result"])[0];
                                    
                                     Vin.Add((JObject)((JArray)JOresult2["vout"])[n]);
                                     */
                                }
                                jo.Add("vin", Vin);
                                jo.Add("debug", "breakpoint3");
                                jo.Add(_Vin);

                            }


                            }
                        catch (Exception e)
                        {
                            Logger.ErrorLog(e);
                            result = getJAbyKV("result", "debugInfo:"+e.Message);
                        }
                        break;
                    case "getnep5transferbyaddress":
                        url = httpHelper.MakeRpcUrlPost(nelJsonRPCUrl, "getnep5transferbyaddress", out postdata, new MyJson.JsonNode_ValueString(req.@params[0].ToString()), new MyJson.JsonNode_ValueString("from"), new MyJson.JsonNode_ValueString(req.@params[1].ToString()), new MyJson.JsonNode_ValueString(req.@params[2].ToString()));
                        JArray JAresult = (JArray)JObject.Parse(httpHelper.HttpPost(url, postdata))["result"];
                        url = httpHelper.MakeRpcUrlPost(nelJsonRPCUrl, "getnep5transferbyaddress", out postdata, new MyJson.JsonNode_ValueString(req.@params[0].ToString()), new MyJson.JsonNode_ValueString("to"), new MyJson.JsonNode_ValueString(req.@params[1].ToString()), new MyJson.JsonNode_ValueString(req.@params[2].ToString()));
                        JAresult.Merge((JArray)JObject.Parse(httpHelper.HttpPost(url, postdata))["result"]);
                        result = JAresult;
                        break;
                    case "gettransbyaddress":
                        sortStr = "{\"blockindex\":-1,\"txid\":-1}";
                        findFliter = "{addr:\"" + req.@params[0].ToString() + "\"}";
                        result = mh.GetDataPages(mongodbConnStr, mongodbDatabase, "address_tx", sortStr, int.Parse(req.@params[1].ToString()), int.Parse(req.@params[2].ToString()), findFliter);
                        break;
                    case "getrankbyasset":
                        var asset = req.@params[0].ToString();
                        var count = int.Parse(req.@params[1].ToString());
                        var page = int.Parse(req.@params[2].ToString());
                        sortStr = "{\"balance\":-1}";
                        findFliter = "{asset:\"" + req.@params[0].ToString() + "\"}";
                        result = mh.GetDataPages(mongodbConnStr, mongodbDatabase, "allAssetRank", sortStr, int.Parse(req.@params[1].ToString()), int.Parse(req.@params[2].ToString()), findFliter);
                        break;
                    case "getrankbyassetcount":
                        findFliter = "{asset:\"" + req.@params[0].ToString() + "\"}";
                        result = getJAbyKV("count", mh.GetDataCount(mongodbConnStr, mongodbDatabase, "allAssetRank", findFliter));
                        break;
                    case "sendauthcode":
                        var phone = req.@params[0].ToString();
                        //防止频发发送
                        long timestamp = (DateTime.Now.ToUniversalTime().Ticks - 621355968000000000) / 10000000;
                        
                        findFliter = "{phone:\"" + phone + "\"}";
                        JArray Ja = mh.GetData(mongodbConnStr, mongodbDatabase, "authcode", findFliter);
                        if (Ja.Count>0 && timestamp - (long)Ja[0]["timestamp"] < 60)
                        {
                            result = getJAbyKV("result", "频繁提交");
                            break;
                        }
                        //随机六个数字作为验证码
                        Random rd = new Random();
                        var authcode = "";
                        for (int i = 0; i < 6; i++)
                        {
                            authcode += rd.Next(10).ToString();
                        }
                        var data = "{\"account\":\"I5676235\",\"password\":\"QFxy6lN2nua501\",\"mobile\":\""+ phone + "\",\"msg\":\"[NNS] Your verification code is  "+authcode+"  Please enter it within 5 minutes. Thank you for your support to NNS.\"}";
                        data = httpHelper.Post("http://intapi.253.com/send/json", data, System.Text.Encoding.UTF8,1);
                        JObject Jodata = JObject.Parse(data);
                        if (string.IsNullOrEmpty(Jodata["error"].ToString()))
                        {
                            //把验证码存入数据库
                            BsonDocument B = new BsonDocument();
                            B.Add("Date", DateTime.Now);
                            B.Add("timestamp", timestamp);
                            B.Add("phone", phone);
                            B.Add("authcode", authcode);
                            mh.DeleteData(mongodbConnStr, mongodbDatabase, "authcode", findFliter);
                            mh.InsertOneData(mongodbConnStr, mongodbDatabase, "authcode", B);
                        }
                        result = getJAbyKV("result", Jodata["error"].ToString());
                        break;
                    case "getauthcode":
                        phone = req.@params[0].ToString();
                        authcode = req.@params[1].ToString();
                        findFliter = "{phone:\"" + phone + "\",authcode:\""+ authcode + "\"}";
                        result = getJAbyKV("result",mh.GetData(mongodbConnStr, mongodbDatabase, "authcode", findFliter));
                        break;
                    case "applyfornnc":
                        var firstName = req.@params[0].ToString();
                        var lastName = req.@params[1].ToString();
                        var country = req.@params[2].ToString();
                        var str_type = req.@params[3].ToString();
                        var idNumber = req.@params[4].ToString();
                        var passportNumber = req.@params[5].ToString();
                        var passportPicture = req.@params[6].ToString();
                        var email = req.@params[7].ToString();
                        var mobileNumber = req.@params[8].ToString();
                        var walletAddress = req.@params[9].ToString();
                        var targetwalletAddress = req.@params[10].ToString();
                        var isbuttonclick = req.@params[11].ToString();
                        authcode = req.@params[12].ToString();
                        //判断钱包地址的审核状态
                        if (!string.IsNullOrEmpty(walletAddress)) 
                        {
                            findFliter = "{walletAddress:\"" + walletAddress + "\"}";
                            if (mh.GetDataCount(mongodbConnStr, mongodbDatabase, "applyfornnc", findFliter) > 0)
                            {
                                JArray JA_applyfornnc = mh.GetData(mongodbConnStr, mongodbDatabase, "applyfornnc", findFliter);
                                if (JA_applyfornnc.Count > 0)
                                {
                                    result = getJAbyKV("result", JA_applyfornnc[0]["state"]);
                                    break;
                                }
                            }
                            else
                            {
                            }
                        }

                        //判断email是否已经注册过
                        if (!string.IsNullOrEmpty(email))
                        {
                            findFliter = "{email:\"" + email+ "\"}";
                            if (mh.GetDataCount(mongodbConnStr, mongodbDatabase, "applyfornnc", findFliter) > 0)
                            {
                                result = getJAbyKV("result", "email used");
                                break;
                            }
                        }
                        //判断mobileNumber是否已经注册过
                        if (!string.IsNullOrEmpty(mobileNumber))
                        {
                            findFliter = "{mobileNumber:\"" + mobileNumber + "\"}";
                            if (mh.GetDataCount(mongodbConnStr, mongodbDatabase, "applyfornnc", findFliter) > 0)
                            {
                                result = getJAbyKV("result", "mobileNumber used");
                                break;
                            }
                        }
                        //判断targetwalletAddress是否已经注册过
                        if (!string.IsNullOrEmpty(targetwalletAddress))
                        {
                            findFliter = "{targetwalletAddress:\"" + targetwalletAddress + "\"}";
                            if (mh.GetDataCount(mongodbConnStr, mongodbDatabase, "applyfornnc", findFliter) > 0)
                            {
                                result = getJAbyKV("result", "targetwalletAddress used");
                                break;
                            }
                        }

                        //findFliter = "{phone:\"" + mobileNumber + "\",authcode:\"" + authcode + "\"}";
                        //result = mh.GetData(mongodbConnStr, mongodbDatabase, "authcode", findFliter);
                        //if (result.Count <= 0)
                        //{
                        //    result = getJAbyKV("result", "authcode error");
                        //    break;
                        //}
                        //

                        //如果有为空的就返回错误
                        if (string.IsNullOrEmpty(firstName) || 
                            string.IsNullOrEmpty(lastName) || 
                            string.IsNullOrEmpty(country) || 
                            string.IsNullOrEmpty(str_type) ||  
                            string.IsNullOrEmpty(passportPicture) || 
                            string.IsNullOrEmpty(email) || 
                            string.IsNullOrEmpty(mobileNumber) ||
                            string.IsNullOrEmpty(walletAddress)||
                            string.IsNullOrEmpty(targetwalletAddress)||
                            string.IsNullOrEmpty(isbuttonclick))
                        {
                            result = getJAbyKV("result", "params cant be none");
                            break;
                        }
                        //必须有一个有值
                        if (string.IsNullOrEmpty(idNumber) && string.IsNullOrEmpty(passportNumber))
                        {
                            result = getJAbyKV("result", "params cant be none");
                            break;
                        }
                        //是要入数据库的
                        if (isbuttonclick == "0")
                        {
                            result=getJAbyKV("result", "params ok");
                            break;
                        }
                        var files = Convert.FromBase64String(req.@params[6].ToString());

                        var hashstr = "";
                        try
                        {
                            var bytes_walletAddress = ThinNeo.Helper.GetPublicKeyHashFromAddress(walletAddress);
                            var hash = ThinNeo.Helper.Sha256(bytes_walletAddress);
                            hashstr = ThinNeo.Helper.Bytes2HexString(hash);
                        }
                        catch (Exception e)
                        {
                            result = getJAbyKV("result", "address is error");
                            break;
                        }

                        System.IO.File.WriteAllBytes(@"E:\ftp/"+ hashstr+".png", files);

                        BsonDocument Bson = new BsonDocument();
                        Bson.Add("firstName", firstName);
                        Bson.Add("lastName", lastName);
                        Bson.Add("country", country);
                        Bson.Add("str_type", str_type);
                        Bson.Add("idNumber", idNumber);
                        Bson.Add("passportNumber", passportNumber);
                        Bson.Add("passportPicture", hashstr);
                        Bson.Add("email", email);
                        Bson.Add("mobileNumber", mobileNumber);
                        Bson.Add("walletAddress", walletAddress);
                        Bson.Add("targetwalletAddress", targetwalletAddress);
                        Bson.Add("state", 2);
                        Bson.Add("commitTime", (DateTime.Now.ToUniversalTime().Ticks - 621355968000000000) / 10000000);
                        Bson.Add("checkTime",0);


                        result = getJAbyKV("result", mh.InsertOneData(mongodbConnStr, mongodbDatabase, "applyfornnc", Bson));
                        break;
                    case "login":
                        var user = req.@params[0].ToString();
                        var password = req.@params[1].ToString();
                        findFliter = "{user:\""+ user + "\",password:\""+password+"\"}";
                        var datacount = mh.GetDataCount(mongodbConnStr, mongodbDatabase, "manager", findFliter);
                        if (datacount <= 0)
                            result = getJAbyKV("result", 401);
                        else
                        {
                            var randon = RandomNumberGenerator.Create();
                            var b = new byte[12] ;
                            for (var i = 0; i < 12; i++)
                            {
                                b[i] = 1;
                            }
                            randon.GetBytes(b);
                            var str_b = ThinNeo.Helper.Base58CheckEncode(b);
                            findFliter = "{user:\"" + user + "\",password:\"" + password + "\",token:\""+ str_b + "\"}";
                            mh.ReplaceData(mongodbConnStr, mongodbDatabase, "manager", "{user:\"" + user + "\",password:\"" + password + "\"}", findFliter);
                            result = getJAbyKV("result", str_b);
                        }
                        break;
                    case "getapplyfornncbywalletAddress":
                        walletAddress = req.@params[0].ToString();
                        findFliter = "{walletAddress:\""+ walletAddress + "\"}";
                        result = mh.GetData(mongodbConnStr, mongodbDatabase, "applyfornnc",findFliter);
                        break;
                    case "getapplyfornnc":
                        sortStr = "{}";
                        findFliter = "{}";
                        result = mh.GetDataPages(mongodbConnStr, mongodbDatabase, "applyfornnc", sortStr, int.Parse(req.@params[0].ToString()), int.Parse(req.@params[1].ToString()), findFliter);
                        break;
                    case "getapplyfornnccount":
                        result = getJAbyKV("count", mh.GetDataCount(mongodbConnStr, mongodbDatabase, "applyfornnc"));
                        break;
                    case "checkapplyfornnc":
                        user = req.@params[0].ToString();
                        var token = req.@params[1].ToString();
                        walletAddress = req.@params[2].ToString();
                        var state = (Int64)req.@params[3];
                        //验证管理员
                        findFliter = "{user:\"" + user + "\",token:\"" + token + "\"}";
                        datacount = mh.GetDataCount(mongodbConnStr, mongodbDatabase, "manager", findFliter);
                        if (datacount <= 0)
                            result = getJAbyKV("result", 401);
                        else
                        {
                            findFliter = "{walletAddress:\"" + walletAddress + "\"}";
                            JObject JO_applyfornnc = (JObject)mh.GetData(mongodbConnStr, mongodbDatabase, "applyfornnc", findFliter)[0];
                            JO_applyfornnc["state"] = state;
                            JO_applyfornnc["checkTime"] = (DateTime.Now.ToUniversalTime().Ticks - 621355968000000000) / 10000000;
                            result = getJAbyKV("result", mh.ReplaceData(mongodbConnStr, mongodbDatabase, "applyfornnc", "{walletAddress:\"" + walletAddress + "\"}", JO_applyfornnc.ToString()));
                        }
                        break;
                    case "getpicture":
                        hashstr = req.@params[0].ToString();
                        files = System.IO.File.ReadAllBytes(@"E:\ftp/" + hashstr + ".png");
                        result = getJAbyKV("result", Convert.ToBase64String(files));
                        break;
                    case "setcontractscript":
                        string str_hash = (string)req.@params[0];
                        string str_avm = (string)req.@params[1];
                        string str_cs = (string)req.@params[2];
                        MyJson.IJsonNode JO_map = (MyJson.IJsonNode)MyJson.Parse((string)req.@params[3]);
                        MyJson.IJsonNode JO_abi = (MyJson.IJsonNode)MyJson.Parse((string)req.@params[4]);
                        string pathScript = @"E:\contract/";
                        if (System.IO.Directory.Exists(pathScript) == false)
                            System.IO.Directory.CreateDirectory(pathScript);
                        //把上传的几个文件都存到本地
                        System.IO.File.WriteAllBytes(System.IO.Path.Combine(pathScript, str_hash + ".avm"),ThinNeo.Helper.HexString2Bytes(str_avm));
                        System.IO.File.WriteAllText(System.IO.Path.Combine(pathScript, str_hash + ".cs"),Uri.UnescapeDataString(str_cs));
                        System.IO.File.WriteAllText(System.IO.Path.Combine(pathScript, str_hash + ".map.json"), JO_map.ToString());
                        System.IO.File.WriteAllText(System.IO.Path.Combine(pathScript, str_hash + ".abi.json"), JO_abi.ToString());
                        result = getJAbyKV("result", "suc");
                        break;
                    case "getcontractscript":
                        str_hash = (string)req.@params[0];
                        str_avm = ThinNeo.Helper.Bytes2HexString(System.IO.File.ReadAllBytes(System.IO.Path.Combine(@"E:\contract/", str_hash + ".avm")));
                        str_cs=System.IO.File.ReadAllText(System.IO.Path.Combine(@"E:\contract/", str_hash + ".cs"));
                        JO_map = (MyJson.IJsonNode)MyJson.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(@"E:\contract/", str_hash + ".map.json")));
                        JO_abi = (MyJson.IJsonNode)MyJson.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(@"E:\contract/", str_hash + ".abi.json")));
                        JObject JO_result = new JObject();
                        JO_result["avm"] = str_avm;
                        JO_result["cs"] = Uri.EscapeDataString(str_cs);
                        JO_result["map"] = JO_map.ToString();
                        JO_result["abi"] = JO_abi.ToString();
                        result = new JArray() { JO_result };
                        break;
                    case "getfulllog":
                        url = httpHelper.MakeRpcUrlPost(nelJsonRPCUrl, "getfulllog", out postdata, new MyJson.JsonNode_ValueString(req.@params[0].ToString()));
                        JAresult = (JArray)JObject.Parse(httpHelper.HttpPost(url, postdata))["result"];
                        result = JAresult;
                        break;
                    case "getdomainbyaddress3":
                        findFliter = "{owner:\""+ req.@params[0].ToString() + "\"}";
                        result = mh.GetData(notify_mongodbConnStr,notify_mongodbDatabase, "0x1ff70bb2147cf56c8b1ce0eb09323eb2b3f57916", findFliter);
                        break;
                    case "getdomainbyaddress2":
                        findFliter = "{owner:\"" + req.@params[0].ToString() + "\"}";
                        result = mh.GetData(notify_mongodbConnStr, notify_mongodbDatabase, "0x1ff70bb2147cf56c8b1ce0eb09323eb2b3f57916", findFliter);
                        MyJson.JsonNode_Object json = new MyJson.JsonNode_Object();
                        foreach(var r in result)
                        {
                            json[r["domain"].ToString()] =new MyJson.JsonNode_ValueString(r["domain"].ToString());
                        }
                        var keys = json.Keys;
                        result = new JArray(keys);
                        break;
                }
                if (result.Count == 0)
                {
                    JsonPRCresponse_Error resE = new JsonPRCresponse_Error(req.id, -1, "No Data", "Data does not exist");

                    return resE;
                }
            }
            catch (Exception e)
            {
                JsonPRCresponse_Error resE = new JsonPRCresponse_Error(req.id, -100, "Parameter Error", e.Message);

                return resE;

            }

            JsonPRCresponse res = new JsonPRCresponse();
            res.jsonrpc = req.jsonrpc;
            res.id = req.id;
            res.result = result;

            return res;
        }
    }
}
