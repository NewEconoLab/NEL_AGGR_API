using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NEL_Agency_API.Controllers
{
    public class Logger
    {
        public static void DebugLog(string msg)
        {
            string FilePath = "/opt/DebugLog.txt";
            try
            {
                if (File.Exists(FilePath))
                {
                    using (StreamWriter tw = File.AppendText(FilePath))
                    {
                        tw.WriteLine(msg.ToString());
                    }
                }
                else
                {
                    TextWriter tw = new StreamWriter(FilePath);
                    tw.WriteLine(msg.ToString());
                    tw.Flush();
                    tw.Close();
                    tw = null;
                }
            }
            catch (Exception)
            {
                //Console.ReadKey();
            }

        }

        public static void ErrorLog(Exception ex)
        {
            string FilePath = "/opt/ErrorLog.txt";

            System.Text.StringBuilder msg = new System.Text.StringBuilder();
            msg.Append("*************************************** \n");
            msg.AppendFormat(" 异常发生时间： {0} \n", DateTime.Now);
            msg.AppendFormat(" 异常类型： {0} \n", ex.HResult);
            msg.AppendFormat(" 导致当前异常的 Exception 实例： {0} \n", ex.InnerException);
            msg.AppendFormat(" 导致异常的应用程序或对象的名称： {0} \n", ex.Source);
            msg.AppendFormat(" 引发异常的方法： {0} \n", ex.TargetSite);
            msg.AppendFormat(" 异常堆栈信息： {0} \n", ex.StackTrace);
            msg.AppendFormat(" 异常消息： {0} \n", ex.Message);
            msg.Append("***************************************");

            try
            {
                if (File.Exists(FilePath))
                {
                    using (StreamWriter tw = File.AppendText(FilePath))
                    {
                        tw.WriteLine(msg.ToString());
                    }
                }
                else
                {
                    TextWriter tw = new StreamWriter(FilePath);
                    tw.WriteLine(msg.ToString());
                    tw.Flush();
                    tw.Close();
                    tw = null;
                }
            }
            catch (Exception)
            {
                //Console.ReadKey();
            }

        }


    }
}
