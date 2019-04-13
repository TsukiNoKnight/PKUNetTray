using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using ConfigParser;
using System.IO;
using NLog;

namespace PKUNetTray
{
    public partial class Form1 : Form
    {
        IniReader cfg;
        Logger logger;

        public Form1()
        {
            InitializeComponent();
            if (File.Exists(".nettray\\config.ini"))
            {
                cfg = new IniReader(".nettray\\config.ini");
                int.TryParse(cfg.Read("ipwg", "timer"), out int interval);
                timer1.Interval = interval;
            }
            logger = LogManager.GetCurrentClassLogger();
            logger.Info("Application Started!");
        }

        public Dictionary<string,string> ConnectITS(int operation=0, int range = 1)
        {
            WebClient pkuNet=new WebClient();
            pkuNet.Headers[HttpRequestHeader.ContentType]= "application/x-www-form-urlencoded";
            pkuNet.Encoding = Encoding.GetEncoding("gb2312");
            string opStr;
            switch (operation)
            {
                case 0:
                    opStr = "connect";
                    break;
                case 1:
                    opStr = "disconnect";
                    break;
                case 2:
                    opStr = "disconnectall";
                    break;
                default:
                    opStr = "connect";
                    break;
            }
            string url = "https://its.pku.edu.cn:5428/ipgatewayofpku?";
            string param = "";
            try
            {
                param=
                    "uid=" + cfg.Read("ipwg", "uid") +
                    "&password=" + cfg.Read("ipwg", "password") +
                    "&range=" + range.ToString() +
                    "&operation=" + opStr +
                    "&timeout=1";
            }
            catch(Exception e)
            {
                MessageBox.Show(e.ToString()+"\r\n应用程序退出。", e.ToString());
                logger.Error(e.ToString());
                notifyIcon1.Visible = false;
                this.Close();
                return null;
            }
            string htmlInfo = "";
            try
            {
                htmlInfo = pkuNet.UploadString(url, param);
            }
            catch
            {
                MessageBox.Show("Connection to its.pku.edu.cn failed.");
                logger.Error("Connection to its.pku.edu.cn failed.");
            }
            string precut = htmlInfo.Substring(htmlInfo.IndexOf("IPGWCLIENT_START") + 17, htmlInfo.IndexOf("IPGWCLIENT_END") - htmlInfo.IndexOf("IPGWCLIENT_START") - 18);
            string[] htmlInfoArray = precut.Split(' ');
            Dictionary<string, string> info_dict = new Dictionary<string, string>();
            foreach (var item in htmlInfoArray)
                if (item.IndexOf('=') >= 0 && (item.Length - item.IndexOf('=') - 1) >= 0)
                    info_dict.Add(item.Substring(0, item.IndexOf('=')), item.Substring(item.IndexOf('=') + 1, item.Length - item.IndexOf('=') - 1));
            string log_info = "\t";
            foreach (var item in info_dict)
                log_info += "\r\n\t\t\t\t\t\t\t\t\t\t\t\t" + item.Key + "\t=" + item.Value;
            logger.Info(log_info);
            return info_dict;
        }

        public int checkConnection()
        {
            WebClient pkuNet=new WebClient();
            pkuNet.Headers[HttpRequestHeader.ContentType]= "application/x-www-form-urlencoded";
            pkuNet.Encoding = Encoding.GetEncoding("gb2312");
            string urlOutside = "http://www.baidu.com";
            string urlInside = "https://its.pku.edu.cn";
            try
            {
                var tmpBytesOutside = pkuNet.DownloadData(urlOutside);
                var tmpStrOutside = Encoding.GetEncoding("utf-8").GetString(tmpBytesOutside);
                if (tmpStrOutside.IndexOf("百度") != -1)
                    return 1;
                else
                {
                    var tmpBytesInside = pkuNet.DownloadData(urlInside);
                    var tmpStrInside = Encoding.GetEncoding("utf-8").GetString(tmpBytesInside);
                    if (tmpStrInside.IndexOf("北京大学") != -1)
                        return 0;
                    else
                        return -1;
                }
            }
            catch(Exception e)
            {
                Console.WriteLine(e.ToString());
                logger.Warn(e.ToString() + "Cannot Connect to baidu.com");
                try
                {
                    var tmpBytesInside = pkuNet.DownloadData(urlInside);
                    var tmpStrInside = Encoding.GetEncoding("utf-8").GetString(tmpBytesInside);
                    if (tmpStrInside.IndexOf("北京大学") != -1)
                        return 0;
                    else
                        return -1;
                }
                catch(Exception ee)
                {
                    Console.WriteLine(ee.ToString());
                    logger.Warn(e.ToString() + "Cannot Connect to its.pku.edu.cn");
                    return -1;
                }
            }
        }

        private void connectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ConnectITS(0, 1);
        }

        private void disConnectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ConnectITS(1, 1);
        }

        private void disconnectAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ConnectITS(2, 1);
        }

        private void keepConnectedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (keepConnectedToolStripMenuItem.Checked)
            {
                logger.Info("Keep Connection Started!");
                timer1.Start();
            }
            else
            {
                logger.Info("Keep Connection Ended!");
                timer1.Stop();
            }
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            notifyIcon1.Visible = false;
            this.Close();
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            notifyIcon1.Visible = false;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            notifyIcon1.Visible = false;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            var status = checkConnection();
            if (status == 1)
            {
                logger.Info("IPWG Connected.");
            }
            else if (status == 0)
            {
                logger.Info("IPWG Connection lost.Reconnecting...");
                ConnectITS(0, 1);
            }
            else
            {
                notifyIcon1.ShowBalloonTip(1000, "PKUNet", "Not in campus!", ToolTipIcon.Info);
                keepConnectedToolStripMenuItem.Checked = false;
                timer1.Stop();
                logger.Warn("Not in campus! Keep Connected Canceled.");
            }
        }
    }
}
