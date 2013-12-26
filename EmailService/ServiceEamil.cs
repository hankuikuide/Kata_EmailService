using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using log4net;
using System.Xml;
using System.IO;
using System.Threading;

namespace EmailService
{
    public partial class ServiceEamil : ServiceBase
    {

        //private static readonly ILog logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly ILog logger = LogManager.GetLogger("CRMeasyLog");

        public static int r_interval = 0;
        public static int s_interval = 0;
        //用于存放网关用户信息
        public static List<UserEntity> ulist = new List<UserEntity>();


        //从配置文件中读取下载路径
        string downloadpath = string.Empty;

        //用户下载目录
        string userdir = string.Empty;

        //用户发送目录
        string sendatt = string.Empty;

        public static DataSet ds = new DataSet();
        //发送相关变量-----------
        //接收路径
        string receive = string.Empty;
        //上传附件路径
        string upload = string.Empty;
        //接收相关变量-----------

        public ServiceEamil()
        {
            InitializeComponent();
            try
            {
                //加载用户配置信息
                XmlDocument xmldoc = new XmlDocument();
                logger.Debug("开始加载配置文件app.xml............" );
                xmldoc.Load("App.xml");
                XmlNodeList nlist = xmldoc.SelectNodes("//company");
                foreach (XmlElement element in nlist)
                {
                    UserEntity ue = new UserEntity();
                    ue.EmailAddress = element.GetElementsByTagName("emailaddress")[0].InnerText;
                    ue.Pop3Address = element.GetElementsByTagName("pop3address")[0].InnerText;
                    ue.Pop3Port = element.GetElementsByTagName("pop3port")[0].InnerText;
                    ue.SmtpAddress = element.GetElementsByTagName("smtpaddress")[0].InnerText;
                    ue.SmtpPort = element.GetElementsByTagName("smtpport")[0].InnerText;
                    ue.PassWord = element.GetElementsByTagName("password")[0].InnerText;
                    ulist.Add(ue);
                }

                //将配置文件读入dataset
                ds.ReadXml("App.xml");
                downloadpath = ds.Tables["global"].Rows[0]["DownloadPath"] + "/";
                logger.Debug("已读取下载路径：" + downloadpath.ToString());
                r_interval = Convert.ToInt32(ds.Tables["global"].Rows[0]["rInterval"]) * 60 * 1000;
                logger.Debug("已读取轮询间隔时间r_interval：" + r_interval);                
                s_interval = Convert.ToInt32(ds.Tables["global"].Rows[0]["sInterval"]) * 60 * 1000;
                logger.Debug("已读取轮询间隔时间s_interval：" + s_interval);                

            }
            catch (Exception err)
            {
                logger.Error("app.xml加载错误：" + err.Message);
            }
        }

        protected override void OnStart(string[] args)
        {
            logger.Debug("111111111111");

            this.run();
        }


        protected override void OnStop()
        {
        }

        public void run()
        {

            if (r_interval < 5)
            {
                logger.Warn("轮询间隔时间应设置为大于等于5，建议10");
                return;
            }
            if (s_interval < 5)
            {
                logger.Warn("轮询间隔时间应设置为大于等于5，建议10");
                return;
            }
            //删除日志文件
            FileInfo fi = new FileInfo("log.txt");
            if (fi.Exists)
            {
                fi.Delete();
            }
            logger.Debug("调用接收和发送函数");

            //调用接收和发送函数
            thSendWork();

        }

        private void thSendWork()
        {
            foreach (UserEntity ue in ulist)
            {
                userdir = downloadpath + ue.EmailAddress + "/";
                if (!Directory.Exists(userdir))
                {
                    Directory.CreateDirectory(userdir);
                }
                Worker wk = new Worker();
                logger.Debug("工作线程属性值初始化");
                wk.downloadpath = downloadpath;
                wk.userdir = userdir;
                wk.ue = ue;
                logger.Debug("启动线程");
                Thread th = new Thread(new ThreadStart(wk.DownloadEmail));
                th.IsBackground = true;
                th.Start();
            }
        }
    }
}
