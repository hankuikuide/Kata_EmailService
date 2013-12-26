using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Drawing;
using System.Net.Mail;
using log4net;

namespace EmailService
{
    class Worker
    {
        private static readonly ILog logger = LogManager.GetLogger("CRMeasyLog");

        //临时文件(*.eml)存放路径
        string temp = string.Empty;

        //附件的下载存放路径
        string att = string.Empty;
        //邮件内容路径，内容以HTML格式存放
        string html = string.Empty;


        //初始化标志位，作用：如果上次的下载动作还没有做完，下次的轮询将不会进行
        static bool status = false;
        //用于存放下载队列
        List<EmailEntity> ReceiveEmails = new List<EmailEntity>();
        //用于存放需要发送的邮件队列
        List<EmailEntity> SendMails = new List<EmailEntity>();

        string EmailTo = string.Empty;
        //初始化数据访问类
        DBHelper db = null;
        DBHelper db2 = null;

        public string downloadpath = string.Empty;

        //需要发送的附件的路径
        public string sendatt = string.Empty;
        public string userdir = string.Empty;
        public UserEntity ue = null;
        //邮件下载
        public void DownloadEmail()
        {
            while (true)
            {
                try
                {
                    GetEmls();
                }
                catch (Exception e)
                {
                    logger.Debug("工作线程属性值初始化");
                }

                Thread.Sleep(ServiceEamil.r_interval);
            }
        }
        //邮件发送
        public void SendEmail()
        {
            while (true)
            {
                try
                {
                    db2 = new DBHelper();
                    //从数据库中获取需要发送的信息
                    SendMails = db2.GetDataFromDB();
                    //执行发送动作
                    DoSendMail(SendMails);
                    //发送完毕后更改信息的状态为已处理
                    db2.SetDataFinish(SendMails);
                    //释放对象
                    db2 = null;
                }
                catch (Exception e)
                {
                    //LogHelper.PrintLog(Loggerlevel.ERROR, "SendEmail", "SendEmail", LoggerMark.Business, e.ToString());
                }


                //线程休眠一段时间
                Thread.Sleep(ServiceEamil.s_interval);
            }
        }
        /// <summary>
        /// 获取所有邮件的Eml格式文本
        /// </summary>
        /// <returns>Eml列表</returns>
        public void GetEmls()
        {
            //检查标志位，如果程序处于忙碌状态，则放弃本次操作，直到上次的动作做完。
            if (status)
            {
                return;
            }
            else
            {
                status = true;
            }
            if (ReceiveEmails.Count > 0)
            {
                ReceiveEmails.Clear();
            }
            //临时文件夹，用于存放eml临时文件
            temp = userdir + "temp/";
            if (!Directory.Exists(temp))
            {
                Directory.CreateDirectory(temp);
            }


            //建立和POP3的TCP连接
            TcpClient Server = new TcpClient(ue.Pop3Address, Convert.ToInt32(ue.Pop3Port));//prcmail.dyndns.org

            string RecString;//用于保存每一次返回值
            string Data;
            byte[] szData;
            int EmailCount = 0;
            string[] tempCount;

            //定义回车
            string CRLF = "\r\n";

            StreamReader RdStrm;
            try
            {
                //获取客户机和服务器会话的数据流
                NetworkStream NetStrm = Server.GetStream();
                RdStrm = new StreamReader(Server.GetStream(), Encoding.GetEncoding("gb2312"));
                RecString = RdStrm.ReadLine();

                //传送账户名称
                Data = "USER " + ue.EmailAddress + CRLF;//dl.kong@ctint.com
                //对数据进行编码
                szData = System.Text.Encoding.UTF8.GetBytes(Data.ToCharArray());
                //向服务器传送账户
                NetStrm.Write(szData, 0, szData.Length);
                //接收服务器反馈数据
                RecString = RdStrm.ReadLine();

                //定义账户对应的口令的命令
                Data = "PASS " + ue.PassWord + CRLF;//881030dale#
                //对数据进行编码
                szData = System.Text.Encoding.UTF8.GetBytes(Data.ToCharArray());
                //向服务器传送账户的口令
                NetStrm.Write(szData, 0, szData.Length);
                //接收服务器反馈数据
                RecString = RdStrm.ReadLine();


                //邮件接收部分

                Data = "STAT" + CRLF;
                szData = System.Text.Encoding.UTF8.GetBytes(Data.ToCharArray());
                //向服务器发送获取邮件总数的命令
                NetStrm.Write(szData, 0, szData.Length);
                tempCount = RdStrm.ReadLine().Split(" ".ToCharArray());
                EmailCount = Convert.ToInt32(tempCount[1]);
                //用于临时存放单个email的eml文本
                StringBuilder sb = new StringBuilder();

                for (int i = 1; i <= EmailCount; i++)
                {
                    //开始下载
                    bool flag = true;
                    string[] arrTemp;
                    string emlid = string.Empty;
                    //发送下载第几封邮件
                    Data = "RETR " + i.ToString() + CRLF;
                    szData = System.Text.Encoding.UTF8.GetBytes(Data.ToCharArray());
                    NetStrm.Write(szData, 0, szData.Length);
                    //过虑不需要的信息
                    RdStrm.ReadLine();
                    //接受返回值判断是否为点
                    while ((RecString = RdStrm.ReadLine()) != ".")
                    {
                        sb.AppendLine(RecString);
                        if (flag)
                        {
                            arrTemp = RecString.Split(":".ToCharArray());
                            if (arrTemp[0] == "Message-ID" || arrTemp[0] == "Message-Id")
                            {
                                emlid = arrTemp[1].Replace("<", "").Replace(">", "");
                                flag = false;
                            }
                        }
                    }
                    //将eml文件保存到本地
                    SaveEmailToFile(temp, sb.ToString(), System.Guid.NewGuid().ToString());
                    //清空临时变量
                    sb.Remove(0, sb.Length);


                    //2011-7-11
                    //lcc
                    //将邮件标记为删除---下完一个删除一个
                    Data = "DELE " + i + CRLF;
                    szData = System.Text.Encoding.UTF8.GetBytes(Data.ToCharArray());
                    //向服务器发送获取邮件总数的命令
                    NetStrm.Write(szData, 0, szData.Length);
                    RdStrm.ReadLine();

                }

                //2011-7-11
                //lcc
                // 将邮件标记为删除
                for (int i = 1; i <= EmailCount; i++)
                {
                    Data = "DELE " + i.ToString() + CRLF;
                    szData = System.Text.Encoding.UTF8.GetBytes(Data.ToCharArray());
                    //向服务器发送获取邮件总数的命令
                    NetStrm.Write(szData, 0, szData.Length);
                    RdStrm.ReadLine();
                }


                //richTextBox1.AppendText("EML文件下载完毕。\n");
                //定义退出命令
                Data = "QUIT" + CRLF;
                szData = System.Text.Encoding.UTF8.GetBytes(Data.ToCharArray());
                //向服务器发送接收邮件的命令
                NetStrm.Write(szData, 0, szData.Length);

                //最后将反馈的MIME信息和quit命令响应信息一起接收
                while ((RecString = RdStrm.ReadLine()) != null)
                {
                    //richTextBox1.AppendText("POP3断开连接。" + "\n");
                }
                NetStrm.Close();
                RdStrm.Close();

                //将数据提交到数据库中
                db = new DBHelper();
                db.Email_GatewayAddress = ue.EmailAddress;
                db.CommitDataToDB(ReceiveEmails);
                db = null;
            }
            catch (InvalidOperationException err)
            {
                //LogPrint.Print("GetEmls()", err.Message);
                //LogHelper.PrintLog(Loggerlevel.ERROR, "GetEmls", "GetEmls", LoggerMark.Business, err.ToString());
            }

            //将标志位设置为空闲
            status = false;
        }

