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
            if (!File.Exists(".nettray\\config.ini"))
            {
                if (!Directory.Exists(".nettray"))
                    Directory.CreateDirectory(".nettray");
                (File.Create(".nettray\\config.ini")).Close();
                cfg = new IniReader(".nettray\\config.ini");
                cfg.Write("ipwg", "timer", "10000");
                cfg.Write("ipwg", "keeping", "0");
            }
            cfg = new IniReader(".nettray\\config.ini");
            int.TryParse(cfg.Read("ipwg", "timer"), out int interval);
            timer1.Interval = interval;
            int.TryParse(cfg.Read("ipwg", "keeping"), out int isKeeping);
            var uid = cfg.Read("ipwg", "uid");
            var password = base64Code(cfg.Read("ipwg", "password"),"decode",null);
            uidTextBox.Text = uid;
            passwordTextBox.Text = password;

            if (isKeeping == 1)
            {
                keepConnectedToolStripMenuItem.Checked = true;
                timer1.Start();
            }
            checkStartatLogin();
            checkConnection();
        }

        /// <summary>
        /// Encode or decode base64 string.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="code">
        /// "encode" or "decode"
        /// </param>
        /// <param name="encoding">
        /// when input is null,it means "utf-8"
        /// </param>
        /// <returns></returns>
        static public string base64Code(string input,string code,Encoding encoding)
        {
            if (encoding == null)
                encoding = Encoding.GetEncoding("utf-8");
            string result = "";
            if (code == "encode")
            {
                var bytes = encoding.GetBytes(input);
                try
                {
                    result = Convert.ToBase64String(bytes);
                }
                catch
                {

                }
            }
            else if (code == "decode")
            {
                var bytes = Convert.FromBase64String(input);
                try
                {
                    result = encoding.GetString(bytes);
                }
                catch
                {

                }
            }
            return result;
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
                if (path_in_table == Application.ExecutablePath.Replace("/","\\"))
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
                    R_autorun.SetValue(appName, Application.ExecutablePath.Replace("/","\\"));
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
                var uid = cfg.Read("ipwg", "uid");
                var password = base64Code(cfg.Read("ipwg", "password"),"decode",null);
                if (uid == cfg.Notext || password == cfg.Notext)
                    return null;
                param=
                    "uid=" + uid +
                    "&password=" + password +
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
            string log_info = "\t"+opStr;
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
            {
                statusToolStripMenuItem.Text = "Status:Connected";
                statusToolStripMenuItem.BackColor = Color.FromName("lime");
                return 2;
            }
            else if (isConnectedInner)
            {
                statusToolStripMenuItem.Text = "Status:Not Connected";
                statusToolStripMenuItem.BackColor = Color.FromName("Yellow");
                return 1;
            }
            else if (isConnectedExtra)
            {
                statusToolStripMenuItem.Text = "Status:Not in Campus";
                statusToolStripMenuItem.BackColor = Color.FromName("Red");
                return 0;
            }
            else
            {
                statusToolStripMenuItem.Text = "Status:Isolated from world";
                statusToolStripMenuItem.BackColor = Color.FromName("Red");
                return -1;
            }
        }

        private void connectITSTollStripMenuItem_Click(object sender, EventArgs e)
        {
            int connect_op = 0;
            switch (((ToolStripMenuItem)sender).Text)
            {
                case "Connect":
                    connect_op = 0;
                    break;
                case "Disconnect":
                    connect_op = 1;
                    break;
                case "DisconnectAll":
                    connect_op = 2;
                    break;
                default:
                    connect_op = 0;
                    break;
            }
            var info_dict = ConnectITS(connect_op,1);
            if (info_dict == null)
            {
                notifyIcon1.ShowBalloonTip(1000, appName, "Please enter uid and password",ToolTipIcon.Error);
                this.Show();
                this.WindowState = FormWindowState.Normal;
                return;
            }
            if (info_dict["SUCCESS"] == "NO")
            {
                if (connect_op==0 && info_dict["REASON"] == "当前连接数超过预定值")
                {
                    ConnectITS(2, 1);
                    ConnectITS(0, 1);
                }
                else
                {
                    keepConnectedToolStripMenuItem.Checked = false;
                    timer1.Stop();
                    string showinfo = "连接失败\r\n"+info_dict["REASON"];
                    notifyIcon1.ShowBalloonTip(1000, appName, showinfo,ToolTipIcon.Error);
                }
            }
            checkConnection();
        }

        private void keepConnectedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (keepConnectedToolStripMenuItem.Checked)
            {
                logger.Info("Keep Connection Started!");
                cfg.Write("ipwg", "keeping", "1");
                timer1.Start();
            }
            else
            {
                logger.Info("Keep Connection Ended!");
                cfg.Write("ipwg", "keeping", "0");
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

        private void configurationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Normal;
            this.Show();
        }

        private void saveConfigBtn_Click(object sender, EventArgs e)
        {
            cfg.Write("ipwg", "uid", uidTextBox.Text);
            cfg.Write("ipwg", "password", base64Code(passwordTextBox.Text,"encode",null));
            this.WindowState = FormWindowState.Normal;
            this.Hide();
        }

    }
}
