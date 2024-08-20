using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Timers;
using System.Configuration;
using System.Windows.Forms;
using System.IO;

namespace dynv6updater.net
{
    public partial class UpdaterService : ServiceBase
    {
        public UpdaterService()
        {
            InitializeComponent();
        }

        private System.Timers.Timer _timerCheckIPv4;
        private WebClient _webClient;
        private Configuration _configurationFile;

        private int _msControlIPv4 = 10 * 60 * 1000;
        private string _apiKeyIPv4 = "";
        private string _zoneToUpdate = "";

        private const string URL_DYNV6_API_CALL = "https://dynv6.com/api/update?zone={0}&token={1}&ipv4={2}";

        public int MsControlIPv4 { get => _msControlIPv4; set => _msControlIPv4 = value; }
        public string ApiKeyIPv4 { get => _apiKeyIPv4; set => _apiKeyIPv4 = value; }
        public string ZoneToUpdate { get => _zoneToUpdate; set => _zoneToUpdate = value; }

        protected override void OnStart(string[] args)
        {
            try
            {
                _configurationFile = ConfigurationManager.OpenExeConfiguration(Application.ExecutablePath);
            }
            catch
            {
                throw new Exception("ERROR WHILE LOADING CONFIGURATION DATA");
            }

            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
            _webClient = new WebClient();

            _timerCheckIPv4 = new System.Timers.Timer();
            _timerCheckIPv4.Elapsed += new ElapsedEventHandler(CheckIPv4_Elapsed);
            _timerCheckIPv4.Interval = 20 * 1000;
            _timerCheckIPv4.AutoReset = false;
            _timerCheckIPv4.Start();
        }

        protected override void OnStop()
        {
            _timerCheckIPv4?.Stop();
        }

        private void CheckIPv4_Elapsed(object sender, ElapsedEventArgs e)
        {
            _timerCheckIPv4.Stop();

            tryUpdateIPv4(getActualIPv4());
            
            try
            {
                int mins = int.Parse(_configurationFile.AppSettings.Settings["MinutesBetweenControls"].Value);
                _msControlIPv4 = mins * 60 * 1000;
            }
            catch { }
            _timerCheckIPv4.Interval = _msControlIPv4;
            _timerCheckIPv4.Start();
        }

        private string getActualIPv4()
        {
            try
            {
                return _webClient.DownloadString("http://ifconfig.me").Replace("\n", "");
            }
            catch { return string.Empty; }
        }

        private void tryUpdateIPv4(string actualIPv4)
        {
            if (string.IsNullOrEmpty(actualIPv4)) return;
            
            try
            {
                _apiKeyIPv4 = _configurationFile.AppSettings.Settings["ApiKeyIPv4"].Value.ToString();
                _zoneToUpdate = _configurationFile.AppSettings.Settings["ZoneToUpdate"].Value.ToString();
            }
            catch { return; }
            if (string.IsNullOrEmpty(_zoneToUpdate)) return;
            if (string.IsNullOrEmpty(_apiKeyIPv4)) return;

            try
            {
                string url = string.Format(URL_DYNV6_API_CALL, _zoneToUpdate, _apiKeyIPv4, actualIPv4);
                var stream = _webClient.OpenRead(url);
                using (StreamReader sr = new StreamReader(stream))
                {
                    var page = sr.ReadToEnd();
                }
            }
            catch { }
        }
    }
}