        /// <summary>
        /// 保存邮件内容到本地
        /// </summary>
        /// <param name="eml">文本内容</param>
        /// <param name="name">文件名</param>
        public void SaveEmailToFile(string temp, string content, string emailid)
        {

            byte[] c = System.Text.Encoding.UTF8.GetBytes(content);
            //邮件内容存储到服务器
            FileStream fs = new FileStream(temp + emailid + ".eml", FileMode.Create, FileAccess.ReadWrite);
            //这里可使用CreateNew抛出异常重新命名新的文件名，防止重复文件名产生覆盖
            fs.Write(c, 0, c.Length); //将字节数组存储到文件流中)
            fs.Flush();
            fs.Close();

            GetEmails(temp, emailid);

        }

        /// <summary>
        /// 获取邮件信息，并将信息存放在临时邮件列表中
        /// </summary>
        /// <param name="emls"></param>
        /// <returns></returns>
        public void GetEmails(string temp, string emailid)
        {

            CDO.Message oMsg = new CDO.Message();
            ADODB.Stream stm = null;

            //读取EML文件到CDO.MESSAGE，做分析的话，实际是用了下面的部分

            try
            {
                stm = new ADODB.Stream();
                stm.Open(System.Reflection.Missing.Value,
                        ADODB.ConnectModeEnum.adModeUnknown,
                        ADODB.StreamOpenOptionsEnum.adOpenStreamUnspecified,
                        "", "");
                stm.Type = ADODB.StreamTypeEnum.adTypeBinary;//二进制方式读入
                stm.LoadFromFile(temp + emailid + ".eml"); //将EML读入数据流

                oMsg.DataSource.OpenObject(stm, "_stream"); //将EML数据流载入到CDO.Message，要做解析的话，后面就可以了。  

            }
            catch (Exception ex)
            {
                // LogPrint.Print("GetEmails(string temp, string emailid)", ex.Message);
                //LogHelper.PrintLog(Loggerlevel.ERROR, "GetEmails", "GetEmails", LoggerMark.Business, ex.ToString());
            }
            finally
            {
                stm.Close();
            }
            #region
            EmailEntity email = new EmailEntity();
            ////发件人
            string _From = oMsg.From;
            email.From = _From;
            ////收件人
            string _To = oMsg.To;
            email.To = _To;
            //抄送
            string _Cc = oMsg.CC;
            email.Cc = _Cc;
            //邮件主题
            string _Subject = oMsg.Subject;
            email.Subject = _Subject;
            //收件日期
            DateTime _Time = oMsg.ReceivedTime;
            email.Date = _Time;

            //email编号
            email.EmailId = emailid;
            #endregion

            string pattern = @"\<.*\>";
            Regex reg = new Regex(pattern, RegexOptions.IgnoreCase);
            Match m = reg.Match(_From);
            //发件人
            _From = m.Value.Replace("<", "").Replace(">", "");


            //读出来用来替换（base64换成url）
            StreamReader sr = new StreamReader(temp + emailid + ".eml", Encoding.UTF8);

            string UserPath = userdir + _From + "/" + emailid + "/";

            html = UserPath + "html/";

            if (!Directory.Exists(html))
            {
                Directory.CreateDirectory(html);
            }
            if (oMsg.HTMLBody == "")
            {
                //纯文本    
                byte[] b_html;
                //
                b_html = System.Text.Encoding.UTF8.GetBytes(oMsg.TextBody);
                FileStream fsHtml = new FileStream(html + emailid + ".html", FileMode.Create);
                fsHtml.Write(b_html, 0, b_html.Length);
                fsHtml.Flush();
                fsHtml.Close();

                email.Content = (ServiceEamil.ds.Tables["global"].Rows[0]["ServerUrl"] + html + emailid + ".html").Replace(downloadpath, "");
            }
            else
            {
                //html格式
                byte[] b_html;
                //得到一个图文并茂的HTML
                b_html = System.Text.Encoding.UTF8.GetBytes(ImageInsertHtml(oMsg.HTMLBody, sr.ReadToEnd(), html));
                FileStream fsHtml = new FileStream(html + emailid + ".html", FileMode.Create);
                fsHtml.Write(b_html, 0, b_html.Length);
                fsHtml.Flush();
                fsHtml.Close();

                email.Content = (ServiceEamil.ds.Tables["global"].Rows[0]["ServerUrl"] + html + emailid + ".html").Replace(downloadpath, "");

            }

            //附件路径
            att = UserPath + "att/";
            if (!Directory.Exists(att))
            {
                Directory.CreateDirectory(att);
            }
            //保存附件到本地
            for (int i = 1; i <= oMsg.Attachments.Count; i++)
            {
                try
                {
                    string _filename = oMsg.Attachments[i].FileName;
                    if (oMsg.Attachments[i].ContentTransferEncoding == "base64")
                    {
                        string base64 = oMsg.Attachments[i].GetEncodedContentStream().ReadText(int.MaxValue);
                        byte[] str = Convert.FromBase64String(base64);
                        FileStream fs = new FileStream(att + _filename, FileMode.Create);
                        fs.Write(str, 0, str.Length);
                        fs.Flush();
                        fs.Close();
                        if (email.Attachment == "")
                        {
                            email.Attachment = _filename;
                            email.AttachmentUrl = (ServiceEamil.ds.Tables["global"].Rows[0]["ServerUrl"] + att + _filename).Replace(downloadpath, "");
                        }
                        else
                        {
                            email.Attachment = email.Attachment + ";" + _filename;
                            email.AttachmentUrl = email.AttachmentUrl + ";" + (ServiceEamil.ds.Tables["global"].Rows[0]["ServerUrl"] + att + _filename).Replace(downloadpath, "");
                        }
                    }
                    if (oMsg.Attachments[i].ContentTransferEncoding == "quoted-printable")
                    {
                        string QPstring = oMsg.Attachments[i].GetEncodedContentStream().ReadText(int.MaxValue);
                        string qp = QPDecode(QPstring);
                        byte[] str = System.Text.Encoding.UTF8.GetBytes(qp);
                        FileStream fs = new FileStream(att + _filename, FileMode.Create);
                        fs.Write(str, 0, str.Length);
                        fs.Flush();
                        fs.Close();
                        if (email.Attachment == "")
                        {
                            email.Attachment = _filename;
                            email.AttachmentUrl = (ServiceEamil.ds.Tables["global"].Rows[0]["ServerUrl"] + att + _filename).Replace(downloadpath, "");
                        }
                        else
                        {
                            email.Attachment = email.Attachment + ";" + _filename;
                            email.AttachmentUrl = email.AttachmentUrl + ";" + (ServiceEamil.ds.Tables["global"].Rows[0]["ServerUrl"] + att + _filename).Replace(downloadpath, "");
                        }
                    }
                }
                catch (Exception err)
                {
                    //LogPrint.Print("GetEmails(string temp, string emailid)", err.Message);
                    //LogHelper.PrintLog(Loggerlevel.ERROR, "GetEmails", "GetEmails", LoggerMark.Business, err.ToString());

                }
            }
            ReceiveEmails.Add(email);
        }
        //base64编码的文本 转为    图片
        private void Base64StringToImage(string inputStr, string name)
        {
            try
            {
                byte[] arr = Convert.FromBase64String(inputStr);
                MemoryStream ms = new MemoryStream(arr);
                Bitmap bmp = new Bitmap(ms);
                if (name.Contains(".jpg"))
                    bmp.Save(att + name, System.Drawing.Imaging.ImageFormat.Jpeg);
                if (name.Contains(".png"))
                    bmp.Save(att + name, System.Drawing.Imaging.ImageFormat.Png);
                if (name.Contains(".bmp"))
                    bmp.Save(att + name, System.Drawing.Imaging.ImageFormat.Bmp);
                if (name.Contains(".gif"))
                    bmp.Save(att + name, System.Drawing.Imaging.ImageFormat.Gif);
                else
                    bmp.Save(att + name, System.Drawing.Imaging.ImageFormat.Png);
                ms.Close();
            }
            catch (Exception ex)
            {
                // LogPrint.Print("Base64StringToImage(string inputStr, string name)", ex.Message);
                //LogHelper.PrintLog(Loggerlevel.ERROR, "Base64StringToImage", "Base64StringToImage", LoggerMark.Business, ex.ToString());
            }
        }

