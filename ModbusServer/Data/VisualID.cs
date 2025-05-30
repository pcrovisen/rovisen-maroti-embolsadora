﻿using log4net;
using ModbusServer.Devices;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ModbusServer.Data
{
    public class VisualID
    {
        static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public static VisualID Instance { get; private set; }
        public Dictionary<string, ushort> Data { get; set; }
        public Dictionary<string, uint> Qrs { get; set; }
        public ushort LastId { get; set; }
        public uint LastQrId { get; set; }

        public static void Init()
        {
            Load();
        }
        public static void Load()
        {
            try
            {
                string text = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "visualIdData.json"));
                Instance = JsonSerializer.Deserialize<VisualID>(text);
            }
            catch(Exception e)
            {
                Log.Error(e.Message);
                Instance = new VisualID()
                {
                    Data = new Dictionary<string, ushort>(),
                    Qrs = new Dictionary<string, uint>(),
                    LastId = 0,
                    LastQrId = 0,
                };
                Save();
            }
            
        }
        public static void Save()
        {
            var filename = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "visualIdData.json");
            File.WriteAllText(filename, JsonSerializer.Serialize(Instance));
        }

        public static ushort GetId(string id)
        {
            if (Instance.Data.ContainsKey(id))
            {
                return Instance.Data[id];
            }
            else
            {
                Instance.Data.Add(id, ++Instance.LastId);
                Save();
                return Instance.LastId;
            }
        }

        public static string GetVisualId(ushort id)
        {
            if(id <= Instance.Data.Count)
            {
                return Instance.Data.Keys.ElementAt(id - 1);
            }
            else
            {
                return "";
            }
        }

        public static async Task<int> GetQrId(string qr)
        {
            return await SqlDatabase.GetIDFromString(qr);
        }

        public static async Task<string> GetQrString(int id)
        {
            var qrString = await SqlDatabase.GetStringFromID(id);
            if(qrString == "")
            {
                Log.Error("QR not valid");
                return null;
            }

            return qrString;
        }
    }
}
