using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EmailService
{
    class EmailEntity
    {
        private string emailId;

        public string EmailId
        {
            get { return emailId; }
            set { emailId = value; }
        }

        private string from;
        //发件人
        public string From
        {
            get { return from; }
            set { from = value; }
        }

        private string to;
        //收件人
        public string To
        {
            get { return to; }
            set { to = value; }
        }

        private string cc;
        //抄送
        public string Cc
        {
            get { return cc; }
            set { cc = value; }
        }

        private string bcc;
        //密送
        public string Bcc
        {
            get { return bcc; }
            set { bcc = value; }
        }

        private DateTime date;
        //日期
        public DateTime Date
        {
            get { return date; }
            set { date = value; }
        }

        private string subject;
        //主题
        public string Subject
        {
            get { return subject; }
            set { subject = value; }
        }

        private string content;
        //正文内容
        public string Content
        {
            get { return content; }
            set { content = value; }
        }


        private List<string> filesize = new List<string>();
        //附件大小
        public List<string> Filesize
        {
            get { return filesize; }
            set { filesize = value; }
        }

        private string attachment = string.Empty;
        //附件名称
        public string Attachment
        {
            get { return attachment; }
            set { attachment = value; }
        }
        private string attachmentUrl = string.Empty;
        //附件链接
        public string AttachmentUrl
        {
            get { return attachmentUrl; }
            set { attachmentUrl = value; }
        }
    }
}
