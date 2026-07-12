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
        public static void Load()
        {
            RegistryKey key = null;
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
                    Instance = new Config { QrRetries = 1, ContinueIfNoQr = false, ContinueIfNoDB = false, DefaultRecipe = 1 };
                }
            }
            catch (Exception e)
            {
                // A missing or wrongly-typed value must not stop the service.
                Log.ErrorFormat("Could not read WencoSettings from the registry. Using defaults. Error: {0}", e.Message);
                Instance = new Config { QrRetries = 1, ContinueIfNoQr = false, ContinueIfNoDB = false, DefaultRecipe = 1 };
            }
            finally
            {
                key?.Close();
            }

            Save();
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
