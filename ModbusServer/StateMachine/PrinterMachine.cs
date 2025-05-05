using log4net;
using mcOMRON;
using ModbusServer.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.CodeDom;

namespace ModbusServer.StateMachine
{
    internal class PrinterMachine : Machine
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        enum States
        {
            Init,
            RetreivingWeightLen,
            RetreivingWeight,
            SendWeightOk,
            WeightOk,
            RetreivingLabels,
            WaitPallet,
            WaitLabelInstruction,
            Print1,
            Print2,
            WaitPrinter,
            WaitPLCConfirmation,
            WaitLabelLost,
            WaitApplicatorReady,
            Reset1,
            Reset2,
            Skipped,
            Completed,
        }

        enum Label
        {
            LabelA,
            LabelB,
        }

        readonly OmronPLC plc;
        readonly NetworkPrinter printer;
        Task<SqlDatabase.Labels> retreivingTask;
        Task<ushort[]> omronReadingDataTask;
        Task<short> omronWaitingPallet;
        Task<bool> omronWritingTask;
        Task<bool> printerPrintTask;
        Task<byte> readingBitTask;
        bool shouldLabel = false;
        bool weightReady = false;
        string code;
        int packager;
        string keyname;
        bool labeling;
        Label currentLabel;
        int errorCount;

        public bool WeightOk
        {
            get
            {
                return (States)State >= States.WeightOk; 
            }
        }
        public PrinterMachine(OmronPLC plc, NetworkPrinter printer, string identifier) : base(States.Init, identifier)
        {
            this.plc = plc;
            this.printer = printer;
            this.keyname =  @"SOFTWARE\WencoInfo\" + identifier;
            this.packager = identifier == "Wolrdjet1" ? 1 : 2;
        }

