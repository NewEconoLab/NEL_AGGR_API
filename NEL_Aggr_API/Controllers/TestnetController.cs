﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NEL_Agency_API.RPC;
using NEL_Agency_API.lib;
using System.IO;

namespace NEL_Agency_API.Controllers
{
    //[RpcRoute("api/[controller]")]
    [Route("api/[controller]")]
    public class TestnetController : Controller
    {
        Api api = new Api("testnet");

        [HttpGet]
        public JsonResult Get(string @jsonrpc, string @method, string @params, long @id)
        {

            try
            {
                JsonRPCrequest req = new JsonRPCrequest
                {
                    jsonrpc = @jsonrpc,
                    method = @method,
                    @params = JsonConvert.DeserializeObject<object[]>(JsonConvert.SerializeObject(JArray.Parse(@params))),
                    id = @id
                };

                string ipAddr = Request.HttpContext.Connection.RemoteIpAddress.ToString();
                return Json(api.getRes(req,ipAddr));
            }
            catch (Exception e)
            {
                JsonPRCresponse_Error resE = new JsonPRCresponse_Error(0, -100, "Parameter Error", e.Message);

                return Json(resE);

            }
        }

        [HttpPost]
        public async Task<JsonResult> Post()
        {
            Logger.DebugLog("TestNet.post start");
            try
            {
                var ctype = HttpContext.Request.ContentType;
                LitServer.FormData form = null;
                JsonRPCrequest req = null;
                if (ctype == "application/x-www-form-urlencoded" ||
                     (ctype.IndexOf("multipart/form-data;") == 0))
                {
                    form = await LitServer.FormData.FromRequest(HttpContext.Request);
                    var _jsonrpc = form.mapParams["jsonrpc"];
                    var _id = long.Parse(form.mapParams["id"]);
                    var _method = form.mapParams["method"];
                    var _strparams = form.mapParams["params"];
                    var _params = JArray.Parse(_strparams);
                    req = new JsonRPCrequest
                    {
                        jsonrpc = _jsonrpc,
                        method = _method,
                        @params = JsonConvert.DeserializeObject<object[]>(JsonConvert.SerializeObject(_params)),
                        id = _id
                    };
                }
                else// if (ctype == "application/json") 其他所有请求方式都这样取好了
                {
                    Logger.DebugLog("TestNet.post application/json");
                    var text = await LitServer.FormData.GetStringFromRequest(HttpContext.Request);
                    req = JsonConvert.DeserializeObject<JsonRPCrequest>(text);
                }

                string ipAddr = Request.HttpContext.Connection.RemoteIpAddress.ToString();
                Logger.DebugLog("TestNet.post call api.getRes");
                return Json(api.getRes(req, ipAddr));
            }
            catch (Exception e)
            {
                return Json("{}");
            }

        }

    }
}