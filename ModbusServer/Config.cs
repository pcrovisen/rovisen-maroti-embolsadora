using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Win32;

namespace ModbusServer
{
    public class Config
    {
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
            RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\WencoSettings");

            //if it does exist, retrieve the stored values  
            if (key != null)
            {
                var qrRetries = (int)key.GetValue("QrRetries");
                var continueIfNoQr = Convert.ToBoolean(key.GetValue("ContinueIfNotQr"));
                var continueIfNoDB = Convert.ToBoolean(key.GetValue("ContinueIfNoDB"));
                var defaultRecipe = (int)key.GetValue("DefaultRecipe");

                Instance = new Config { QrRetries = qrRetries, ContinueIfNoQr = continueIfNoQr, ContinueIfNoDB = continueIfNoDB, DefaultRecipe = defaultRecipe };
                key.Close();

                Save();
            }
            else
            {
                Instance = new Config { QrRetries = 5, ContinueIfNoQr = false, ContinueIfNoDB = false, DefaultRecipe = 1 };
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
