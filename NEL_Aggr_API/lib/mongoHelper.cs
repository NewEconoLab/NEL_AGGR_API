﻿using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using MongoDB.Bson.IO;

namespace NEL_Agency_API.lib
{
    public class mongoHelper
    {
        public string mongodbConnStr_testnet = string.Empty;
        public string mongodbDatabase_testnet = string.Empty;
        public string nelJsonRPCUrl_testnet = string.Empty;
        public string notify_mongodbConnStr_testnet = string.Empty;
        public string notify_mongodbDatabase_testnet = string.Empty;

        public string mongodbConnStr_mainnet = string.Empty;
        public string mongodbDatabase_mainnet = string.Empty;
        public string nelJsonRPCUrl_mainnet = string.Empty;
        public string notify_mongodbConnStr_mainnet = string.Empty;
        public string notify_mongodbDatabase_mainnet = string.Empty;

        public string mongodbConnStr_NeonOnline = string.Empty;
        public string mongodbDatabase_NeonOnline = string.Empty;
        
        public string mongodbConnStrAtBlock_mainnet = string.Empty;
        public string mongodbDatabaseAtBlock_mainnet = string.Empty;
        public string mongodbConnStrAtBlock_testnet = string.Empty;
        public string mongodbDatabaseAtBlock_testnet = string.Empty;

        public string ossServiceUrl_testnet = string.Empty;
        public string ossServiceUrl_mainnet = string.Empty;
        public string queryDomainCollection_testnet = string.Empty;
        public string queryDomainCollection_mainnet = string.Empty;
        
        public mongoHelper() {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection()    //将配置文件的数据加载到内存中
                .SetBasePath(System.IO.Directory.GetCurrentDirectory())   //指定配置文件所在的目录
                .AddJsonFile("mongodbsettings.json", optional: true, reloadOnChange: true)  //指定加载的配置文件
                .Build();    //编译成对象  
            mongodbConnStr_testnet = config["mongodbConnStr_testnet"];
            mongodbDatabase_testnet = config["mongodbDatabase_testnet"];
            notify_mongodbConnStr_testnet = config["notify_mongodbConnStr_testnet"];
            notify_mongodbDatabase_testnet = config["notify_mongodbDatabase_testnet"];
            nelJsonRPCUrl_testnet = config["nelJsonRPCUrl_testnet"];

            mongodbConnStr_mainnet = config["mongodbConnStr_mainnet"];
            mongodbDatabase_mainnet = config["mongodbDatabase_mainnet"];
            notify_mongodbConnStr_mainnet = config["notify_mongodbConnStr_mainnet"];
            notify_mongodbDatabase_mainnet = config["notify_mongodbDatabase_mainnet"];
            nelJsonRPCUrl_mainnet = config["nelJsonRPCUrl_mainnet"];

            mongodbConnStr_NeonOnline = config["mongodbConnStr_NeonOnline"];
            mongodbDatabase_NeonOnline = config["mongodbDatabase_NeonOnline"];
            
            queryDomainCollection_testnet = config["queryDomainCollection_testnet"];
            queryDomainCollection_mainnet = config["queryDomainCollection_mainnet"];
            
            mongodbConnStrAtBlock_mainnet = config["mongodbConnStrAtBlock_mainnet"];
            mongodbDatabaseAtBlock_mainnet = config["mongodbDatabaseAtBlock_mainnet"];
            mongodbConnStrAtBlock_testnet = config["mongodbConnStrAtBlock_testnet"];
            mongodbDatabaseAtBlock_testnet = config["mongodbDatabaseAtBlock_testnet"];

            ossServiceUrl_testnet = config["ossServiceUrl_testnet"];
            ossServiceUrl_mainnet = config["ossServiceUrl_mainnet"];
            
        }

