using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace NEL_Agency_API.lib
{
    public class RestHelper
    {

        public static string RestPost(string url, string data)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.ContentType = "application/json;charset=UTF-8";
            
            // 发送请求
            using (var writer = new StreamWriter(request.GetRequestStream()))
            {
                writer.Write(data);
                writer.Flush();
            }

            // 接收响应
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    StringBuilder sb = new StringBuilder();
                    string line = null;
                    while ((line = reader.ReadLine()) != null)
                    {
                        sb.Append(line);
                    }
                    return sb.ToString();
                }
            }
        }


        public static string RestGet(string Url, string param)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Url + (param == "" ? "" : "?") + param);
            request.Method = "GET";
            request.ContentType = "text/html;charset=UTF-8";

            // 发送请求+接收响应
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.GetEncoding("utf-8")))
                {
                    return reader.ReadToEnd();
                }
            }
        }
    }
}
