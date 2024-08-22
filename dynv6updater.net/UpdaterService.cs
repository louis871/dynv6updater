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
        private string _logFilename = "";

        private const string URL_DYNV6_API_CALL_FORMAT = "https://dynv6.com/api/update?zone={0}&token={1}&ipv4={2}";
        private const string LOG_FILENAME_FORMAT = "{0}\\dynv6updater.net.log";
        private const string LOG_MESSAGE_FORMAT = "#{0} <{1} {2}> {3}\n";

        protected override void OnStart(string[] args)
        {
            _logFilename = string.Format(LOG_FILENAME_FORMAT, Path.GetDirectoryName(Application.ExecutablePath));
            AppendLog("Service started", "I");

            try
            {
                _configurationFile = ConfigurationManager.OpenExeConfiguration(Application.ExecutablePath);
            }
            catch(Exception e)
            {
                AppendLog("Error while loading configuration data: " + getFullNestedErrorMessage(e), "E");
                return;
            }

            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
            _webClient = new WebClient();

            _timerCheckIPv4 = new System.Timers.Timer();
            _timerCheckIPv4.Elapsed += new ElapsedEventHandler(CheckIPv4_Elapsed);
            _timerCheckIPv4.Interval = 1000;
            _timerCheckIPv4.AutoReset = false;
            _timerCheckIPv4.Start();
        }

        protected override void OnStop()
        {
            _timerCheckIPv4?.Stop();
            AppendLog("Service stopped", "I");
        }

        private void CheckIPv4_Elapsed(object sender, ElapsedEventArgs eArgs)
        {
            _timerCheckIPv4.Stop();

            tryUpdateIPv4(getActualIPv4());
            
            try
            {
                int mins = int.Parse(_configurationFile.AppSettings.Settings["MinutesBetweenControls"].Value);
                int newMsControlIPv4 = mins * 60 * 1000;
                if (newMsControlIPv4 != _msControlIPv4)
                {
                    _msControlIPv4 = newMsControlIPv4;
                    AppendLog("MinutesBetweenControls set to " + mins, "I");
                }
            }
            catch(Exception e) 
            {
                AppendLog("Malformed minutes configuration data: " + getFullNestedErrorMessage(e), "E");
                AppendLog("MinutesBetweenControls set to " + (_msControlIPv4 / 60000.00), "I");
            }
            _timerCheckIPv4.Interval = _msControlIPv4;
            _timerCheckIPv4.Start();
        }

        private string getActualIPv4()
        {
            try
            {
                return _webClient.DownloadString("http://ifconfig.me").Replace("\n", "");
            }
            catch(Exception e)
            {
                AppendLog("Actual Public IPv4 <null>: " + getFullNestedErrorMessage(e), "E");
                return string.Empty; 
            }
        }

        private void tryUpdateIPv4(string actualIPv4)
        {
            if (string.IsNullOrEmpty(actualIPv4)) return;
            
            try
            {
                _apiKeyIPv4 = _configurationFile.AppSettings.Settings["ApiKeyIPv4"].Value.ToString();
                _zoneToUpdate = _configurationFile.AppSettings.Settings["ZoneToUpdate"].Value.ToString();
            }
            catch(Exception e)
            {
                AppendLog("Malformed configuration data: " + getFullNestedErrorMessage(e), "E");
                return; 
            }
            if (string.IsNullOrEmpty(_zoneToUpdate))
            {
                AppendLog("Missing Zone configuration", "I");
                return;
            }
            if (string.IsNullOrEmpty(_apiKeyIPv4))
            {
                AppendLog("Missing API Key configuration", "I");
                return;
            }

            try
            {
                string url = string.Format(URL_DYNV6_API_CALL_FORMAT, _zoneToUpdate, _apiKeyIPv4, actualIPv4);
                var stream = _webClient.OpenRead(url);
                using (StreamReader sr = new StreamReader(stream))
                {
                    string output = sr.ReadToEnd();
                    if( !output.ToLower().Contains("unchanged") )
                        AppendLog("Zone " + _zoneToUpdate + ": ("+ actualIPv4 +")" + output, "I");
                }
            }
            catch(Exception e)
            {
                AppendLog("Bad call to dynv6.com API: " + getFullNestedErrorMessage(e), "E");
                return;
            }
        }

        private void AppendLog(string message, string logType)
        {
            File.AppendAllText(_logFilename, string.Format(LOG_MESSAGE_FORMAT, logType, DateTime.Now.ToShortDateString(), DateTime.Now.ToLongTimeString(), message));
        }

        private static string getFullNestedErrorMessage(Exception e)
        {
            string errorMessage = e.Message;
            while (e.InnerException != null)
            {
                errorMessage += " <- " + e.InnerException.Message;
                e = e.InnerException;
            }

            return errorMessage;
        }
    }
}
