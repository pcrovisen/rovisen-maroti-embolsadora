using log4net;
using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Reflection;
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

        // ------------------------------------------------------------------
        // Calls
        // ------------------------------------------------------------------
        //
        // Every one of these was 25 lines of the same open/read/catch, doubled by
        // a `static Foo() => Instance._Foo()` wrapper. The stored-procedure text
        // is a contract with the [maroti] schema and is unchanged; only the
        // plumbing moved into Execute.

        public static Task<PackagerPreference> AskForPackager(string code)
        {
            return Execute(
                c => PackageCommand(c, code),
                r => new PackagerPreference()
                {
                    Packager = r[0].ToString() == "" ? 0 : r.GetInt32(0),
                    Recipe = r[1].ToString() == "" ? 0 : r.GetInt32(1),
                    Injector = r.GetString(2),
                    Labeling = r.GetBoolean(3),
                },
                null, "sp_evento_lectura_codigo");
        }

        public static Task<Labels> AskForLabels(string code, int weight)
        {
            return Execute(
                c => LabelsCommand(c, code, weight),
                r => new Labels() { ALabel = r.GetString(0), BLabel = r.GetString(1) },
                null, "sp_evento_peso_embolsadora_y_datos_etiquetado");
        }

        public static Task<bool> NotifyPalletOut(string code)
        {
            return Execute(c => PalletOutCommand(c, code), r => true, false, "sp_evento_etiquetado");
        }

        public static Task<bool> NotifyPalletIn(string code, int packager)
        {
            return Execute(c => PalletInCommand(c, code, packager), r => true, false, "sp_evento_ingreso_embolsadora");
        }

        public static Task<bool> NotifyError(SystemErrors error, string code = "", int packager = 0)
        {
            return Execute(c => ErrorCommand(c, error.ToString(), code, packager), r => true, false, "sp_evento_alarma");
        }

        // Remote configuration. Not called yet — see docs/ARCHITECTURE.md — but it
        // is the only record of the sp_get_parametros signature.
        public static Task<bool> GetConfiguration()
        {
            return Execute(ConfigCommand, r => true, false, "sp_get_parametros");
        }

        public static Task<bool> GetAuthElevator(string code, string msg = null)
        {
            return Execute(c => ElevatorCommand(c, code, msg), r => r.GetBoolean(0), false,
                "sp_solicitar_ingreso_por_elevador");
        }

        public static Task<string> GetStringFromID(int id)
        {
            return Execute(c => GetStringFromIDCommand(c, id), r => r[0].ToString(), "", "sp_get_codigo");
        }

        public static Task<int> GetIDFromString(string code)
        {
            return Execute(c => GetIDFromStringCommand(c, code), r => r.GetInt32(0), -1, "sp_get_id");
        }

        // ------------------------------------------------------------------
        // Plumbing
        // ------------------------------------------------------------------

        // Opens a connection, runs `build`'s command, and projects the first row
        // with `read`. Any failure — connection, execution, or an unusable row —
        // is logged and reported as `onError`; these calls never throw at the
        // caller, which polls them from a state machine step.
        private static async Task<T> Execute<T>(
            Func<SqlConnection, SqlCommand> build,
            Func<SqlDataReader, T> read,
            T onError,
            string procedure)
        {
            using (var connection = new SqlConnection(Instance.connectionString))
            {
                try
                {
                    await connection.OpenAsync();
                    using (var command = build(connection))
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        Status.Instance.Connections.WencoDB = connection.State == ConnectionState.Open;

                        // The batches all end in a SELECT of the output parameters,
                        // so a missing row means the procedure did not run.
                        if (!await reader.ReadAsync())
                        {
                            Log.ErrorFormat("{0} returned no rows", procedure);
                            return onError;
                        }
                        return read(reader);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"Could not send message to the sql database ({procedure}). Error: {ex.Message}");
                    Status.Instance.Connections.WencoDB = false;
                    return onError;
                }
            }
        }

        // ------------------------------------------------------------------
        // Commands
        // ------------------------------------------------------------------
        //
        // All inputs go in as SqlParameters — never interpolate scanned QR content
        // into the SQL text.

        private static SqlCommand PackageCommand(SqlConnection connection, string code)
        {
            var command = new SqlCommand(
                    "\r\n DECLARE @out_preferencia_embolsadora INT;\r\n" +
                    " DECLARE @out_receta INT;\r\n" +
                    " DECLARE @out_identificador_visual NVARCHAR(50);\r\n" +
                    " DECLARE @out_omitir_proceso_etiquetado BIT;\r\n" +
                    "EXECUTE [maroti].[sp_evento_lectura_codigo]\r\n" +
                    "@in_codigo = @codigo\r\n" +
                    ",@in_disponibilidad_embolsadora_1 = @disponibilidad1\r\n" +
                    ",@in_disponibilidad_embolsadora_2 = @disponibilidad2\r\n" +
                    ",@out_preferencia_embolsadora = @out_preferencia_embolsadora OUTPUT\r\n" +
                    ",@out_receta = @out_receta OUTPUT\r\n" +
                    ",@out_identificador_visual = @out_identificador_visual OUTPUT\r\n" +
                    ",@out_omitir_proceso_etiquetado = @out_omitir_proceso_etiquetado OUTPUT;\r\n\r\n" +
                    "select @out_preferencia_embolsadora, @out_receta, @out_identificador_visual, @out_omitir_proceso_etiquetado;\r\n", connection);
            command.Parameters.AddWithValue("@codigo", code);
            command.Parameters.AddWithValue("@disponibilidad1", FatekPLC.ReadBit(FatekPLC.Signals.bcd1Avaliable) ? 1 : 0);
            command.Parameters.AddWithValue("@disponibilidad2", FatekPLC.ReadBit(FatekPLC.Signals.bcd2Avaliable) ? 1 : 0);
            return command;
        }

        private static SqlCommand LabelsCommand(SqlConnection connection, string code, int weight)
        {
            var command = new SqlCommand(
                    "\r\n DECLARE @out_comando_etiqueta_1 NVARCHAR(MAX);\r\n" +
                    " DECLARE @out_comando_etiqueta_2 NVARCHAR(MAX);\r\n" +
                    "EXECUTE [maroti].[sp_evento_peso_embolsadora_y_datos_etiquetado]\r\n" +
                    "@in_codigo = @codigo\r\n" +
                    ",@in_peso_gramos = @peso\r\n" +
                    ",@out_comando_etiqueta_1 = @out_comando_etiqueta_1 OUTPUT\r\n" +
                    ",@out_comando_etiqueta_2 = @out_comando_etiqueta_2 OUTPUT;\r\n\r\n" +
                    "select @out_comando_etiqueta_1, @out_comando_etiqueta_2;\r\n", connection);
            command.Parameters.AddWithValue("@codigo", code);
            command.Parameters.AddWithValue("@peso", weight);
            return command;
        }

        private static SqlCommand PalletInCommand(SqlConnection connection, string code, int packager)
        {
            var command = new SqlCommand(
                    "\r\n DECLARE @out_ack INT;\r\n" +
                    "EXECUTE [maroti].[sp_evento_ingreso_embolsadora]\r\n" +
                    "@in_codigo = @codigo\r\n" +
                    ",@in_embolsadora = @embolsadora\r\n" +
                    ",@out_ack = @out_ack OUTPUT;\r\n\r\n" +
                    "select @out_ack;\r\n", connection);
            command.Parameters.AddWithValue("@codigo", code);
            command.Parameters.AddWithValue("@embolsadora", packager);
            return command;
        }

        private static SqlCommand PalletOutCommand(SqlConnection connection, string code)
        {
            var command = new SqlCommand(
                    "\r\n DECLARE @out_ack INT;\r\n" +
                    "EXECUTE [maroti].[sp_evento_etiquetado]\r\n" +
                    "@in_codigo = @codigo\r\n" +
                    ",@out_ack = @out_ack OUTPUT;\r\n\r\n" +
                    "select @out_ack;\r\n", connection);
            command.Parameters.AddWithValue("@codigo", code);
            return command;
        }

        private static SqlCommand ErrorCommand(SqlConnection connection, string error, string code, int packager)
        {
            var command = new SqlCommand(
                    "\r\n DECLARE @out_ack INT;\r\n" +
                    "EXECUTE [maroti].[sp_evento_alarma]\r\n" +
                    "@in_id_tipo_alerta = @tipoAlerta\r\n" +
                    ",@in_id_embolsadora = @embolsadora\r\n" +
                    ",@in_codigo = @codigo\r\n" +
                    ",@out_ack = @out_ack OUTPUT;\r\n\r\n" +
                    "select @out_ack;\r\n", connection);
            command.Parameters.AddWithValue("@tipoAlerta", error);
            command.Parameters.AddWithValue("@embolsadora", packager == 0 ? (object)DBNull.Value : packager);
            command.Parameters.AddWithValue("@codigo", string.IsNullOrEmpty(code) ? (object)DBNull.Value : code);
            return command;
        }

        private static SqlCommand ConfigCommand(SqlConnection connection)
        {
            return new SqlCommand(
                    "\r\n DECLARE @out_continuar_sin_lectura_codigo BIT;\r\n" +
                    " DECLARE @out_continuar_sin_respuesta_db BIT;\r\n" +
                    " DECLARE @out_receta_por_defecto INT;\r\n" +
                    "EXECUTE [maroti].[sp_get_parametros]\r\n" +
                    "@out_continuar_sin_lectura_codigo = @out_continuar_sin_lectura_codigo OUTPUT\r\n" +
                    ",@out_continuar_sin_respuesta_db = @out_continuar_sin_respuesta_db OUTPUT\r\n" +
                    ",@out_receta_por_defecto = @out_receta_por_defecto OUTPUT;\r\n\r\n" +
                    "select @out_continuar_sin_lectura_codigo, @out_continuar_sin_respuesta_db, @out_receta_por_defecto;\r\n", connection);
        }

        private static SqlCommand ElevatorCommand(SqlConnection connection, string code, string msg)
        {
            var command = new SqlCommand(
                    "\r\n DECLARE @out_autorizado BIT;\r\n" +
                    "EXECUTE [maroti].[sp_solicitar_ingreso_por_elevador]\r\n" +
                    "@in_codigo = @codigo\r\n" +
                    ",@in_msg = @msg\r\n" +
                    ",@out_autorizado = @out_autorizado OUTPUT;\r\n\r\n" +
                    "select @out_autorizado;\r\n", connection);
            command.Parameters.AddWithValue("@codigo", code ?? string.Empty);
            command.Parameters.AddWithValue("@msg", msg ?? string.Empty);
            return command;
        }

        private static SqlCommand GetStringFromIDCommand(SqlConnection connection, int id)
        {
            var command = new SqlCommand(
                    "\r\n DECLARE @out_codigo NVARCHAR(200);\r\n" +
                    "EXECUTE [maroti].[sp_get_codigo]\r\n" +
                    "@in_id = @id\r\n" +
                    ",@out_codigo = @out_codigo OUTPUT;\r\n\r\n" +
                    "select @out_codigo;\r\n", connection);
            command.Parameters.AddWithValue("@id", id);
            return command;
        }

        private static SqlCommand GetIDFromStringCommand(SqlConnection connection, string code)
        {
            var command = new SqlCommand(
                    "\r\n DECLARE @out_id INT;\r\n" +
                    "EXECUTE [maroti].[sp_get_id]\r\n" +
                    "@in_codigo = @codigo\r\n" +
                    ",@out_id = @out_id OUTPUT;\r\n\r\n" +
                    "select @out_id;\r\n", connection);
            command.Parameters.AddWithValue("@codigo", code);
            return command;
        }
    }
}
