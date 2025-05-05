 using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using TcpHMIClient;
using static TcpHMIClient.HMIClient;

namespace InterfaceHMI
{
    public partial class HMIForm : Form
    {
        HMIClient hmiClient;
        List<Label> emb1HMIQueue;
        List<Label> emb2HMIQueue;
        HMIClient.SystemStatus currentStatus;
        private AutoResetEvent _resetEvent = new AutoResetEvent(false);

        public HMIForm()
        {
            InitializeComponent();
            hmiClient = new HMIClient(ClientWorker);
            emb1HMIQueue = new List<Label>()
            {
                emb1st2,
                emb1st3,
                emb1st4,
                emb1st5,
            };

            emb2HMIQueue = new List<Label>()
            {
                emb2st1,
                emb2st2,
                emb2st3,
                emb2st4,
                emb2st5,
            };
            tabControl1.SelectedIndex = 2;
        }

        private void CommunicateWithServer(object sender, DoWorkEventArgs e)
        {
            while (!ClientWorker.CancellationPending)
            {
                hmiClient.Step();
                Thread.Sleep(100);
            }
            Console.WriteLine("Terminating");
            hmiClient.Terminate();
            _resetEvent.Set();
        }

        private void UpdateHMI(object sender, ProgressChangedEventArgs e)
        {
            if (e.UserState != null)
            {
                SystemStatus newStatus = e.UserState as SystemStatus;

                if(currentStatus == null)
                {
                    currentStatus = newStatus;
                }

                UpdateConnections(newStatus.Connections);
                UpdatePalletEntryState(newStatus.MachineState);
                UpdateCar(newStatus.Car);
                UpdateMachieState(newStatus.MachineState);
                UpdatePalletEntryInfo(newStatus.EntryPallet);
                UpdateErrors(newStatus.ErrorMessages);

                if(newStatus.Packager1 != null)
                {
                    currentStatus.Packager1 = newStatus.Packager1;
                    UpdateQueue1(newStatus.Packager1, newStatus.Signals);
                }

                if (newStatus.Packager2 != null)
                {
                    currentStatus.Packager2 = newStatus.Packager2;
                    UpdateQueue2(newStatus.Packager2, newStatus.Signals);
                }

                if(newStatus.States != null)
                {
                    currentStatus.States = newStatus.States;
                    machineStateTable.Rows.Clear();
                    foreach (var key in currentStatus.States.Keys)
                    {
                        machineStateTable.Rows.Add(key, currentStatus.States[key][0]);
                    }
                }
            }
            else
            {
                ResetConnections();
            }
        }

        private void UpdateErrors(ErrorMessages errorMessages)
        {
            entryErrors.Text = errorMessages.EntryError;
            bcd1Errors.Text = errorMessages.BDC1Error;
            bcd2Errors.Text = errorMessages.BDC2Error;
            carErrors.Text = errorMessages.CarError;
        }

        private void UpdatePalletEntryInfo(Pallet entryPallet)
        {
            if(entryPallet != null)
            {
                if (entryPallet.Id != "0")
                {
                    entryPalletLabel.Text = $"{entryPallet.Qr}\nId: {entryPallet.Id}\nMaq: {entryPallet.Injector}";
                    enterEmb1.Text = $"{entryPallet.Qr}\nId: {entryPallet.Id}\nMaq: {entryPallet.Injector}";
                }
                else
                {
                    entryPalletLabel.Text = $"{entryPallet.Qr}";
                    enterEmb1.Text = $"{entryPallet.Qr}";
                }
            }
            else
            {
                entryPalletLabel.Text = "";
                enterEmb1.Text = "";
            }
        }

        private void UpdateMachieState(Dictionary<string, int> machineState)
        {
            if (currentStatus.States != null)
            {
                foreach (DataGridViewRow row in machineStateTable.Rows)
                {
                    var machineKey = row.Cells["process"].Value.ToString();
                    if (machineState.ContainsKey(machineKey))
                    {
                        row.Cells["state"].Value = currentStatus.States[machineKey][machineState[machineKey]];
                    }
                }
            }
        }