        public JArray GetData(string mongodbConnStr,string mongodbDatabase, string coll,string findFliter)
        {
            var client = new MongoClient(mongodbConnStr);
            var database = client.GetDatabase(mongodbDatabase);
            var collection = database.GetCollection<BsonDocument>(coll);

            List<BsonDocument> query = collection.Find(BsonDocument.Parse(findFliter)).ToList();
            client = null;

            if (query.Count > 0)
            {
                var jsonWriterSettings = new JsonWriterSettings { OutputMode = JsonOutputMode.Strict };
                JArray JA = JArray.Parse(query.ToJson(jsonWriterSettings));
                foreach (JObject j in JA)
                {
                    j.Remove("_id");
                }
                return JA;
            }
            else { return new JArray(); }      
        }

        public JArray GetDataPages(string mongodbConnStr, string mongodbDatabase, string coll,string sortStr, int pageCount, int pageNum, string findBson = "{}")
        {
            var client = new MongoClient(mongodbConnStr);
            var database = client.GetDatabase(mongodbDatabase);
            var collection = database.GetCollection<BsonDocument>(coll);

            List<BsonDocument> query = collection.Find(BsonDocument.Parse(findBson)).Sort(sortStr).Skip(pageCount * (pageNum-1)).Limit(pageCount).ToList();
            client = null;

            if (query.Count > 0)
            {

                var jsonWriterSettings = new JsonWriterSettings { OutputMode = JsonOutputMode.Strict };
                JArray JA = JArray.Parse(query.ToJson(jsonWriterSettings));
                foreach (JObject j in JA)
                {
                    j.Remove("_id");
                }
                return JA;
            }
            else { return new JArray(); }
        }

        public JArray GetDataPagesWithField(string mongodbConnStr, string mongodbDatabase, string coll, string fieldBson, int pageCount, int pageNum, string sortStr = "{}", string findBson = "{}")
        {
            var client = new MongoClient(mongodbConnStr);
            var database = client.GetDatabase(mongodbDatabase);
            var collection = database.GetCollection<BsonDocument>(coll);

            List<BsonDocument> query = collection.Find(BsonDocument.Parse(findBson)).Project(BsonDocument.Parse(fieldBson)).Sort(sortStr).Skip(pageCount * (pageNum - 1)).Limit(pageCount).ToList();
            client = null;

            if (query.Count > 0)
            {
                var jsonWriterSettings = new JsonWriterSettings { OutputMode = JsonOutputMode.Strict };
                JArray JA = JArray.Parse(query.ToJson(jsonWriterSettings));
                foreach (JObject j in JA)
                {
                    j.Remove("_id");
                }
                return JA;
            }
            else { return new JArray(); }
        }
        public JArray GetDataWithField(string mongodbConnStr, string mongodbDatabase, string coll, string fieldBson, string findBson = "{}")
        {
            var client = new MongoClient(mongodbConnStr);
            var database = client.GetDatabase(mongodbDatabase);
            var collection = database.GetCollection<BsonDocument>(coll);

            List<BsonDocument> query = collection.Find(BsonDocument.Parse(findBson)).Project(BsonDocument.Parse(fieldBson)).ToList();
            client = null;

            if (query.Count > 0)
            {
                var jsonWriterSettings = new JsonWriterSettings { OutputMode = JsonOutputMode.Strict };
                JArray JA = JArray.Parse(query.ToJson(jsonWriterSettings));
                foreach (JObject j in JA)
                {
                    j.Remove("_id");
                }
                return JA;
            }
            else { return new JArray(); }
        }

        public long GetDataCount(string mongodbConnStr, string mongodbDatabase,string coll)
        {
            var client = new MongoClient(mongodbConnStr);
            var database = client.GetDatabase(mongodbDatabase);
            var collection = database.GetCollection<BsonDocument>(coll);

            var txCount = collection.Find(new BsonDocument()).Count();

            client = null;

            return txCount;
        }