        /// <summary>
        /// 将quoted-printable解码为字符串
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        private string QPDecode(string source)
        {
            source = source.Replace("=\r\n", "");
            int len = source.Length;
            string dest = string.Empty;
            int i = 0;
            while (i < len)
            {
                string temp = source.Substring(i, 1);
                if (temp == "=")
                {
                    int code = Convert.ToInt32(source.Substring(i + 1, 2), 16);
                    if (Convert.ToInt32(code.ToString(), 10) < 127)
                    {
                        dest += ((char)code).ToString();
                        i = i + 3;
                    }
                    else
                    {
                        dest += System.Text.Encoding.Default.GetString(new byte[] { Convert.ToByte(source.Substring(i + 1, 2), 16), Convert.ToByte(source.Substring(i + 4, 2), 16) });
                        i = i + 6;
                    }
                }
                else
                {
                    dest += temp;
                    i++;
                }
            }
            return dest;
        }

        /// <summary>
        /// 根据Html找到所有内嵌的图像
        /// </summary>
        /// <param name="html"></param>
        /// <param name="eml"></param>
        public string ImageInsertHtml(string html, string eml, string path)
        {
            try
            {
                string[] arrTemp;
                //保存邮件（html格式）内容中所有图片的ID
                List<string> img = new List<string>();
                string ImageID = string.Empty;
                Regex re = new Regex(@"<img[^>]*src=([＇""]?)(?<img>[^＇""\s>]*)\1[^>]*>", RegexOptions.IgnoreCase);
                MatchCollection matches = re.Matches(html);

                foreach (Match mh in matches)
                {
                    //找出邮件内容（html格式）里面的图片ID用于匹配eml文件里面的图片编码ID
                    string _tmpImageUrl = mh.Groups["img"].Value;//src里面的路径
                    arrTemp = _tmpImageUrl.Split(":".ToCharArray());
                    if (arrTemp.Count() == 2)
                    {
                        ImageID = arrTemp[1];
                        img.Add(ImageID);
                    }
                }

                if (matches.Count > 0 && img.Count > 0)
                {
                    //将图片编码转换成图片保存到本地
                    EmlToBase64String(eml, img, path);

                    for (int i = 0; i < matches.Count; i++)
                    {
                        //html = html.Replace("cid:" + img[i], img[i] + ".png");
                        //2011-5-11 这里图片的格式是写死的
                        if (img[i].Contains(".jpg"))
                        {
                            html = html.Replace("cid:" + img[i], img[i] + ".jpg");
                            continue;
                        }
                        if (img[i].Contains(".bmp"))
                        {
                            html = html.Replace("cid:" + img[i], img[i] + ".bmp");
                            continue;
                        }
                        if (img[i].Contains(".gif"))
                        {
                            html = html.Replace("cid:" + img[i], img[i] + ".gif");
                            continue;
                        }
                        else
                            html = html.Replace("cid:" + img[i], img[i] + ".png");

                    }
                }
                else
                {
                    return html;
                }
            }
            catch (Exception err)
            {
                //LogPrint.Print("ImageInsertHtml()", err.Message);
                //LogHelper.PrintLog(Loggerlevel.ERROR, "ImageInsertHtml", "ImageInsertHtml", LoggerMark.Business, err.ToString());

            }
            return html;

        }
        /// <summary>
        /// 根据Eml和Img标签找到所有图像的Base64编码
        /// </summary>
        /// <param name="eml"></param>
        /// <param name="img"></param>
        public void EmlToBase64String(string eml, List<string> img, string path)
        {

            List<string> base64 = new List<string>();
            for (int i = 0; i < img.Count; i++)
            {
                int index = eml.IndexOf("<" + img[i] + ">") + ("<" + img[i] + ">").Length;
                string temp = eml.Substring(0, index);
                temp = eml.Replace(temp, "");
                try
                {
                    int finish = temp.IndexOf("--");

                    temp = temp.Substring(0, finish);
                }
                catch (Exception err)
                {
                    //LogPrint.Print("EmlToBase64String(string eml, List<string> img, string path)", err.Message);
                    //LogHelper.PrintLog(Loggerlevel.ERROR, "EmlToBase64String", "EmlToBase64String", LoggerMark.Business, err.ToString() + "EmlToBase64String error:");

                    continue;
                }
                base64.Add(temp);

            }
            //开始转换（将base64转换成图片）
            Base64StringToImage(base64, img, path);
        }

