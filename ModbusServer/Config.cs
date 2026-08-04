using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using log4net;
using Microsoft.Win32;

namespace ModbusServer
{
    public class Config
    {
        static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public static Config Instance { get; private set; }
        public int QrRetries { get; set; }
        public bool ContinueIfNoQr { get; set; }
        public bool ContinueIfNoDB { get; set; }
        public int DefaultRecipe { get; set; }

        public static void Init()
        {
            Load();
        }
        private static Config Defaults()
        {
            return new Config { QrRetries = 1, ContinueIfNoQr = false, ContinueIfNoDB = false, DefaultRecipe = 1 };
        }

        public static void Load()
        {
            RegistryKey key = null;
            // Only a first run (no key at all) may write the defaults back. Saving
            // unconditionally meant that one wrongly-typed or missing value made the
            // service overwrite the operator's whole configuration with defaults —
            // silently, and permanently.
            var firstRun = false;
            try
            {
                key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\WencoSettings");

                //if it does exist, retrieve the stored values
                if (key != null)
                {
                    var qrRetries = (int)key.GetValue("QrRetries");
                    var continueIfNoQr = Convert.ToBoolean(key.GetValue("ContinueIfNotQr"));
                    var continueIfNoDB = Convert.ToBoolean(key.GetValue("ContinueIfNoDB"));
                    var defaultRecipe = (int)key.GetValue("DefaultRecipe");

                    Instance = new Config { QrRetries = qrRetries, ContinueIfNoQr = continueIfNoQr, ContinueIfNoDB = continueIfNoDB, DefaultRecipe = defaultRecipe };
                }
                else
                {
                    Instance = Defaults();
                    firstRun = true;
                }
            }
            catch (Exception e)
            {
                // A missing or wrongly-typed value must not stop the service — but it
                // must not destroy the other values either, so no Save() here.
                Log.ErrorFormat("Could not read WencoSettings from the registry. Using defaults for this run, leaving the stored values alone. Error: {0}", e.Message);
                Instance = Defaults();
            }
            finally
            {
                key?.Close();
            }

            if (firstRun)
            {
                Save();
            }
        }

        public static void Save()
        {
            RegistryKey key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\WencoSettings");

            key.SetValue("QrRetries", Instance.QrRetries);
            key.SetValue("ContinueIfNotQr", Instance.ContinueIfNoQr);
            key.SetValue("ContinueIfNoDB", Instance.ContinueIfNoDB);
            key.SetValue("DefaultRecipe", Instance.DefaultRecipe);
            key.Close();
        }
    }
}
