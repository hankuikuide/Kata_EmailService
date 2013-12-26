using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Data.OracleClient;
using System.IO;

namespace EmailService
{
    class DBHelper
    {
        private OracleConnection conn = null;
        private OracleCommand cmd = null;

        public string Email_GatewayAddress = string.Empty;

        public DBHelper()
        {
            try
            {
                conn = new OracleConnection(ServiceEamil.ds.Tables["global"].Rows[0]["conn"].ToString());
                conn.Open();
                cmd = new OracleCommand();
                cmd.Connection = conn;
            }
            catch (Exception err)
            {
                // LogPrint.Print("DBHelper()", err.Message);
                //LogHelper.PrintLog(Loggerlevel.ERROR, "DBHelper", "DBHelper", LoggerMark.Business, err.ToString() + "DBHelper error:");


            }

        }
        /// <summary>
        /// 从数据库中获取待处理的数据
        /// </summary>
        /// <returns>返回所有数据</returns>
        public List<EmailEntity> GetDataFromDB()
        {
            int actual = 0;

            List<EmailEntity> list = new List<EmailEntity>();
            OracleDataReader odr = null;

            string sql = "select * from crm_email_info where STATUS=2 and TYPE_FLAG=0";
            cmd.CommandText = sql;
            odr = cmd.ExecuteReader();
            StringBuilder sb = new StringBuilder();
            while (odr.Read())
            {
                try
                {
                    string EmailID = odr.GetOracleValue(0).ToString();
                    EmailEntity email = new EmailEntity();
                    email.EmailId = EmailID;
                    email.To = odr.GetOracleString(10).ToString();
                    email.Cc = odr.GetOracleString(12).ToString();

                    //string aa = odr.GetOracleString(12);

                    email.From = odr.GetOracleString(11).ToString();
                    email.Subject = odr.GetOracleString(8).ToString();
                    OracleLob myClob = odr.GetOracleLob(7);
                    StreamReader sr = new StreamReader(myClob, Encoding.Unicode);
                    char[] cbuffer = new char[100];
                    while ((actual = sr.Read(cbuffer, 0, cbuffer.Length)) > 0)
                    {
                        string text = new string(cbuffer, 0, actual);
                        sb.Append(text);
                    }
                    email.Content = sb.ToString();
                    sb.Remove(0, sb.Length);

                    string attsql = "select FILENAME,FILETITLE from crm_email_attach where EMAILID='" + EmailID + "'";
                    cmd.CommandText = attsql;
                    OracleDataReader attodr = cmd.ExecuteReader();
                    while (attodr.Read())
                    {
                        email.Attachment = attodr.GetOracleString(0).ToString();
                        email.AttachmentUrl = attodr.GetOracleString(1).ToString();
                    }
                    attodr.Close();
                    //将该信息存放在队列中
                    list.Add(email);
                    //LogHelper.PrintLog(Loggerlevel.ERROR, "Form1", "Form1", LoggerMark.Business, "将邮件发送给：" + odr.GetOracleString(10).ToString() + "。邮件内容:" + email.Content);

                }
                catch (Exception err)
                {
                    //LogPrint.Print("GetDataFromDB()", err.Message);
                    //LogHelper.PrintLog(Loggerlevel.ERROR, "GetDataFromDB", "GetDataFromDB", LoggerMark.Business, err.ToString() + "GetDataFromDB error:");

                }
            }

            odr.Close();

            return list;
        }

        /// <summary>
        /// 将数据库中的数据设置为已完成
        /// </summary>
        /// <param name="odr"></param>
        public void SetDataFinish(List<EmailEntity> list)
        {

            foreach (EmailEntity ee in list)
            {
                try
                {
                    string sqlstr = "update crm_email_info set Status=4" + " where EMAIL_ID='" + ee.EmailId + "'";
                    cmd.CommandText = sqlstr;
                    cmd.ExecuteNonQuery();
                }
                catch (Exception err)
                {
                    // LogPrint.Print("SetDataFinish(List<EmailEntity> list)", err.Message);
                    //LogHelper.PrintLog(Loggerlevel.ERROR, "SetDataFinish", "SetDataFinish", LoggerMark.Business, err.ToString() + "SetDataFinish error:");

                }
            }

            conn.Close();
        }

