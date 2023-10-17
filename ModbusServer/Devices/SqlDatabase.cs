using log4net;
using log4net.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ModbusServer.Devices
{
    internal class SqlDatabase
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public static SqlDatabase Instance { get; private set; }

        public class PackagerPreference
        {
            public int Packager { get; set; }
            public int Recipe { get; set; }
            public string Injector { get; set; }
            public bool Labeling { get; set; }
        }

        public class Labels
        {
            public string ALabel { get; set; }
            public string BLabel { get; set; }
        }

        string connectionString;

        public enum SystemErrors
        {
            qr_no_detectado,
            sistema_detenido,
            sistema_en_funcionamiento,
            embolsadora_detenida,
            embolsadora_funcionando,
            desorden_embolsadora,
            timeout_etiquetado,
            error_entrega_a_carro,
            error_entrega_a_embolsadora_2,
        }

        public static void Init()
        {
            Instance = new SqlDatabase()
            {
                connectionString = ConfigurationManager.AppSettings["sqlConnectiongString"],
            };
        }
        public static async Task<PackagerPreference> AskForPackager(string code)
        {
            return await Instance._AskForPackager(code);
        }
        private async Task<PackagerPreference> _AskForPackager(string code)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                try
                {
                    await connection.OpenAsync();
                    using (SqlDataReader sqlDataReader = new SqlCommand(CreatePackageCommand(code), connection).ExecuteReader())
                    {
                        await sqlDataReader.ReadAsync();
                        Status.Instance.Connections.WencoDB = connection.State == System.Data.ConnectionState.Open;
                        return new PackagerPreference()
                        {
                            Packager = sqlDataReader[0].ToString() == "" ? 0 : sqlDataReader.GetInt32(0),
                            Recipe = sqlDataReader[1].ToString() == "" ? 0 : sqlDataReader.GetInt32(1),
                            Injector = sqlDataReader.GetString(2),
                            Labeling = sqlDataReader.GetBoolean(3),
                        };
                    }

                }
                catch (Exception ex)
                {
                    Log.Error($"Could not send message to the sql database. Error: {ex.Message}");
                    Status.Instance.Connections.WencoDB = false;
                    return null;
                }
            }
        }
        public static async Task<Labels> AskForLabels(string code, int weight)
        {
            return await Instance._AskForLabels(code, weight);
        }
        private async Task<Labels> _AskForLabels(string code, int weight)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                try
                {
                    await connection.OpenAsync();
                    using (SqlDataReader sqlDataReader = new SqlCommand(CreateLabelsCommand(code, weight), connection).ExecuteReader())
                    {
                        await sqlDataReader.ReadAsync();
                        Status.Instance.Connections.WencoDB = connection.State == System.Data.ConnectionState.Open;
                        return new Labels()
                        {
                            ALabel = sqlDataReader.GetString(0),
                            BLabel = sqlDataReader.GetString(1),
                        };
                    }

                }
                catch (Exception ex)
                {
                    Log.Error($"Could not send message to the sql database. Error: {ex.Message}");
                    Status.Instance.Connections.WencoDB = false;
                    return null;
                }
            }
        }
        public static async Task<bool> NotifyPalletOut(string code)
        {
            return await Instance._NotifyPalletOut(code);
        }
        private async Task<bool> _NotifyPalletOut(string code)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                try
                {
                    await connection.OpenAsync();
                    using (SqlDataReader sqlDataReader = new SqlCommand(CreatePalletOutCommand(code), connection).ExecuteReader())
                    {
                        await sqlDataReader.ReadAsync();
                        Status.Instance.Connections.WencoDB = connection.State == System.Data.ConnectionState.Open;
                        return true;
                    }

                }
                catch (Exception ex)
                {
                    Log.Error($"Could not send message to the sql database. Error: {ex.Message}");
                    Status.Instance.Connections.WencoDB = false;
                    return false;
                }
            }
        }
        public static async Task<bool> NotifyPalletIn(string code, int packager)
        {
            return await Instance._NotifyPalletIn(code, packager);
        }
        private async Task<bool> _NotifyPalletIn(string code, int packager)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                try
                {
                    await connection.OpenAsync();
                    using (SqlDataReader sqlDataReader = new SqlCommand(CreatePalletInCommand(code, packager), connection).ExecuteReader())
                    {
                        await sqlDataReader.ReadAsync();
                        Status.Instance.Connections.WencoDB = connection.State == System.Data.ConnectionState.Open;
                        return true;
                    }

                }
                catch (Exception ex)
                {
                    Log.Error($"Could not send message to the sql database. Error: {ex.Message}");
                    Status.Instance.Connections.WencoDB = false;
                    return false;
                }
            }
        }
        public static async Task<bool> NotifyError(SystemErrors error, string code = "", int packager = 0)
        {
            return await Instance._NotifyError(error.ToString(), code, packager);
        }
        private async Task<bool> _NotifyError(string error, string code, int packager)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                try
                {
                    await connection.OpenAsync();
                    using (SqlDataReader sqlDataReader = new SqlCommand(CreateErrorCommand(error, code, packager), connection).ExecuteReader())
                    {
                        await sqlDataReader.ReadAsync();
                        Status.Instance.Connections.WencoDB = connection.State == System.Data.ConnectionState.Open;
                        return true;
                    }

                }
                catch (Exception ex)
                {
                    Log.Error($"Could not send message to the sql database. Error: {ex.Message}");
                    Status.Instance.Connections.WencoDB = false;
                    return false;
                }
            }
        }
        public static async Task<bool> GetConfiguration()
        {
            return await Instance._GetConfiguration();
        }
        private async Task<bool> _GetConfiguration()
        {
            using (var connection = new SqlConnection(connectionString))
            {
                try
                {
                    await connection.OpenAsync();
                    using (SqlDataReader sqlDataReader = new SqlCommand(CreateConfigCommand(), connection).ExecuteReader())
                    {
                        await sqlDataReader.ReadAsync();
                        Status.Instance.Connections.WencoDB = connection.State == System.Data.ConnectionState.Open;
                        return true;
                    }

                }
                catch (Exception ex) 
                {
                    Log.Error($"Could not send message to the sql database. Error: {ex.Message}");
                    Status.Instance.Connections.WencoDB = false;
                    return false;
                }
            }
        }

        public static async Task<bool> GetAuthElevator(string code)
        {
            return await Instance._GetAuthElevator(code);
        }

        private async Task<bool> _GetAuthElevator(string code)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                try
                {
                    await connection.OpenAsync();
                    using (SqlDataReader sqlDataReader = new SqlCommand(CreateElevatorCommnad(code), connection).ExecuteReader())
                    {
                        await sqlDataReader.ReadAsync();
                        Status.Instance.Connections.WencoDB = connection.State == System.Data.ConnectionState.Open;
                        return sqlDataReader.GetBoolean(0);
                    }

                }
                catch (Exception ex)
                {
                    Log.Error($"Could not send message to the sql database. Error: {ex.Message}");
                    Status.Instance.Connections.WencoDB = false;
                    return false;
                }
            }
        }

        private string CreatePackageCommand(string code)
        {
            return ("\r\n DECLARE @out_preferencia_embolsadora INT;\r\n" +
                    " DECLARE @out_receta INT;\r\n" +
                    " DECLARE @out_identificador_visual NVARCHAR(50);\r\n" +
                    " DECLARE @out_omitir_proceso_etiquetado BIT;\r\n" +
                    "EXECUTE [maroti].[sp_evento_lectura_codigo]\r\n" +
                    "@in_codigo = '" + code + "'\r\n" +
                    ",@in_disponibilidad_embolsadora_1 = " + (FatekPLC.ReadBit(FatekPLC.Signals.bcd1Avaliable) ? 1 : 0)  + "\r\n" +
                    ",@in_disponibilidad_embolsadora_2 = " + (FatekPLC.ReadBit(FatekPLC.Signals.bcd2Avaliable) ? 1 : 0) + "\r\n" +
                    ",@out_preferencia_embolsadora = @out_preferencia_embolsadora OUTPUT\r\n" +
                    ",@out_receta = @out_receta OUTPUT\r\n" +
                    ",@out_identificador_visual = @out_identificador_visual OUTPUT\r\n" +
                    ",@out_omitir_proceso_etiquetado = @out_omitir_proceso_etiquetado OUTPUT;\r\n\r\n" +
                    "select @out_preferencia_embolsadora, @out_receta, @out_identificador_visual, @out_omitir_proceso_etiquetado;\r\n");
        }

        private string CreateLabelsCommand(string code, int weight)
        {
            return ("\r\n DECLARE @out_comando_etiqueta_1 NVARCHAR(MAX);\r\n" +
                    " DECLARE @out_comando_etiqueta_2 NVARCHAR(MAX);\r\n" +
                    "EXECUTE [maroti].[sp_evento_peso_embolsadora_y_datos_etiquetado]\r\n" +
                    "@in_codigo = '" + code + "'\r\n" +
                    ",@in_peso_gramos = " + weight + "\r\n" +
                    ",@out_comando_etiqueta_1 = @out_comando_etiqueta_1 OUTPUT\r\n" +
                    ",@out_comando_etiqueta_2 = @out_comando_etiqueta_2 OUTPUT;\r\n\r\n" +
                    "select @out_comando_etiqueta_1, @out_comando_etiqueta_2;\r\n");
        }

        private string CreatePalletInCommand(string code, int packager)
        {
            return ("\r\n DECLARE @out_ack INT;\r\n" +
                    "EXECUTE [maroti].[sp_evento_ingreso_embolsadora]\r\n" +
                    "@in_codigo = '" + code + "'\r\n" +
                    ",@in_embolsadora = " + packager + "\r\n" +
                    ",@out_ack = @out_ack OUTPUT;\r\n\r\n" +
                    "select @out_ack;\r\n");
        }

        private string CreatePalletOutCommand(string code)
        {
            return ("\r\n DECLARE @out_ack INT;\r\n" +
                    "EXECUTE [maroti].[sp_evento_etiquetado]\r\n" +
                    "@in_codigo = '" + code + "'\r\n" +
                    ",@out_ack = @out_ack OUTPUT;\r\n\r\n" +
                    "select @out_ack;\r\n");
        }

        private string CreateErrorCommand(string error, string code, int packager)
        {
            return ("\r\n DECLARE @out_ack INT;\r\n" +
                    "EXECUTE [maroti].[sp_evento_alarma]\r\n" +
                    "@in_id_tipo_alerta = '" + error + "'\r\n" +
                    ",@in_id_embolsadora = " + (packager == 0 ? "NULL" : packager.ToString()) + "\r\n" +
                    ",@in_codigo = " + (code == "" ? "NULL" : ("'" + code + "'")) + "\r\n" +
                    ",@out_ack = @out_ack OUTPUT;\r\n\r\n" +
                    "select @out_ack;\r\n");
        }
        private string CreateConfigCommand()
        {
            return ("\r\n DECLARE @out_continuar_sin_lectura_codigo BIT;\r\n" +
                    " DECLARE @out_continuar_sin_respuesta_db BIT;\r\n" +
                    " DECLARE @out_receta_por_defecto INT;\r\n" +
                    "EXECUTE [maroti].[sp_get_parametros]\r\n" +
                    "@out_continuar_sin_lectura_codigo = @out_continuar_sin_lectura_codigo OUTPUT\r\n" +
                    ",@out_continuar_sin_respuesta_db = @out_continuar_sin_respuesta_db OUTPUT\r\n" +
                    ",@out_receta_por_defecto = @out_receta_por_defecto OUTPUT;\r\n\r\n" +
                    "select @out_continuar_sin_lectura_codigo, out_continuar_sin_respuesta_db, out_receta_por_defecto;\r\n");
        }

        private string CreateElevatorCommnad(string code)
        {
            return ("\r\n DECLARE @out_autorizado BIT;\r\n" +
                    "EXECUTE [maroti].[sp_solicitar_ingreso_por_elevador]\r\n" +
                    "@in_codigo = '" + code + "'\r\n" +
                    ",@out_autorizado = @out_autorizado OUTPUT;\r\n\r\n" +
                    "select @out_autorizado;\r\n");
        }
    }
}