        private void UpdatePalletEntryState(Dictionary<string, int> machineState)
        {
            if (machineState.ContainsKey("PalletEntry"))
            {
                var state = (PalletEntryStates)machineState["PalletEntry"];
                switch (state)
                {
                    case PalletEntryStates.Waiting:
                        entryPalletLabel.Visible = false;
                        enterEmb1.Visible = false;
                        fCameraQrGood.Visible = true;
                        fCameraQrBad.Visible = false;
                        fLabelEnter2.TurnOff();
                        fLToBCDArrow.TurnOff();
                        fLToCarArrow.TurnOff();
                        fCameraQrBad.TurnOff();
                        fCameraQrGood.TurnOff();
                        break;
                    case PalletEntryStates.ReadingQR:
                        fLToBCDArrow.TurnOff();
                        fLToCarArrow.TurnOff();
                        fCameraQrGood.Blink();
                        fLabelEnter2.TurnOff();
                        entryPalletLabel.Visible = true;
                        enterEmb1.Visible = true;
                        break;
                    case PalletEntryStates.AskingDB:
                        fCameraQrGood.TurnOn();
                        fLToBCDArrow.TurnOff();
                        fLToCarArrow.TurnOff();
                        fCameraQrBad.TurnOff();
                        fLabelEnter2.TurnOff();
                        break;
                    case PalletEntryStates.WaitForBocedi1:
                        entryPalletLabel.Visible = true;
                        enterEmb1.Visible = true;
                        fCameraQrBad.TurnOff();
                        fCameraQrGood.TurnOn();
                        fLToCarArrow.TurnOff();
                        fLToBCDArrow.TurnOff();
                        fLabelEnter2.TurnOff();
                        break;
                    case PalletEntryStates.WaitEnterBocedi:
                        entryPalletLabel.Visible = true;
                        fCameraQrBad.TurnOff();
                        fCameraQrGood.TurnOn();
                        fLToCarArrow.TurnOff();
                        fLToBCDArrow.TurnOn();
                        fLabelEnter2.Blink();
                        break;
                    case PalletEntryStates.WaitForCar:
                        entryPalletLabel.Visible = true;
                        enterEmb1.Visible = true;
                        fCameraQrBad.TurnOff();
                        fCameraQrGood.TurnOn();
                        fLToCarArrow.Blink();
                        fLabelEnter2.TurnOff();
                        break;
                    case PalletEntryStates.WaitEnterCar:
                        entryPalletLabel.Visible = true;
                        enterEmb1.Visible = true;
                        fCameraQrBad.TurnOff();
                        fCameraQrGood.TurnOn();
                        fLToCarArrow.TurnOn();
                        fLabelEnter2.TurnOff();
                        break;
                    case PalletEntryStates.ReadingQrInError:
                        entryPalletLabel.Visible = true;
                        enterEmb1.Visible = true;
                        fCameraQrGood.Visible = false;
                        fCameraQrBad.Visible = true;
                        fCameraQrBad.Blink();
                        fLabelEnter2.TurnOff();
                        break;
                    case PalletEntryStates.UpdateCar:
                        entryPalletLabel.Visible = false;
                        enterEmb1.Visible = false;
                        fCameraQrBad.TurnOff();
                        fCameraQrGood.TurnOff();
                        fLabelEnter2.TurnOff();
                        fLToCarArrow.TurnOff();
                        fLToBCDArrow.TurnOff();
                        break;
                    case PalletEntryStates.Paused:
                        entryPalletLabel.Visible = false;
                        enterEmb1.Visible = false;
                        fCameraQrBad.TurnOff();
                        fCameraQrGood.Blink();
                        fLabelEnter2.TurnOff();
                        fLToCarArrow.Blink();
                        fLToBCDArrow.Blink();
                        break;

                }
            }
        }

