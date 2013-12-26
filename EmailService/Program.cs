using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Reflection;
using System.IO;
using log4net;
using log4net.Config;

namespace EmailService
{
    static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        static void Main()
        {
            try
            {
                System.Uri uri = new Uri(typeof(string).Assembly.CodeBase);
                string RuntimePath = System.IO.Path.GetDirectoryName(uri.LocalPath);
                string strInstallUtilPath = System.IO.Path.Combine(RuntimePath, "InstallUtil.exe");
                foreach (string arg in System.Environment.GetCommandLineArgs())
                {
                    Console.WriteLine(arg);
                    if (arg == "/install")
                    {
                        return;
                    }
                    else if (arg == "/uninstall")
                    {
                        return;
                    }
                    else if (arg == "/client")
                    {
                        return;
                    }
                    else if (arg == "/debug")
                    {
                        ServiceEamil service = new ServiceEamil();
                        service.run();
                        System.Threading.Thread.Sleep(1000 * 600);
                        return;
                    }
                }
            }
            catch (Exception ext)
            {
                Console.WriteLine(ext.ToString());
                return;
            }
            // 运行服务对象
            ServiceBase.Run(new ServiceEamil());
        }
    }
}
