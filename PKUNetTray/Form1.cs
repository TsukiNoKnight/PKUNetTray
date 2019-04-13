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
using Microsoft.Win32;

namespace PKUNetTray
{
    public partial class Form1 : Form
    {
        /// <summary>
        /// Config File Reader(Config is .nettray\\config.ini)
        /// </summary>
        IniReader cfg;
        /// <summary>
        /// Instance of Logger whose logfile is .nettray\\.log
        /// </summary>
        Logger logger;
        /// <summary>
        /// Name stored in registry table.
        /// </summary>
        string appName = "PKUNetTray";

        public Form1()
        {
            InitializeComponent();
            logger = LogManager.GetCurrentClassLogger();
            logger.Info("Application Started!");
            if (File.Exists(".nettray\\config.ini"))
            {
                cfg = new IniReader(".nettray\\config.ini");
                int.TryParse(cfg.Read("ipwg", "timer"), out int interval);
                timer1.Interval = interval;
                int.TryParse(cfg.Read("ipwg", "keeping"), out int isKeeping);
                if (isKeeping == 1)
                {
                    keepConnectedToolStripMenuItem.Checked = true;
                    timer1.Start();
                }
            }
            checkStartatLogin();
        }

        /// <summary>
        /// Check in Registry that whether this app is registered for start at login.
        /// </summary>
        /// <returns></returns>
        public bool checkStartatLogin()
        {
            try
            {
                RegistryKey R_autorun = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
                var path_in_table=(string)R_autorun.GetValue(appName);
                if (path_in_table == Application.ExecutablePath)
                {
                    startAtLoginToolStripMenuItem.Checked = true;
                    R_autorun.Close();
                    return true;
                }
                startAtLoginToolStripMenuItem.Checked = false;
                R_autorun.Close();
                return false;
            }
            catch(Exception e)
            {
                logger.Warn(e.ToString());
                startAtLoginToolStripMenuItem.Checked = false;
                return false;
            }
        }

        /// <summary>
        /// Change the state whether this app starts at login in registry.
        /// </summary>
        /// <param name="isAuto"></param>
        /// <returns></returns>
        public bool setStartatLogin(bool isAuto)
        {
            logger.Info("Set Start at Login as " + isAuto.ToString());
            try
            {
                RegistryKey R_autorun = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
                if (isAuto)
                {
                    R_autorun.SetValue(appName, Application.ExecutablePath);
                }
                else
                    R_autorun.DeleteValue(appName, false);
                R_autorun.Close();
                return true;
            }
            catch(Exception e)
            {
                logger.Warn(e.ToString());
                return false;
            }
        }

        /// <summary>
        /// Connect to its.pku.edu.cn to gain Extra-net access.
        /// </summary>
        /// <param name="operation">
        /// 0 for Connect; 1 for Disconnect; 2 for Disconnect All.
        /// </param>
        /// <param name="range">
        /// 1 for global, 0 for local.(This currently is useless since global time is infinite now.
        /// </param>
        /// <returns>
        /// Stores returned connecting status from web.
        /// </returns>
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

        /// <summary>
        /// Checking is this computer connected to Extra-net or inner-net.
        /// </summary>
        /// <returns>2 for connected to both; 1 for not connected to extra-net; 0 for not in campus; -1 for not connected to both.</returns>
        public int checkConnection()
        {
            string urlExtra = "http://www.baidu.com";
            string urlInner = "https://its.pku.edu.cn";
            bool isConnectedExtra = false;
            bool isConnectedInner = false;
            try
            {
                WebClient pkuNet=new WebClient();
                pkuNet.Headers[HttpRequestHeader.ContentType]= "application/x-www-form-urlencoded";
                var tmpBytesExtra = pkuNet.DownloadData(urlExtra);
                var tmpStrExtra = Encoding.GetEncoding("utf-8").GetString(tmpBytesExtra);
                if (tmpStrExtra.IndexOf("百度") != -1)
                    isConnectedExtra = true;
                else
                    isConnectedExtra = false;
            }
            catch(Exception e)
            {
                logger.Warn(e.ToString() + "Cannot Connect to baidu.com");
                isConnectedExtra = false;
            }
            try
            {
                WebClient pkuNet=new WebClient();
                pkuNet.Headers[HttpRequestHeader.ContentType]= "application/x-www-form-urlencoded";
                var tmpBytesInner = pkuNet.DownloadData(urlInner);
                var tmpStrInner = Encoding.GetEncoding("utf-8").GetString(tmpBytesInner);
                if (tmpStrInner.IndexOf("北京大学") != -1)
                {
                    if (tmpStrInner.IndexOf("校外") != -1)
                        isConnectedInner = false;
                    else
                        isConnectedInner = true;
                }
                else
                    isConnectedInner = false;
            }
            catch(Exception e)
            {
                logger.Warn(e.ToString() + "Cannot Connect to its.pku.edu.cn");
                isConnectedInner = false;
            }
            if (isConnectedExtra && isConnectedInner)
                return 2;
            else if (isConnectedInner)
                return 1;
            else if (isConnectedExtra)
                return 0;
            else
                return -1;
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
            switch (status)
            {
                case 2:
                    logger.Info("IPWG Connected.");
                    break;
                case 1:
                    logger.Info("IPWG Connection lost.Reconnecting...");
                    ConnectITS(0, 1);
                    break;
                case 0:
                    notifyIcon1.ShowBalloonTip(1000, "PKUNet", "Not in campus!", ToolTipIcon.Info);
                    keepConnectedToolStripMenuItem.Checked = false;
                    timer1.Stop();
                    logger.Info("Not in campus! Keep Connected Canceled.");
                    break;
                case -1:
                    notifyIcon1.ShowBalloonTip(1000, "PKUNet", "Not connected to Internet and not in campus!", ToolTipIcon.Warning);
                    keepConnectedToolStripMenuItem.Checked = false;
                    timer1.Stop();
                    logger.Warn("Not connected to Internet and not in campus! Keep Connected Canceled.");
                    break;
                default:
                    break;
            }
        }

        private void startAtLoginToolStripMenuItem_Click(object sender, EventArgs e)
        {
            setStartatLogin(!startAtLoginToolStripMenuItem.Checked);
            checkStartatLogin();
        }
    }
}