        private void UpdateCar(Car car)
        {
            if (car != null)
            {
                switch (car.CarPosition)
                {
                    case Car.Position.Unknown:
                        panelCarInB2.Visible = false;
                        panelCarInB1.Visible = false;
                        tabCarB1.Visible = false;
                        tabCarB2.Visible = false;
                        tabCarNoPos.Visible = true;
                        fCarUnkown.Blink();
                        carToB1.TurnOff();
                        carToB2.TurnOff();
                        break;
                    case Car.Position.InB1:
                        panelCarInB2.Visible = false;
                        panelCarInB1.Visible = true;
                        tabCarB1.Visible = true;
                        tabCarB2.Visible = false;
                        tabCarNoPos.Visible = false;
                        fCarUnkown.TurnOff();
                        carToB1.TurnOff();
                        carToB2.TurnOff();
                        break;
                    case Car.Position.GoingToB2:
                        panelCarInB2.Visible = false;
                        panelCarInB1.Visible = false;
                        tabCarB1.Visible = false;
                        tabCarB2.Visible = false;
                        tabCarNoPos.Visible = true;
                        fCarUnkown.TurnOff();
                        carToB1.TurnOff();
                        carToB2.Blink();
                        break;
                    case Car.Position.InB2:
                        panelCarInB2.Visible = true;
                        panelCarInB1.Visible = false;
                        tabCarB1.Visible = false;
                        tabCarB2.Visible = true;
                        tabCarNoPos.Visible = false;
                        fCarUnkown.TurnOff();
                        carToB1.TurnOff();
                        carToB2.TurnOff();
                        break;
                    case Car.Position.GoingToB1:
                        panelCarInB2.Visible = false;
                        panelCarInB1.Visible = false;
                        tabCarB1.Visible = false;
                        tabCarB2.Visible = false;
                        tabCarNoPos.Visible = true;
                        fCarUnkown.TurnOff();
                        carToB1.Blink();
                        carToB2.TurnOff();
                        break;
                }

                if (car.HasPallet)
                {
                    palletInCarPos1.Visible = true;
                    palletInCarPos2.Visible = true;
                    tabPalletB1.Visible = true;
                    tabPalletB2.Visible = true;
                    tabPalletNoPos.Visible = true;

                    try
                    {
                        var text = $"{car.Pallet.Qr}\nId: {car.Pallet.Id}\nMaq: {car.Pallet.Injector}";

                        palletInCarPos1.Text = text;
                        palletInCarPos2.Text = text;
                        tabPalletB1.Text = text;
                        tabPalletB2.Text = text;
                        tabPalletNoPos.Text = text;
                    }
                    catch
                    {
                        Console.WriteLine("Failed to set car text");
                    }
                }
                else
                {
                    palletInCarPos1.Visible = false;
                    palletInCarPos2.Visible = false;
                    tabPalletB1.Visible = false;
                    tabPalletB2.Visible = false;
                    tabPalletNoPos.Visible = false;
                }
            }
        }

        private void UpdateQueue1(Packager packager, bool[] signals)
        {
            if (packager.Queue == null)
                return;

            foreach (var panel in emb1HMIQueue)
            {
                panel.Text = "";
                panel.Visible = false;
            }

            entryBCD1.Visible = false;
            entryBCD2.Visible = false;

            for (int i = 0; i < packager.Queue.Count && i < emb1HMIQueue.Count; i++)
            {
                var qr = packager.Queue[i].Qr;
                emb1HMIQueue[i].Text = $"{qr}\nId: {packager.Queue[i].Id}\nMaq: {packager.Queue[i].Injector}";
                emb1HMIQueue[i].Visible = true;
                emb1HMIQueue[i].BackColor = Color.Peru;

                if (i == 0)
                {
                    entryBCD1.Visible = true;
                    entryBCD1.Text = $"{qr}\nId: {packager.Queue[i].Id}\nMaq: {packager.Queue[i].Injector}";
                }

                if (i == 1)
                {
                    entryBCD2.Visible = true;
                    entryBCD2.Text = $"{qr}\nId: {packager.Queue[i].Id}\nMaq: {packager.Queue[i].Injector}";
                }

                

                if (i == packager.Queue.Count - 1)
                {
                    if (signals[(int)SignalsNames.WaitCorrection1])
                    {
                        emb1HMIQueue[i].BackColor = Color.LightCoral;
                    }
                }
            }

            labelingEmb1.Text = "";
            labelingEmb1.Visible = false;
            if (packager.LabelPallet != null)
            {
                labelingEmb1.Visible = true;
                labelingEmb1.Text = $"{packager.LabelPallet.Qr}\nId: {packager.LabelPallet.Id}\nMaq: {packager.LabelPallet.Injector}";
            }

            exitPallet1.Text = "";
            exitPallet1.Visible = false;
            if (packager.ExitPallet != null)
            {
                exitPallet1.Visible = true;
                exitPallet1.Text = $"{packager.ExitPallet.Qr}\nId: {packager.ExitPallet.Id}\nMaq: {packager.ExitPallet.Injector}";
            }
        }

