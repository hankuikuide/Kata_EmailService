using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EmailService
{
    public class UserEntity
    {
        private string emailAddress;

        public string EmailAddress
        {
            get { return emailAddress; }
            set { emailAddress = value; }
        }
        private string pop3Address;

        public string Pop3Address
        {
            get { return pop3Address; }
            set { pop3Address = value; }
        }
        private string pop3Port;

        public string Pop3Port
        {
            get { return pop3Port; }
            set { pop3Port = value; }
        }
        private string smtpAddress;

        public string SmtpAddress
        {
            get { return smtpAddress; }
            set { smtpAddress = value; }
        }
        private string smtpPort;

        public string SmtpPort
        {
            get { return smtpPort; }
            set { smtpPort = value; }
        }
        private string passWord;

        public string PassWord
        {
            get { return passWord; }
            set { passWord = value; }
        }
    }
}