        public long GetDataCount(string mongodbConnStr, string mongodbDatabase, string coll, string findBson)
        {
            var client = new MongoClient(mongodbConnStr);
            var database = client.GetDatabase(mongodbDatabase);
            var collection = database.GetCollection<BsonDocument>(coll);

            var txCount = collection.Find(BsonDocument.Parse(findBson)).Count();

            client = null;

            return txCount;
        }

        public JArray Getdatablockheight(string mongodbConnStr, string mongodbDatabase)
        {
            int blockDataHeight = -1;
            int txDataHeight = -1;
            int utxoDataHeight = -1;
            int notifyDataHeight = -1;
            int fulllogDataHeight = -1;

            var client = new MongoClient(mongodbConnStr);
            var database = client.GetDatabase(mongodbDatabase);

            var collection = database.GetCollection<BsonDocument>("block");
            var sortBson = BsonDocument.Parse("{index:-1}");
            var query = collection.Find(new BsonDocument()).Sort(sortBson).Limit(1).ToList();
            if (query.Count > 0)
            {blockDataHeight = (int)query[0]["index"];}

            collection = database.GetCollection<BsonDocument>("tx");
            sortBson = BsonDocument.Parse("{blockindex:-1}");
            query = collection.Find(new BsonDocument()).Sort(sortBson).Limit(1).ToList();
            if (query.Count > 0)
            { txDataHeight = (int)query[0]["blockindex"]; }

            collection = database.GetCollection<BsonDocument>("system_counter");
            query = collection.Find(new BsonDocument()).ToList();
            if (query.Count > 0)
            {
                foreach (var q in query)
                {
                    if ((string)q["counter"] == "utxo") { utxoDataHeight = (int)q["lastBlockindex"]; };
                    if ((string)q["counter"] == "notify") { notifyDataHeight = (int)q["lastBlockindex"]; };
                    if ((string)q["counter"] == "fulllog") { fulllogDataHeight = (int)q["lastBlockindex"]; };
                }
            }

            client = null;

            JObject J = new JObject
            {
                { "blockDataHeight", blockDataHeight },
                { "txDataHeight", txDataHeight },
                { "utxoDataHeight", utxoDataHeight },
                { "notifyDataHeight", notifyDataHeight },
                { "fulllogDataHeight", fulllogDataHeight }
            };
            JArray JA = new JArray
            {
                J
            };

            return JA;
        }

        public void InsertOneDataByCheckKey(string mongodbConnStr, string mongodbDatabase, string coll, JObject Jdata,string key,string value)
        {
            var client = new MongoClient(mongodbConnStr);
            var database = client.GetDatabase(mongodbDatabase);
            var collection = database.GetCollection<BsonDocument>(coll);

            var query = collection.Find("{'" + key + "':'" + value +"'}").ToList();
            if (query.Count == 0) {
                string strData = Newtonsoft.Json.JsonConvert.SerializeObject(Jdata);
                BsonDocument bson = BsonDocument.Parse(strData);
                bson.Add("getTime", DateTime.Now);
                collection.InsertOne(bson);
            }

            client = null;
        }

        public string InsertOneData(string mongodbConnStr, string mongodbDatabase, string coll,string insertBson)
        {
            var client = new MongoClient(mongodbConnStr);
            var database = client.GetDatabase(mongodbDatabase);
            var collection = database.GetCollection<BsonDocument>(coll);
            try
            {
                var query = collection.Find(BsonDocument.Parse(insertBson)).ToList();
                if (query.Count == 0)
                {
                    BsonDocument bson = BsonDocument.Parse(insertBson);
                    collection.InsertOne(bson);
                }
                client = null;
                return "suc";
            }
            catch (Exception e)
            {
                return e.ToString();
            }

        }

        public string InsertOneData(string mongodbConnStr, string mongodbDatabase, string coll, BsonDocument insertBson)
        {
            var client = new MongoClient(mongodbConnStr);
            var database = client.GetDatabase(mongodbDatabase);
            var collection = database.GetCollection<BsonDocument>(coll);
            try
            {
                collection.InsertOne(insertBson);
                client = null;
                return "suc";
            }
            catch (Exception e)
            {
                client = null;
                return "faild"; ;
            }

        }