        private void UpdateQueue2(Packager packager, bool[] signals)
        {
            if (packager.Queue == null)
                return;

            foreach (var panel in emb2HMIQueue)
            {
                panel.Text = "";
                panel.Visible = false;
            }

            for (int i = 0; i < packager.Queue.Count && i < emb2HMIQueue.Count; i++)
            {
                var qr = packager.Queue[i].Qr;
                emb2HMIQueue[i].Text = $"{qr}\nId: {packager.Queue[i].Id}\nMaq:{packager.Queue[i].Injector}";
                emb2HMIQueue[i].Visible = true;
                emb2HMIQueue[i].BackColor = Color.Peru;

                if (i == packager.Queue.Count - 1)
                {
                    if (signals[(int)SignalsNames.WaitCorrection2])
                    {
                        emb2HMIQueue[i].BackColor = Color.LightCoral;
                    }
                }
            }

            labelingEmb2.Text = "";
            labelingEmb2.Visible = false;
            if (packager.LabelPallet != null)
            {
                labelingEmb2.Visible = true;
                labelingEmb2.Text = $"{packager.LabelPallet.Qr}\nId: {packager.LabelPallet.Id}\nMaq: {packager.LabelPallet.Injector}";
            }

            exitPallet2.Text = "";
            exitPallet2.Visible = false;
            if (packager.ExitPallet != null)
            {
                exitPallet2.Visible = true;
                exitPallet2.Text = $"{packager.ExitPallet.Qr}\nId: {packager.ExitPallet.Id}\nMaq: {packager.ExitPallet.Injector}";
            }
        }

        private void UpdateConnections(HMIClient.Connections connections)
        {
            pcIndustStatus1.BackColor = pcIndustStatus2.BackColor = Color.Green;
            masterPLCStatus1.BackColor = masterPLCStatus2.BackColor = connections.MasterPLC ? Color.Green : Color.Red;
            slavePLCStatus2.BackColor = connections.SlavePLC? Color.Green : Color.Red;
            erpStatus1.BackColor = erpStatus2.BackColor = connections.WencoDB ? Color.Green : Color.Red;
            emb1Status1.BackColor = connections.Packager1 ? Color.Green : Color.Red;
            emb2Status2.BackColor = connections.Packager2 ? Color.Green : Color.Red;
            labelerStatus1.BackColor = connections.Labeler1 ? Color.Green : Color.Red;
            labelerStatus2.BackColor = connections.Labeler2 ? Color.Green : Color.Red;
            qrStatus1.BackColor = connections.QrReader ? Color.Green : Color.Red;
        }

        private void ResetConnections()
        {
            pcIndustStatus1.BackColor = pcIndustStatus2.BackColor = Color.Red;
            masterPLCStatus1.BackColor = masterPLCStatus2.BackColor = Color.White;
            slavePLCStatus2.BackColor = Color.White;
            erpStatus1.BackColor = erpStatus2.BackColor = Color.White;
            emb1Status1.BackColor = Color.White;
            emb2Status2.BackColor = Color.White;
            labelerStatus1.BackColor = Color.White;
            labelerStatus2.BackColor = Color.White;
        }

        private void HMIForm_Load(object sender, EventArgs e)
        {
            ClientWorker.RunWorkerAsync();
        }

        private void ShowDetails(object sender, EventArgs e)
        {
            var label = sender as Label;

            if (emb1HMIQueue.Contains(label))
            {
                var index = emb1HMIQueue.IndexOf(label);

                var dialog = new PalletDetail();

                if(currentStatus != null)
                {
                    dialog.Init(1, index, currentStatus.Packager1.Queue[index] , hmiClient);
                    Console.WriteLine(dialog.ShowDialog());
                }
                return;
            }

            if (emb2HMIQueue.Contains(label))
            {
                var index = emb2HMIQueue.IndexOf(label);

                var dialog = new PalletDetail();

                if (currentStatus != null)
                {
                    dialog.Init(2, index, currentStatus.Packager2.Queue[index], hmiClient);
                    Console.WriteLine(dialog.ShowDialog());
                }
                return;
            }
        }

        private void StartClosing(object sender, FormClosingEventArgs e)
        {
            ClientWorker.CancelAsync();
            _resetEvent.WaitOne(1000);
        }
    }
}