        /// <summary>
        /// 将解析完成的邮件数据导入数据库中
        /// </summary>
        /// <param name="list">邮件信息列表</param>
        public void CommitDataToDB(List<EmailEntity> list)
        {
            //cmd = new OracleCommand();
            foreach (EmailEntity ee in list)
            {
                try
                {
                    OracleParameter[] parameters ={
                                                 new OracleParameter("P_Email_ID",OracleType.Number),
                                                 new OracleParameter("P_Schedule_time",OracleType.DateTime),
                                                 new OracleParameter("P_Sent_time",OracleType.DateTime),
                                                 new OracleParameter("P_Recv_time",OracleType.DateTime),
                                                 new OracleParameter("P_Type_flag",OracleType.VarChar),
                                                 new OracleParameter("P_Status",OracleType.VarChar),
                                                 new OracleParameter("P_Msg",OracleType.Clob),
                                                 new OracleParameter("P_Subject",OracleType.VarChar),
                                                 new OracleParameter("P_PRI",OracleType.Number),
                                                 new OracleParameter("P_Email_Address",OracleType.VarChar),
                                                 new OracleParameter("P_CC_Address",OracleType.VarChar),
                                                 new OracleParameter("P_Email_GatewayAddress",OracleType.VarChar),
                                                 new OracleParameter("P_CreateTime",OracleType.DateTime)
                                                 
                                             };
                    parameters[0].Direction = ParameterDirection.Output;
                    parameters[1].Value = DateTime.MinValue;
                    parameters[1].Direction = ParameterDirection.Input;
                    parameters[2].Value = DateTime.MinValue;
                    parameters[2].Direction = ParameterDirection.Input;
                    parameters[3].Value = ee.Date;
                    parameters[3].Direction = ParameterDirection.Input;
                    parameters[4].Value = '1';
                    parameters[4].Direction = ParameterDirection.Input;
                    parameters[5].Value = '2';
                    parameters[5].Direction = ParameterDirection.Input;
                    parameters[6].Value = ee.Content.Replace("\0", "");
                    parameters[6].Direction = ParameterDirection.Input;
                    parameters[7].Value = ee.Subject.Replace("\0", "");
                    parameters[7].Direction = ParameterDirection.Input;

                    //邮件等级暂缺，将保持默认值。
                    parameters[8].Value = 3;
                    parameters[8].Direction = ParameterDirection.Input;


                    parameters[9].Value = ee.From.Replace("\0", "");
                    parameters[9].Direction = ParameterDirection.Input;
                    if (ee.Cc == "")
                    {
                        ee.Cc = "Null";
                        parameters[10].Value = ee.Cc.Replace("\0", "");
                    }
                    else
                    {
                        parameters[10].Value = ee.Cc.Replace("\0", "");
                    }

                    parameters[10].Direction = ParameterDirection.Input;
                    //邮件接收地址

                    parameters[11].Value = Email_GatewayAddress;

                    parameters[11].Direction = ParameterDirection.Input;

                    parameters[12].Value = DateTime.Now;
                    parameters[12].Direction = ParameterDirection.Input;

                    cmd.CommandText = "Crm_Email_Info_Add";//设置存储过程名
                    cmd.CommandType = CommandType.StoredProcedure;
                    foreach (OracleParameter parameter in parameters)
                    {
                        cmd.Parameters.Add(parameter);
                    }
                    int temp = 0;
                    temp = cmd.ExecuteNonQuery(); //执行存储过程

                    cmd.Parameters.Clear();

                    string EmailId = parameters[0].Value.ToString();//返回Email ID.

                    if (EmailId == "0")
                    {
                        //LogPrint.Print("CommitDataToDB(List<EmailEntity> list)", "数据插入失败，可能是数据库中的某个字段空间太小导致的。");
                    }
                    else
                    {
                        if (ee.AttachmentUrl == "")
                        {
                            continue;
                        }

                        string sql = "INSERT INTO CRM_Email_Attach(EmailID,FileName,FileTitle,CreateTime) values('" + int.Parse(EmailId) + "','" + ee.AttachmentUrl + "','" + ee.Attachment + "',sysdate)";

                        //当值为空时，赋为Null,防止出现异常

                        cmd.Parameters.Clear();
                        cmd.CommandText = sql;
                        cmd.ExecuteNonQuery();

                    }
                }
                catch (Exception err)
                {
                    //LogPrint.Print("CommitDataToDB(List<EmailEntity> list)", err.Message);
                    //LogHelper.PrintLog(Loggerlevel.ERROR, "CommitDataToDB", "CommitDataToDB", LoggerMark.Business, err.ToString());

                }
            }

            conn.Close();//关闭数据库链接。
        } 
    }
}