        public override void Step()
        {
            switch (State)
            {
                case States.Init:
                    if (weightReady)
                    {
                        Log.Info("Pallet already weigth");
                        NextState(States.WeightOk);
                    }
                    else
                    {
                        Log.Info("Reading Weight");
                        omronReadingDataTask = plc.ReadDMs(40, 1);
                        NextState(States.RetreivingWeightLen);
                    }
                    break;
                case States.RetreivingWeightLen:
                    if (omronReadingDataTask.IsCompleted)
                    {
                        if (omronReadingDataTask.IsFaulted)
                        {
                            if(StateTime.ElapsedMilliseconds > 100)
                            {
                                Log.Error("Could not receive weight lenght. Retrying");
                                NextState(States.Init);
                            }
                        }
                        else
                        {
                            Log.InfoFormat("Weight lenght successfully acquired. Lenght: {0}", omronReadingDataTask.Result[0]);
                            NextState(States.RetreivingWeight);
                            omronReadingDataTask = plc.ReadDMs(44, 3);
                            Log.Info("Sending weight OK");
                        }
                    }
                    break;
                case States.RetreivingWeight:
                    if (omronReadingDataTask.IsCompleted)
                    {
                        if (omronReadingDataTask.IsFaulted)
                        {
                            if(StateTime.ElapsedMilliseconds > 100)
                            {
                                Log.Error("Could not receive weight. Retrying");
                                omronReadingDataTask = plc.ReadDMs(44, 3);
                                NextState(States.RetreivingWeight);
                            }
                        }
                        else
                        {
                            Log.InfoFormat("Weight successfully acquired. Weight: {0} Kgs.", GetWeight(omronReadingDataTask.Result));
                            RegistryKey key = Registry.LocalMachine.OpenSubKey(keyname, true);
                            if(key != null)
                            {
                                key.SetValue("CurrentWeight", GetWeight(omronReadingDataTask.Result));
                                key.Close();
                            }
                            else
                            {
                                RegistryKey newKey = Registry.LocalMachine.CreateSubKey(keyname);
                                newKey.SetValue("CurrentWeight", GetWeight(omronReadingDataTask.Result));
                                newKey.Close();
                            }
                            
                            NextState(States.SendWeightOk);
                            omronWritingTask = plc.WriteDM(27, 1);
                            Log.Info("Sending weight OK");
                        } 
                    }
                    break;
                case States.SendWeightOk:
                    if (omronWritingTask.IsCompleted)
                    {
                        if (omronWritingTask.Result)
                        {
                            Log.Info("PLC receive weight OK");
                            NextState(States.WeightOk);
                        }
                        else
                        {
                            Log.Error("PLC did not receive weight OK. Retrying");
                            NextState(States.RetreivingWeight);
                        }
                    }
                    break;
                case States.WeightOk:
                    if (shouldLabel)
                    {
                        NextState(States.RetreivingLabels);
                        int weight = 0;
                        using (RegistryKey key = Registry.LocalMachine.OpenSubKey(keyname))
                        {
                            if (key != null)
                            {
                                weight = (int)key.GetValue("CurrentWeight");
                                key.Close();
                            }
                        }
                        retreivingTask = SqlDatabase.AskForLabels(code, weight);
                    }
                    else
                    {
                        Log.Info("No label, waiting pallet");
                        NextState(States.WaitPallet);
                        omronWaitingPallet = plc.ReadDM(25);
                    }
                    break;
                case States.RetreivingLabels:
                    if (retreivingTask.IsCompleted)
                    {
                        if (retreivingTask.IsFaulted || retreivingTask.Result == null)
                        {
                            if(StateTime.ElapsedMilliseconds > 100)
                            {
                                Log.Error("Could not get the labels from the database. Retrying");
                                NextState(States.WeightOk);
                            }
                        }
                        else
                        {
                            Log.Info("Labels adquired successfully");
                            NextState(States.WaitPallet);
                            omronWaitingPallet = plc.ReadDM(25);
                            Log.Info("Waiting for pallet in applicator position");
                        }   
                    }
                    break;
                case States.WaitPallet:
                    if(omronWaitingPallet.IsCompleted)
                    {
                        if(omronWaitingPallet.Result == 1)
                        {
                            if (shouldLabel)
                            {
                                Log.Info("Pallet arrived to the applicator position");
                                omronReadingDataTask = plc.ReadDMs(10, 2);
                                NextState(States.WaitLabelInstruction);
                            }
                            else
                            {
                                Log.Info("Pallet without label");
                                NextState(States.Skipped);
                                omronWritingTask = plc.WriteDMs(10, new short[] {0, 0, 0, 0, 1} );
                            }
                        }
                        else
                        {
                            omronWaitingPallet = plc.ReadDM(25);
                        }
                    }
                    break;
                case States.WaitLabelInstruction:
                    if (omronReadingDataTask.IsCompleted)
                    {
                        if (omronReadingDataTask.IsFaulted)
                        {
                            Log.Error("Failed to read label instruction. Retrying");
                            omronReadingDataTask = plc.ReadDMs(10, 2);
                        }
                        else
                        {
                            if (omronReadingDataTask.Result[0] == 1)
                            {
                                errorCount= 0;
                                Log.Info("Receive order to print");
                                if (packager == 1)
                                {
                                    Status.Instance.ErrorMessages.BDC1Error = "";
                                }
                                else
                                {
                                    Status.Instance.ErrorMessages.BDC2Error = "";
                                }
                                NextState(States.Print1);
                            }
                            else
                            {
                                if (labeling)
                                {
                                    // Read if label lost

                                    readingBitTask = plc.ReadCIOBit(178, 0);
                                    NextState(States.WaitLabelLost);
                                }
                                else
                                {
                                    omronReadingDataTask = plc.ReadDMs(10, 2);
                                }
                                
                            }
                        }
                    }
                    break;
                case States.WaitLabelLost:
                    if (readingBitTask.IsCompleted)
                    {
                        if(readingBitTask.IsFaulted)
                        {
                            //Retry
                            readingBitTask = plc.ReadCIOBit(178, 0);
                        }
                        else
                        {
                            if (readingBitTask.Result == 1)
                            {
                                Log.Error("Label lost. Waiting applicator");
                                errorCount++;
                                if (errorCount < 3)
                                {
                                    readingBitTask = plc.ReadCIOBit(160, 6);
                                    NextState(States.WaitApplicatorReady);
                                }
                                else
                                {
                                    if(packager == 1)
                                    {
                                        Status.Instance.ErrorMessages.BDC1Error = "La etiquetadora no fue capaz de colocar la etiqueta. Imprimir una etiqueta manualmente con el botón FEED.";
                                    }
                                    else
                                    {
                                        Status.Instance.ErrorMessages.BDC2Error = "La etiquetadora no fue capaz de colocar la etiqueta. Imprimir una etiqueta manualmente con el botón FEED.";
                                    }
                                    labeling = false;
                                    _ = SqlDatabase.NotifyError(SqlDatabase.SystemErrors.timeout_etiquetado, code, packager);
                                    NextState(States.WaitLabelInstruction);
                                }
                            }
                            else
                            {
                                // Wait label instrucction
                                omronReadingDataTask = plc.ReadDMs(10, 2);
                                NextState(States.WaitLabelInstruction);
                            }
                        }
                    }
                    break;
                case States.WaitApplicatorReady:
                    if (readingBitTask.IsCompleted)
                    {
                        if (readingBitTask.Result == 0)
                        {
                            readingBitTask = plc.ReadCIOBit(160, 6);
                        }
                        else
                        {
                            Log.Info("Applicator ready");
                            omronWritingTask = plc.WriteCIOBit(161, 3, 1);
                            NextState(States.Reset1);
                        }
                    }
                    break;
                case States.Reset1:
                    if (omronWritingTask.IsCompleted)
                    {
                        if (omronWritingTask.Result)
                        {
                            if(StateTime.ElapsedMilliseconds > 220)
                            {
                                Log.Info("Reseting 1");
                                omronWritingTask = plc.WriteCIOBit(161, 3, 0);
                                NextState(States.Reset2);
                            }
                        }
                        else
                        {
                            // Retry
                            omronWritingTask = plc.WriteCIOBit(161, 3, 1);
                        }
                    }
                    break;
                case States.Reset2:
                    if (omronWritingTask.IsCompleted)
                    {
                        if (omronWritingTask.Result)
                        {
                            /*Log.Info("Wait label instruction");
                            omronReadingDataTask = plc.ReadDMs(10, 2);
                            NextState(States.WaitLabelInstruction);*/
                            NextState(States.Print2);
                        }
                        else
                        {
                            // Retry
                            omronWritingTask = plc.WriteCIOBit(161, 3, 0);
                        }
                    }
                    break;
                case States.Print1:
                    Log.Info("Labeling");
                    omronWritingTask = plc.WriteCIOBit(161, 3, 1);
                    labeling = true;
                    if (omronReadingDataTask.Result[1] == 1)
                    {
                        currentLabel = Label.LabelB;
                        NextState(States.Reset1);
                        break;
                    }

                    if (omronReadingDataTask.Result[1] == 2)
                    {
                        currentLabel = Label.LabelA;
                        NextState(States.Reset1);
                        break;
                    }
                    break;
                case States.Print2:
                    switch (currentLabel)
                    {
                        case Label.LabelA:
                            Log.Info("Printing label A");
                            printerPrintTask = printer.Print(retreivingTask.Result.ALabel);
                            
                            break;
                        case Label.LabelB:
                            Log.Info("Printing label B");
                            printerPrintTask = printer.Print(retreivingTask.Result.BLabel);
                            break;
                    }
                    NextState(States.WaitPrinter);
                    break;
                case States.WaitPrinter:
                    if(printerPrintTask.IsCompleted)
                    {
                        if (printerPrintTask.Result)
                        {
                            Log.Info("Print successfully");
                            omronWritingTask = plc.ClearDMs(10, 2);
                            NextState(States.WaitPLCConfirmation);
                        }
                        else
                        {
                            Log.Error("Printer did not receive label. Retrying");
                            NextState(States.Print2);
                        }
                    }
                    break;
                case States.WaitPLCConfirmation:
                    if (omronWritingTask.IsCompleted)
                    {
                        if (omronWritingTask.Result)
                        {
                            omronReadingDataTask = plc.ReadDMs(10, 2);
                            NextState(States.WaitLabelInstruction);
                            Log.Info("PLC received the print OK.");
                        }
                        else
                        {
                            Log.Error("PLC did not receive message. Retrying");
                            omronWritingTask = plc.ClearDMs(10, 2);
                        }
                    }
                    break;
                case States.Skipped:
                    if(omronWritingTask.IsCompleted)
                    {
                        if(omronWritingTask.Result)
                        {
                            Log.Info("PLC recieved the skip signal");
                            NextState(States.Completed);
                        }
                        else
                        {
                            Log.Error("PLC did not receive message. Retrying");
                            omronWritingTask = plc.WriteDMs(10, new short[] { 0, 0, 0, 0, 1 });
                        }
                    }
                    break;
                case States.Completed:
                    break;
            }
        }

        public void Reset(string code, bool shouldLabel = true, bool weightReady = false)
        {
            NextState(States.Init);
            errorCount = 0;
            this.code = code;
            this.shouldLabel = shouldLabel;
            this.weightReady = weightReady;
            this.labeling = false;
        }

        public int GetWeight(ushort[] values)
        {
            byte[] bytes = new byte[values.Length * 2];
            for(int i = 0; i < values.Length; i++)
            {
                byte[] byteValues = BitConverter.GetBytes(values[i]);
                bytes[i * 2] = byteValues[1];
                bytes[i * 2 + 1] = byteValues[0];
            }
            var weight = Encoding.UTF8.GetString(bytes);
            return (int)(Convert.ToDouble(weight.Insert(weight.Length - 1, ".")) * 1000);
        }
    }
}
