using NEL_Agency_API.lib;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NEL_Agency_API.Controllers
{
    public class AuctionRecharge
    {
        public string Notify_mongodbConnStr { set; get; }
        public string Notify_mongodbDatabase { set; get; }
        public mongoHelper mh { set; get; }
        public string nelJsonRPCUrl { get; set; }
        public string rechargeCollection { get; set; }


        /// <summary>
        /// 发送recharge&transfer交易
        /// 
        /// </summary>
        /// <param name="txhex1"></param>
        /// <param name="txhex2"></param>
        /// <returns></returns>
        public JArray rechargeAndTransfer(string txhex1, string txhex2)
        {
            // 发送第一笔交易
            JObject res = PostTx("sendrawtransaction", txhex1);
            string result = Convert.ToString(res["sendrawtransactionresult"]);
            string txid = Convert.ToString(res["txid"]);
            if (result != "true" && txid == "")
            {
                // 第一笔交易未发送成功
                res = new JObject();
                res.Add("errCode", TxState.TX_FAILD.code);
                res.Add("errMessage", "send tx1 failed");
                res.Add("txid", txid);
            }
            else
            {
                // 保存交易状态
                saveTxState(txid, TxState.TX_WAITING);
                // 异步发送第二笔交易
                syncSendTx2(txhex2, txid);
                res = new JObject();
                res.Add("errCode", TxState.TX_SECC.code);
                res.Add("errMessage", TxState.TX_SECC.codeMessage);
                res.Add("txid", txid);
            }
            return new JArray() { res };
        }
        private void saveTxState(string txid1, TxStateCode txid1Code, bool isReplace = false)
        {
            saveTxState(txid1, txid1Code, "", TxState.TX_NULL, isReplace);
        }
        private void saveTxState(string txid1, TxStateCode txid1Code, string txid2, TxStateCode txid2Code, bool isReplace = false)
        {
            JObject param = new JObject();
            param.Add("txid1", txid1);
            param.Add("txid1Code", txid1Code.code);
            param.Add("txid1CodeMessage", txid1Code.codeMessage);
            param.Add("txid2", txid2);
            param.Add("txid2Code", txid2Code.code);
            param.Add("txid2CodeMessage", txid2Code.codeMessage);
            if (!isReplace)
            {
                mh.InsertOneData(Notify_mongodbConnStr, Notify_mongodbDatabase, rechargeCollection, param.ToString());
            }
            else
            {
                JObject filter = new JObject();
                filter.Add("txid1", txid1);
                mh.ReplaceData(Notify_mongodbConnStr, Notify_mongodbDatabase, rechargeCollection, filter.ToString(), param.ToString());
            }

        }
        
        private async void syncSendTx2(string txhex2, string txid1)
        {
            await Task.Run(() => {
                JObject res = null;
                // 第一笔交易发送成功则查询该笔交易是否入链
                if (!checkTxHasInBlock(txid1))
                {
                    // 第一笔未成功入链
                    saveTxState(txid1, TxState.TX_FAILD, true);
                }
                else
                {
                    // 第一笔成功入链则发送第二笔
                    res = PostTx("sendrawtransaction", txhex2);
                    string result2 = Convert.ToString(res["sendrawtransactionresult"]);
                    string txid2 = Convert.ToString(res["txid"]);
                    if (result2 != "true" && txid2 == null)
                    {
                        // 第二笔未发送成功
                        saveTxState(txid1, TxState.TX_SECC, txid2, TxState.TX_INTERRUPT, true);
                    }
                    else
                    {
                        // 第二笔发送成功则坚持该笔交易是否入链
                        if (!checkTxHasInBlock(txid2))
                        {
                            // 第二笔未成功入链
                            saveTxState(txid1, TxState.TX_SECC, txid2, TxState.TX_INTERRUPT, true);
                        }
                        else
                        {
                            // 第二笔成功入链
                            saveTxState(txid1, TxState.TX_SECC, txid2, TxState.TX_SECC, true);
                        }
                    }
                }
            });
        }

        private bool checkTxHasInBlock(string txid)
        {
            bool flag = false;
            int curHeight = getBlockCount();
            JObject res = null;
            do
            {
                res = PostTx("getrawtransaction", txid);
                if (res != null)
                {
                    flag = true;
                    break;
                }
                Thread.Sleep(200);
            } while (getBlockCount() <= curHeight + 2);
            return flag;
        }
        private int getBlockCount()
        {
            JObject res = PostTx("getblockcount", "{}");
            int blockcount = int.Parse(Convert.ToString(res["blockcount"]));
            return blockcount;
        }
        private JObject PostTx(string method, string data)
        {
            byte[] postdata = null;
            string url = httpHelper.MakeRpcUrlPost(nelJsonRPCUrl, method, out postdata, new MyJson.JsonNode_ValueString(data));
            //JObject res = (JObject)(((JArray)(JObject.Parse(httpHelper.HttpPost(url, postdata))["result"]))[0]);
            string ss = httpHelper.HttpPost(url, postdata);
            if (JObject.Parse(ss)["result"] == null)
            {
                return null;
            }
            JObject res = (JObject)(((JArray)(JObject.Parse(ss)["result"]))[0]);
            return res;
        }

        /// <summary>
        ///  查询Recharge&Transfer交易
        ///  
        /// </summary>
        /// <param name="txid"></param>
        /// <returns></returns>
        public JArray getRechargeAndTransfer(string txid)
        {
            JObject res = new JObject();

            JObject filter = new JObject();
            filter.Add("txid1", txid);
            JArray result = mh.GetData(Notify_mongodbConnStr, Notify_mongodbDatabase, rechargeCollection, filter.ToString());
            if (result != null && result.Count > 0)
            {
                string txid1Code = Convert.ToString(result[0]["txid1Code"]);
                string txid2Code = Convert.ToString(result[0]["txid2Code"]);
                string txid1CodeMessage = Convert.ToString(result[0]["txid1CodeMessage"]);
                string txid2CodeMessage = Convert.ToString(result[0]["txid2CodeMessage"]);
                if (txid1Code != TxState.TX_SECC.code)
                {
                    res.Add("errCode", txid1Code);
                    res.Add("errMessage", txid1CodeMessage);
                    res.Add("txid", "");
                }
                else
                {
                    res.Add("errCode", txid2Code);
                    res.Add("errMessage", txid2CodeMessage);
                    res.Add("txid", "");
                }
            }
            else
            {
                res.Add("errCode", TxState.TX_NOTFIND.code);
                res.Add("errMessage", TxState.TX_NOTFIND.codeMessage);
                res.Add("txid", "");
            }
            return new JArray() { res };
        }
    }
    class TxState
    {
        public static TxStateCode TX_SECC = new TxStateCode { code = "0000", codeMessage = "成功" };
        public static TxStateCode TX_FAILD = new TxStateCode { code = "3001", codeMessage = "失败" };
        public static TxStateCode TX_INTERRUPT = new TxStateCode { code = "3002", codeMessage = "中断" };
        public static TxStateCode TX_WAITING = new TxStateCode { code = "3003", codeMessage = "等待" };
        public static TxStateCode TX_NOTFIND = new TxStateCode { code = "3004", codeMessage = "Not find data" };
        public static TxStateCode TX_NULL = new TxStateCode { code = "", codeMessage = "" };
    }
    class TxStateCode
    {
        public string code { get; set; }
        public string codeMessage { get; set; }
    }
}