        /// <summary>
        /// 将Base64编码转成图像,并存放在本地
        /// </summary>
        /// <param name="base64"></param>
        /// <param name="imgname"></param>
        private void Base64StringToImage(List<string> base64, List<string> imgname, string path)
        {
            for (int i = 0; i < base64.Count; i++)
            {
                try
                {
                    //将base64转换成二进制在转成图片保存到本地
                    byte[] arr = Convert.FromBase64String(base64[i]);
                    MemoryStream ms = new MemoryStream(arr);
                    Bitmap bmp = new Bitmap(ms);

                    //if (imgname[i].Contains(".png"))
                    //    bmp.Save(path + imgname[i] + ".png", System.Drawing.Imaging.ImageFormat.Png);
                    //if (imgname[i].Contains(".jpg"))
                    //    bmp.Save(path + imgname[i] + ".jpg", System.Drawing.Imaging.ImageFormat.Jpeg);
                    //if (imgname[i].Contains(".bmp"))
                    //    bmp.Save(path + imgname[i] + ".bmp", System.Drawing.Imaging.ImageFormat.Bmp);
                    //if (imgname[i].Contains(".gif"))
                    //    bmp.Save(path + imgname[i] + ".gif", System.Drawing.Imaging.ImageFormat.Gif);
                    //else
                    //    bmp.Save(path + imgname[i] + ".png", System.Drawing.Imaging.ImageFormat.Png);

                    //2011-5-11 修改原因， 重复保存
                    if (imgname[i].Contains(".jpg"))
                    {
                        bmp.Save(path + imgname[i] + ".jpg", System.Drawing.Imaging.ImageFormat.Jpeg);
                        continue;
                    }
                    if (imgname[i].Contains(".bmp"))
                    {
                        bmp.Save(path + imgname[i] + ".bmp", System.Drawing.Imaging.ImageFormat.Bmp);
                        continue;
                    }
                    if (imgname[i].Contains(".gif"))
                    {
                        bmp.Save(path + imgname[i] + ".gif", System.Drawing.Imaging.ImageFormat.Gif);
                        continue;
                    }
                    else
                        bmp.Save(path + imgname[i] + ".png", System.Drawing.Imaging.ImageFormat.Png);

                    ms.Close();
                }
                catch (Exception ex)
                {
                    //LogPrint.Print("Base64StringToImage()", ex.Message);
                    //LogHelper.PrintLog(Loggerlevel.ERROR, "Base64StringToImage", "Base64StringToImage", LoggerMark.Business, ex.ToString());

                    continue;
                }

            }
        }