        public string DeleteData(string mongodbConnStr, string mongodbDatabase, string coll, string deleteBson)
        {
            var client = new MongoClient(mongodbConnStr);
            var database = client.GetDatabase(mongodbDatabase);
            var collection = database.GetCollection<BsonDocument>(coll);
            try
            {
                var query = collection.Find(BsonDocument.Parse(deleteBson)).ToList();
                if (query.Count != 0)
                {
                    BsonDocument bson = BsonDocument.Parse(deleteBson);
                    collection.DeleteOne(bson);
                }
                client = null;
                return "suc";
            }
            catch (Exception e)
            {
                return e.ToString();
            }
        }

        public decimal GetTotalSysFeeByBlock(string mongodbConnStr, string mongodbDatabase, int blockindex)
        {
            var client = new MongoClient(mongodbConnStr);
            var database = client.GetDatabase(mongodbDatabase);
            var collection = database.GetCollection<BsonDocument>("block_sysfee");

            decimal totalSysFee = 0;
            var query = collection.Find("{'index':" + blockindex + "}").ToList();
            if (query.Count > 0)
            {
                totalSysFee = decimal.Parse(query[0]["totalSysfee"].AsString);
            }
            else {
                totalSysFee = -1;
            }
            client = null;
            return totalSysFee;
        }

        public string ReplaceData(string mongodbConnStr, string mongodbDatabase, string collName, string whereFliter, string replaceFliter)
        {
            var client = new MongoClient(mongodbConnStr);
            var database = client.GetDatabase(mongodbDatabase);
            var collection = database.GetCollection<BsonDocument>(collName);
            try
            {
                List<BsonDocument> query = collection.Find(whereFliter).ToList();
                if (query.Count > 0)
                {
                    collection.ReplaceOne(BsonDocument.Parse(whereFliter), BsonDocument.Parse(replaceFliter));
                    client = null;
                    return "suc";
                }
                else
                {
                    client = null;
                    return "no data";
                }
            }
            catch (Exception e)
            {
                client = null;
                return e.ToString();
            }
        }

        public string ReplaceOrInsertData(string mongodbConnStr, string mongodbDatabase, string collName, string whereFliter, string replaceFliter)
        {
            var client = new MongoClient(mongodbConnStr);
            var database = client.GetDatabase(mongodbDatabase);
            var collection = database.GetCollection<BsonDocument>(collName);
            try
            {
                List<BsonDocument> query = collection.Find(whereFliter).ToList();
                if (query.Count > 0)
                {
                    collection.ReplaceOne(BsonDocument.Parse(whereFliter), BsonDocument.Parse(replaceFliter));
                    client = null;
                    return "suc";
                }
                else
                {
                    BsonDocument bson = BsonDocument.Parse(replaceFliter);
                    collection.InsertOne(bson);
                    client = null;
                    return "suc";
                }
            }
            catch (Exception e)
            {
                client = null;
                return e.ToString();
            }
        }

        public JArray GetDataAtBlock(string mongodbConnStr, string mongodbDatabase, string coll, string findFliter)
        {
            var client = new MongoClient(mongodbConnStr);
            var database = client.GetDatabase(mongodbDatabase);
            var collection = database.GetCollection<BsonDocument>(coll);

            List<BsonDocument> query = collection.Find(BsonDocument.Parse(findFliter)).ToList();
            client = null;

            if (query.Count > 0)
            {
                var jsonWriterSettings = new JsonWriterSettings { OutputMode = JsonOutputMode.Strict };
                JArray JA = JArray.Parse(query.ToJson(jsonWriterSettings));
                foreach (JObject j in JA)
                {
                    j.Remove("_id");
                }
                return JA;
            }
            else { return new JArray(); }
        }

    }
}
