using NEL_Agency_API.lib;
using NEL_Agency_API.RPC;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Bson;

namespace NEL_Agency_API.Controllers
{
    public class Api
    {
        private string netnode { get; set; }
        private string mongodbConnStr { get; set; }
        private string mongodbDatabase { get; set; }
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
                    nelJsonRPCUrl = mh.nelJsonRPCUrl_testnet;
                    break;
                case "mainnet":
                    mongodbConnStr = mh.mongodbConnStr_mainnet;
                    mongodbDatabase = mh.mongodbDatabase_mainnet;
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
            JArray result = new JArray();
            string resultStr = string.Empty;
            string findFliter = string.Empty;
            string sortStr = string.Empty;
            try
            {
                switch (req.method)
                {
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
                        address = req.@params[0].ToString();
                        var domain = req.@params[1].ToString();
                        if (address != null && address != string.Empty)
                        {
                            findFliter = "{addr:\"" + address + "\"name:\""+ domain + "\"}";
                        }
                        result = getJAbyKV("result", mh.DeleteData(mongodbConnStr, mongodbDatabase, "nns", findFliter));
                        break;
                    case "getaddresstxs":
                        byte[] postdata;
                        string url = httpHelper.MakeRpcUrlPost(nelJsonRPCUrl, "getaddresstxs", out postdata, new MyJson.JsonNode_ValueString(req.@params[0].ToString()), new MyJson.JsonNode_ValueNumber(int.Parse(req.@params[1].ToString())), new MyJson.JsonNode_ValueNumber(int.Parse(req.@params[2].ToString())));
                        result  = (JArray)JObject.Parse(httpHelper.HttpPost(url, postdata))["result"];
                        foreach (JObject jo in result)
                        {
                            url = httpHelper.MakeRpcUrlPost(nelJsonRPCUrl, "getrawtransaction", out postdata, new MyJson.JsonNode_ValueString(jo["txid"].ToString()));
                            JObject JOresult = (JObject)((JArray)JObject.Parse(httpHelper.HttpPost(url, postdata))["result"])[0];
                            string type = JOresult["type"].ToString();
                            jo.Add("type", type);
                            JArray Vout = (JArray)JOresult["vout"];
                            jo.Add("vout",Vout);
                            JArray _Vin = (JArray)JOresult["vin"];
                            JArray Vin = new JArray();
                            foreach (JObject vin in _Vin)
                            {
                                string txid = vin["txid"].ToString();
                                int n = (int)vin["vout"];
                                url = httpHelper.MakeRpcUrlPost(nelJsonRPCUrl, "getrawtransaction", out postdata, new MyJson.JsonNode_ValueString(txid));
                                JObject JOresult2 = (JObject)((JArray)JObject.Parse(httpHelper.HttpPost(url, postdata))["result"])[0];
                                Vin.Add((JObject)((JArray)JOresult2["vout"])[n]);
                            }
                            jo.Add("vin", Vin);
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
                        sortStr = "{'blockindex':-1,'txid':-1}";
                        findFliter = "{addr:\"" + req.@params[0].ToString() + "\"}";
                        result = mh.GetDataPages(mongodbConnStr, mongodbDatabase, "address_tx", sortStr, int.Parse(req.@params[1].ToString()), int.Parse(req.@params[2].ToString()), findFliter);
                        break;
                    case "getrankbyasset":
                        var count = int.Parse(req.@params[1].ToString());
                        var page = int.Parse(req.@params[2].ToString());
                        url = httpHelper.MakeRpcUrlPost(nelJsonRPCUrl, "getnep5transfersbyasset", out postdata, new MyJson.JsonNode_ValueString(req.@params[0].ToString()));
                        JAresult = (JArray)JObject.Parse(httpHelper.HttpPost(url, postdata))["result"];
                        Dictionary<string, decimal> dic = new Dictionary<string, decimal>();
                        foreach (JObject joresult in JAresult)
                        {
                            if (!string.IsNullOrEmpty(joresult["from"].ToString()))
                            {
                                if (dic.ContainsKey(joresult["from"].ToString()))
                                {
                                    dic[joresult["from"].ToString()] = (decimal)dic[joresult["from"].ToString()] - (decimal)joresult["value"];
                                }
                                else
                                {
                                    dic[joresult["from"].ToString()] =-(decimal)joresult["value"];
                                }
                            }
                            if (!string.IsNullOrEmpty(joresult["to"].ToString()))
                            {
                                if (dic.ContainsKey(joresult["to"].ToString()))
                                {
                                    dic[joresult["to"].ToString()] = (decimal)dic[joresult["to"].ToString()] + (decimal)joresult["value"];
                                }
                                else
                                {
                                    dic[joresult["to"].ToString()] = +(decimal)joresult["value"];
                                }
                            }
                        }
                        var dic2 = dic.ToList();
                        dic2.Sort((a, b) =>
                        {
                            if (a.Value > b.Value)
                                return -1;
                            else if (a.Value < b.Value)
                                return 1;
                            else
                                return 0;
                        });
                        foreach (var key in dic2)
                        {
                            JObject j = new JObject();
                            j[key.Key] = dic[key.Key];
                            result.Add(j);
                        }
                        var result_ = result.Skip((page-1)*count).Take(count).ToArray();
                        result = new JArray();
                        foreach (var i in result_)
                        {
                            result.Add(i);
                        }
                        break;
                    case "rankbyassetcount":

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
                        var data = "{\"account\":\"I5676235\",\"password\":\"QFxy6lN2nua501\",\"mobile\":\""+ phone + "\",\"msg\":\"【NNS】 Your verification code is  "+authcode+"  Please enter it within 5 minutes. Thank you for your support to NNS. Wishing you a happy life!\"}";
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