        public void DoSendMail(List<EmailEntity> list)
        {

            foreach (EmailEntity ee in list)
            {
                try
                {
                    //实例化一条新邮件
                    MailMessage m = new MailMessage();
                    m.From = new MailAddress(ee.From);
                    string[] arrTo = ee.To.Split(";".ToCharArray());
                    for (int i = 0; i < arrTo.Count(); i++)
                    {
                        //向邮件中添加收件人
                        m.To.Add(new MailAddress(arrTo[i]));
                    }
                    if (ee.Cc.ToString() != "Null")
                    {
                        string[] arrCc = ee.Cc.Split(";".ToCharArray());

                        for (int i = 0; i < arrCc.Count(); i++)
                        {
                            //向邮件中添加抄送地址
                            m.CC.Add(new MailAddress(arrCc[i]));
                        }
                    }

                    //向邮件中添加附件
                    string[] arrAtt = null;
                    if (ee.Attachment != "Null")
                    {
                        arrAtt = ee.Attachment.Split(";".ToCharArray());
                        for (int i = 0; i < arrAtt.Count(); i++)
                        {
                            //添加附件（由路径可以得到附件）
                            //m.Attachments.Add(new Attachment(sendatt + arrAtt[i]));william2012-03-1注释.
                            //william 2012-03-1新增

                            File.Delete(sendatt + arrAtt[i]);
                            File.Copy(ee.AttachmentUrl, sendatt + arrAtt[i]);
                            //string aa = "C:/CTIL/webmail/upload/lv_chen_@126.com/001.txt";
                            //File.Copy(aa, sendatt + arrAtt[i],true);

                            m.Attachments.Add(new Attachment(sendatt + arrAtt[i]));
                            m.Attachments[i].Name = arrAtt[i].ToString();
                            //m.Attachments[i].ContentId =arrAtt[i];
                            m.Attachments[i].ContentDisposition.Inline = true;
                            m.Attachments[i].NameEncoding = m.SubjectEncoding = m.BodyEncoding = Encoding.UTF8;
                            //LogHelper.PrintLog(Loggerlevel.ERROR, "DoSendMail", "DoSendMail", LoggerMark.Business, "添加附件：" + sendatt + arrAtt[i]);

                        }
                    }
                    m.Subject = ee.Subject;
                    m.IsBodyHtml = true;


                    m.Body = ee.Content;



                    SmtpClient s = new SmtpClient();
                    s.Port = Convert.ToInt32(ue.SmtpPort);

                    s.Host = ue.SmtpAddress;

                    s.Credentials = new System.Net.NetworkCredential(ue.EmailAddress, ue.PassWord);
                    s.Send(m);
                    //LogHelper.PrintLog(Loggerlevel.ERROR, "DoSendMail", "DoSendMail", LoggerMark.Business, "发送成功。将邮件发送给：" + m.To.ToString() + "发送内容为:" + m.Body.ToString());
                    m.Attachments.Dispose();

                    //删除附件
                    if (arrAtt != null)
                    {
                        for (int i = 0; i < arrAtt.Count(); i++)
                        {
                            FileInfo fi = new FileInfo(sendatt + arrAtt[i]);
                            fi.Delete();
                            //LogHelper.PrintLog(Loggerlevel.ERROR, "DoSendMail", "DoSendMail", LoggerMark.Business, "删除附件：" + sendatt + arrAtt[i]);


                        }
                    }
                }
                catch (Exception err)
                {
                    //LogPrint.Print("DoSendMail(List<EmailEntity> list)", err.Message);
                    //LogHelper.PrintLog(Loggerlevel.ERROR, "DoSendMail", "DoSendMail", LoggerMark.Business, err.ToString());

                    continue;
                }
            }
        }
    }
}
