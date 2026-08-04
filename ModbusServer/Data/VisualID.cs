using log4net;
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

        // id -> name, the inverse of Data. Private, so it is not serialized: it is
        // rebuilt from Data on load and kept in step by GetId.
        private Dictionary<ushort, string> byId = new Dictionary<ushort, string>();

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
                if (Instance?.Data == null)
                {
                    throw new InvalidDataException("visualIdData.json has no Data map");
                }
                Instance.Qrs = Instance.Qrs ?? new Dictionary<string, uint>();
                Instance.BuildReverseIndex();
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

        private void BuildReverseIndex()
        {
            byId = new Dictionary<ushort, string>();
            foreach (var entry in Data)
            {
                byId[entry.Value] = entry.Key;
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
                Instance.byId[Instance.LastId] = id;
                Save();
                return Instance.LastId;
            }
        }

        // Was `Data.Keys.ElementAt(id - 1)`: an O(n) scan that assumed the
        // dictionary's enumeration order still matched the order ids were handed
        // out, which is why visualIdData.json could not be reordered by hand. The
        // inverse map makes the id the actual key, so the file's order stops
        // mattering and an unknown id gives "" instead of throwing.
        public static string GetVisualId(ushort id)
        {
            return Instance.byId.TryGetValue(id, out var name) ? name : "";
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
