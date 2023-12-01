namespace InterfaceHMI
{
    partial class HMIForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(HMIForm));
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.emb2Tab = new System.Windows.Forms.TabPage();
            this.groupBox8 = new System.Windows.Forms.GroupBox();
            this.erpStatus2 = new System.Windows.Forms.Label();
            this.labelerStatus2 = new System.Windows.Forms.Label();
            this.slavePLCStatus2 = new System.Windows.Forms.Label();
            this.masterPLCStatus2 = new System.Windows.Forms.Label();
            this.emb2Status2 = new System.Windows.Forms.Label();
            this.pcIndustStatus2 = new System.Windows.Forms.Label();
            this.groupBox6 = new System.Windows.Forms.GroupBox();
            this.bcd2Errors = new System.Windows.Forms.Label();
            this.groupBox5 = new System.Windows.Forms.GroupBox();
            this.exitPallet2 = new System.Windows.Forms.Label();
            this.labelingEmb2 = new System.Windows.Forms.Label();
            this.panelCarInB2 = new System.Windows.Forms.Panel();
            this.palletInCarPos2 = new System.Windows.Forms.Label();
            this.emb2st2 = new System.Windows.Forms.Label();
            this.emb2st4 = new System.Windows.Forms.Label();
            this.emb2st5 = new System.Windows.Forms.Label();
            this.emb2st3 = new System.Windows.Forms.Label();
            this.emb2st1 = new System.Windows.Forms.Label();
            this.carTab = new System.Windows.Forms.TabPage();
            this.groupBox7 = new System.Windows.Forms.GroupBox();
            this.carErrors = new System.Windows.Forms.Label();
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.tabCarNoPos = new System.Windows.Forms.Panel();
            this.tabPalletNoPos = new System.Windows.Forms.Label();
            this.tabCarB1 = new System.Windows.Forms.Panel();
            this.tabPalletB1 = new System.Windows.Forms.Label();
            this.tabCarB2 = new System.Windows.Forms.Panel();
            this.tabPalletB2 = new System.Windows.Forms.Label();
            this.label30 = new System.Windows.Forms.Label();
            this.palletEntryTab = new System.Windows.Forms.TabPage();
            this.groupBox10 = new System.Windows.Forms.GroupBox();
            this.entryErrors = new System.Windows.Forms.Label();
            this.groupBox9 = new System.Windows.Forms.GroupBox();
            this.entryBCD2 = new System.Windows.Forms.Label();
            this.entryBCD1 = new System.Windows.Forms.Label();
            this.entryPalletLabel = new System.Windows.Forms.Label();
            this.panelCarInB1 = new System.Windows.Forms.Panel();
            this.palletInCarPos1 = new System.Windows.Forms.Label();
            this.emb1Tab = new System.Windows.Forms.TabPage();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.bcd1Errors = new System.Windows.Forms.Label();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.labelerStatus1 = new System.Windows.Forms.Label();
            this.erpStatus1 = new System.Windows.Forms.Label();
            this.emb1Status1 = new System.Windows.Forms.Label();
            this.qrStatus1 = new System.Windows.Forms.Label();
            this.masterPLCStatus1 = new System.Windows.Forms.Label();
            this.pcIndustStatus1 = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.exitPallet1 = new System.Windows.Forms.Label();
            this.labelingEmb1 = new System.Windows.Forms.Label();
            this.enterEmb1 = new System.Windows.Forms.Label();
            this.emb1st5 = new System.Windows.Forms.Label();
            this.emb1st4 = new System.Windows.Forms.Label();
            this.emb1st3 = new System.Windows.Forms.Label();
            this.emb1st2 = new System.Windows.Forms.Label();
            this.statesTab = new System.Windows.Forms.TabPage();
            this.machineStateTable = new System.Windows.Forms.DataGridView();
            this.process = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.state = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ClientWorker = new System.ComponentModel.BackgroundWorker();
            this.carToB1 = new TcpHMIClient.FLabel();
            this.carToB2 = new TcpHMIClient.FLabel();
            this.fCarUnkown = new TcpHMIClient.FLabel();
            this.fCameraQrGood = new TcpHMIClient.FLabel();
            this.fCameraQrBad = new TcpHMIClient.FLabel();
            this.fLToCarArrow = new TcpHMIClient.FLabel();
            this.fLToBCDArrow = new TcpHMIClient.FLabel();
            this.fLabelEnter2 = new TcpHMIClient.FLabel();
            this.tabControl1.SuspendLayout();
            this.emb2Tab.SuspendLayout();
            this.groupBox8.SuspendLayout();
            this.groupBox6.SuspendLayout();
            this.groupBox5.SuspendLayout();
            this.panelCarInB2.SuspendLayout();
            this.carTab.SuspendLayout();
            this.groupBox7.SuspendLayout();
            this.groupBox4.SuspendLayout();
            this.tabCarNoPos.SuspendLayout();
            this.tabCarB1.SuspendLayout();
            this.tabCarB2.SuspendLayout();
            this.palletEntryTab.SuspendLayout();
            this.groupBox10.SuspendLayout();
            this.groupBox9.SuspendLayout();
            this.panelCarInB1.SuspendLayout();
            this.emb1Tab.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.statesTab.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.machineStateTable)).BeginInit();
            this.SuspendLayout();
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.emb2Tab);
            this.tabControl1.Controls.Add(this.carTab);
            this.tabControl1.Controls.Add(this.palletEntryTab);
            this.tabControl1.Controls.Add(this.emb1Tab);
            this.tabControl1.Controls.Add(this.statesTab);
            this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl1.ItemSize = new System.Drawing.Size(350, 35);
            this.tabControl1.Location = new System.Drawing.Point(0, 0);
            this.tabControl1.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(803, 562);
            this.tabControl1.TabIndex = 2;
            // 
            // emb2Tab
            // 
            this.emb2Tab.Controls.Add(this.groupBox8);
            this.emb2Tab.Controls.Add(this.groupBox6);
            this.emb2Tab.Controls.Add(this.groupBox5);
            this.emb2Tab.Location = new System.Drawing.Point(4, 39);
            this.emb2Tab.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.emb2Tab.Name = "emb2Tab";
            this.emb2Tab.Padding = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.emb2Tab.Size = new System.Drawing.Size(795, 519);
            this.emb2Tab.TabIndex = 1;
            this.emb2Tab.Text = "Embolsadora 2";
            this.emb2Tab.UseVisualStyleBackColor = true;
            // 
            // groupBox8
            // 
            this.groupBox8.Controls.Add(this.erpStatus2);
            this.groupBox8.Controls.Add(this.labelerStatus2);
            this.groupBox8.Controls.Add(this.slavePLCStatus2);
            this.groupBox8.Controls.Add(this.masterPLCStatus2);
            this.groupBox8.Controls.Add(this.emb2Status2);
            this.groupBox8.Controls.Add(this.pcIndustStatus2);
            this.groupBox8.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBox8.Location = new System.Drawing.Point(410, 332);
            this.groupBox8.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.groupBox8.Name = "groupBox8";
            this.groupBox8.Padding = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.groupBox8.Size = new System.Drawing.Size(382, 185);
            this.groupBox8.TabIndex = 4;
            this.groupBox8.TabStop = false;
            this.groupBox8.Text = "Conexiones";
            // 
            // erpStatus2
            // 
            this.erpStatus2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.erpStatus2.Location = new System.Drawing.Point(54, 132);
            this.erpStatus2.Name = "erpStatus2";
            this.erpStatus2.Size = new System.Drawing.Size(126, 32);
            this.erpStatus2.TabIndex = 1;
            this.erpStatus2.Text = "ERP Wenco";
            this.erpStatus2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // labelerStatus2
            // 
            this.labelerStatus2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.labelerStatus2.Location = new System.Drawing.Point(206, 132);
            this.labelerStatus2.Name = "labelerStatus2";
            this.labelerStatus2.Size = new System.Drawing.Size(126, 32);
            this.labelerStatus2.TabIndex = 6;
            this.labelerStatus2.Text = "Impresora";
            this.labelerStatus2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // slavePLCStatus2
            // 
            this.slavePLCStatus2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.slavePLCStatus2.Location = new System.Drawing.Point(206, 85);
            this.slavePLCStatus2.Name = "slavePLCStatus2";
            this.slavePLCStatus2.Size = new System.Drawing.Size(126, 32);
            this.slavePLCStatus2.TabIndex = 7;
            this.slavePLCStatus2.Text = "PLC esclavo";
            this.slavePLCStatus2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // masterPLCStatus2
            // 
            this.masterPLCStatus2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.masterPLCStatus2.Location = new System.Drawing.Point(54, 85);
            this.masterPLCStatus2.Name = "masterPLCStatus2";
            this.masterPLCStatus2.Size = new System.Drawing.Size(126, 32);
            this.masterPLCStatus2.TabIndex = 4;
            this.masterPLCStatus2.Text = "PLC Maestro";
            this.masterPLCStatus2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // emb2Status2
            // 
            this.emb2Status2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.emb2Status2.Location = new System.Drawing.Point(206, 41);
            this.emb2Status2.Name = "emb2Status2";
            this.emb2Status2.Size = new System.Drawing.Size(126, 32);
            this.emb2Status2.TabIndex = 5;
            this.emb2Status2.Text = "Embolsadora";
            this.emb2Status2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // pcIndustStatus2
            // 
            this.pcIndustStatus2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pcIndustStatus2.Location = new System.Drawing.Point(54, 41);
            this.pcIndustStatus2.Name = "pcIndustStatus2";
            this.pcIndustStatus2.Size = new System.Drawing.Size(126, 32);
            this.pcIndustStatus2.TabIndex = 3;
            this.pcIndustStatus2.Text = "PC Industrial";
            this.pcIndustStatus2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // groupBox6
            // 
            this.groupBox6.Controls.Add(this.bcd2Errors);
            this.groupBox6.Dock = System.Windows.Forms.DockStyle.Left;
            this.groupBox6.Location = new System.Drawing.Point(3, 332);
            this.groupBox6.Margin = new System.Windows.Forms.Padding(27, 24, 27, 24);
            this.groupBox6.Name = "groupBox6";
            this.groupBox6.Padding = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.groupBox6.Size = new System.Drawing.Size(407, 185);
            this.groupBox6.TabIndex = 2;
            this.groupBox6.TabStop = false;
            this.groupBox6.Text = "Alarmas";
            // 
            // bcd2Errors
            // 
            this.bcd2Errors.BackColor = System.Drawing.Color.White;
            this.bcd2Errors.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.bcd2Errors.Font = new System.Drawing.Font("Consolas", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.bcd2Errors.Location = new System.Drawing.Point(6, 19);
            this.bcd2Errors.Name = "bcd2Errors";
            this.bcd2Errors.Size = new System.Drawing.Size(395, 160);
            this.bcd2Errors.TabIndex = 23;
            this.bcd2Errors.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // groupBox5
            // 
            this.groupBox5.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("groupBox5.BackgroundImage")));
            this.groupBox5.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.groupBox5.Controls.Add(this.exitPallet2);
            this.groupBox5.Controls.Add(this.labelingEmb2);
            this.groupBox5.Controls.Add(this.panelCarInB2);
            this.groupBox5.Controls.Add(this.emb2st2);
            this.groupBox5.Controls.Add(this.emb2st4);
            this.groupBox5.Controls.Add(this.emb2st5);
            this.groupBox5.Controls.Add(this.emb2st3);
            this.groupBox5.Controls.Add(this.emb2st1);
            this.groupBox5.Dock = System.Windows.Forms.DockStyle.Top;
            this.groupBox5.Location = new System.Drawing.Point(3, 2);
            this.groupBox5.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.groupBox5.Name = "groupBox5";
            this.groupBox5.Padding = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.groupBox5.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.groupBox5.Size = new System.Drawing.Size(789, 330);
            this.groupBox5.TabIndex = 1;
            this.groupBox5.TabStop = false;
            this.groupBox5.Text = "Estado actual";
            // 
            // exitPallet2
            // 
            this.exitPallet2.BackColor = System.Drawing.Color.Peru;
            this.exitPallet2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.exitPallet2.Font = new System.Drawing.Font("Consolas", 7.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.exitPallet2.Location = new System.Drawing.Point(40, 10);
            this.exitPallet2.Name = "exitPallet2";
            this.exitPallet2.Size = new System.Drawing.Size(85, 85);
            this.exitPallet2.TabIndex = 27;
            this.exitPallet2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // labelingEmb2
            // 
            this.labelingEmb2.BackColor = System.Drawing.Color.Peru;
            this.labelingEmb2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.labelingEmb2.Font = new System.Drawing.Font("Consolas", 7.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelingEmb2.Location = new System.Drawing.Point(40, 152);
            this.labelingEmb2.Name = "labelingEmb2";
            this.labelingEmb2.Size = new System.Drawing.Size(85, 85);
            this.labelingEmb2.TabIndex = 26;
            this.labelingEmb2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // panelCarInB2
            // 
            this.panelCarInB2.BackColor = System.Drawing.Color.Transparent;
            this.panelCarInB2.BackgroundImage = global::TcpHMIClient.Properties.Resources.CarrInPos1;
            this.panelCarInB2.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.panelCarInB2.Controls.Add(this.palletInCarPos2);
            this.panelCarInB2.Location = new System.Drawing.Point(677, 137);
            this.panelCarInB2.Name = "panelCarInB2";
            this.panelCarInB2.Size = new System.Drawing.Size(110, 110);
            this.panelCarInB2.TabIndex = 25;
            // 
            // palletInCarPos2
            // 
            this.palletInCarPos2.BackColor = System.Drawing.Color.Peru;
            this.palletInCarPos2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.palletInCarPos2.Font = new System.Drawing.Font("Consolas", 7.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.palletInCarPos2.Location = new System.Drawing.Point(12, 12);
            this.palletInCarPos2.Name = "palletInCarPos2";
            this.palletInCarPos2.Size = new System.Drawing.Size(85, 85);
            this.palletInCarPos2.TabIndex = 22;
            this.palletInCarPos2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // emb2st2
            // 
            this.emb2st2.BackColor = System.Drawing.Color.Peru;
            this.emb2st2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.emb2st2.Font = new System.Drawing.Font("Consolas", 7.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.emb2st2.Location = new System.Drawing.Point(462, 152);
            this.emb2st2.Name = "emb2st2";
            this.emb2st2.Size = new System.Drawing.Size(85, 85);
            this.emb2st2.TabIndex = 5;
            this.emb2st2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.emb2st2.Click += new System.EventHandler(this.ShowDetails);
            // 
            // emb2st4
            // 
            this.emb2st4.BackColor = System.Drawing.Color.Peru;
            this.emb2st4.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.emb2st4.Font = new System.Drawing.Font("Consolas", 7.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.emb2st4.Location = new System.Drawing.Point(269, 152);
            this.emb2st4.Name = "emb2st4";
            this.emb2st4.Size = new System.Drawing.Size(85, 85);
            this.emb2st4.TabIndex = 4;
            this.emb2st4.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.emb2st4.Click += new System.EventHandler(this.ShowDetails);
            // 
            // emb2st5
            // 
            this.emb2st5.BackColor = System.Drawing.Color.Peru;
            this.emb2st5.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.emb2st5.Font = new System.Drawing.Font("Consolas", 7.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.emb2st5.Location = new System.Drawing.Point(174, 152);
            this.emb2st5.Name = "emb2st5";
            this.emb2st5.Size = new System.Drawing.Size(85, 85);
            this.emb2st5.TabIndex = 3;
            this.emb2st5.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.emb2st5.Click += new System.EventHandler(this.ShowDetails);
            // 
            // emb2st3
            // 
            this.emb2st3.BackColor = System.Drawing.Color.Peru;
            this.emb2st3.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.emb2st3.Font = new System.Drawing.Font("Consolas", 7.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.emb2st3.Location = new System.Drawing.Point(366, 152);
            this.emb2st3.Name = "emb2st3";
            this.emb2st3.Size = new System.Drawing.Size(85, 85);
            this.emb2st3.TabIndex = 2;
            this.emb2st3.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.emb2st3.Click += new System.EventHandler(this.ShowDetails);
            // 
            // emb2st1
            // 
            this.emb2st1.BackColor = System.Drawing.Color.Peru;
            this.emb2st1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.emb2st1.Font = new System.Drawing.Font("Consolas", 7.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.emb2st1.Location = new System.Drawing.Point(560, 152);
            this.emb2st1.Name = "emb2st1";
            this.emb2st1.Size = new System.Drawing.Size(85, 85);
            this.emb2st1.TabIndex = 0;
            this.emb2st1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.emb2st1.Click += new System.EventHandler(this.ShowDetails);
            // 
            // carTab
            // 
            this.carTab.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.carTab.Controls.Add(this.groupBox7);
            this.carTab.Controls.Add(this.groupBox4);
            this.carTab.Location = new System.Drawing.Point(4, 39);
            this.carTab.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.carTab.Name = "carTab";
            this.carTab.Padding = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.carTab.Size = new System.Drawing.Size(795, 519);
            this.carTab.TabIndex = 4;
            this.carTab.Text = "Carro";
            this.carTab.UseVisualStyleBackColor = true;
            // 
            // groupBox7
            // 
            this.groupBox7.Controls.Add(this.carErrors);
            this.groupBox7.Dock = System.Windows.Forms.DockStyle.Left;
            this.groupBox7.Location = new System.Drawing.Point(3, 403);
            this.groupBox7.Name = "groupBox7";
            this.groupBox7.Size = new System.Drawing.Size(1064, 114);
            this.groupBox7.TabIndex = 23;
            this.groupBox7.TabStop = false;
            this.groupBox7.Text = "Alarmas";
            // 
            // carErrors
            // 
            this.carErrors.BackColor = System.Drawing.Color.White;
            this.carErrors.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.carErrors.Font = new System.Drawing.Font("Consolas", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.carErrors.Location = new System.Drawing.Point(5, 18);
            this.carErrors.Name = "carErrors";
            this.carErrors.Size = new System.Drawing.Size(781, 96);
            this.carErrors.TabIndex = 25;
            this.carErrors.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // groupBox4
            // 
            this.groupBox4.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("groupBox4.BackgroundImage")));
            this.groupBox4.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.groupBox4.Controls.Add(this.tabCarNoPos);
            this.groupBox4.Controls.Add(this.tabCarB1);
            this.groupBox4.Controls.Add(this.tabCarB2);
            this.groupBox4.Controls.Add(this.carToB1);
            this.groupBox4.Controls.Add(this.carToB2);
            this.groupBox4.Controls.Add(this.label30);
            this.groupBox4.Controls.Add(this.fCarUnkown);
            this.groupBox4.Dock = System.Windows.Forms.DockStyle.Top;
            this.groupBox4.Location = new System.Drawing.Point(3, 2);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.Size = new System.Drawing.Size(789, 401);
            this.groupBox4.TabIndex = 22;
            this.groupBox4.TabStop = false;
            this.groupBox4.Text = "Estado del carro";
            // 
            // tabCarNoPos
            // 
            this.tabCarNoPos.BackColor = System.Drawing.Color.Transparent;
            this.tabCarNoPos.BackgroundImage = global::TcpHMIClient.Properties.Resources.CarrInPos1;
            this.tabCarNoPos.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.tabCarNoPos.Controls.Add(this.tabPalletNoPos);
            this.tabCarNoPos.Location = new System.Drawing.Point(303, 120);
            this.tabCarNoPos.Name = "tabCarNoPos";
            this.tabCarNoPos.Size = new System.Drawing.Size(163, 163);
            this.tabCarNoPos.TabIndex = 26;
            // 
            // tabPalletNoPos
            // 
            this.tabPalletNoPos.BackColor = System.Drawing.Color.Peru;
            this.tabPalletNoPos.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.tabPalletNoPos.Location = new System.Drawing.Point(18, 17);
            this.tabPalletNoPos.Name = "tabPalletNoPos";
            this.tabPalletNoPos.Size = new System.Drawing.Size(128, 128);
            this.tabPalletNoPos.TabIndex = 22;
            this.tabPalletNoPos.Text = " ";
            this.tabPalletNoPos.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // tabCarB1
            // 
            this.tabCarB1.BackColor = System.Drawing.Color.Transparent;
            this.tabCarB1.BackgroundImage = global::TcpHMIClient.Properties.Resources.CarrInPos1;
            this.tabCarB1.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.tabCarB1.Controls.Add(this.tabPalletB1);
            this.tabCarB1.Location = new System.Drawing.Point(607, 120);
            this.tabCarB1.Name = "tabCarB1";
            this.tabCarB1.Size = new System.Drawing.Size(163, 163);
            this.tabCarB1.TabIndex = 26;
            // 
            // tabPalletB1
            // 
            this.tabPalletB1.BackColor = System.Drawing.Color.Peru;
            this.tabPalletB1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.tabPalletB1.Location = new System.Drawing.Point(17, 18);
            this.tabPalletB1.Name = "tabPalletB1";
            this.tabPalletB1.Size = new System.Drawing.Size(128, 128);
            this.tabPalletB1.TabIndex = 22;
            this.tabPalletB1.Text = " ";
            this.tabPalletB1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // tabCarB2
            // 
            this.tabCarB2.BackColor = System.Drawing.Color.Transparent;
            this.tabCarB2.BackgroundImage = global::TcpHMIClient.Properties.Resources.CarrInPos1;
            this.tabCarB2.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.tabCarB2.Controls.Add(this.tabPalletB2);
            this.tabCarB2.Location = new System.Drawing.Point(19, 120);
            this.tabCarB2.Name = "tabCarB2";
            this.tabCarB2.Size = new System.Drawing.Size(163, 163);
            this.tabCarB2.TabIndex = 25;
            // 
            // tabPalletB2
            // 
            this.tabPalletB2.BackColor = System.Drawing.Color.Peru;
            this.tabPalletB2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.tabPalletB2.Location = new System.Drawing.Point(18, 17);
            this.tabPalletB2.Name = "tabPalletB2";
            this.tabPalletB2.Size = new System.Drawing.Size(128, 128);
            this.tabPalletB2.TabIndex = 22;
            this.tabPalletB2.Text = " ";
            this.tabPalletB2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label30
            // 
            this.label30.AutoSize = true;
            this.label30.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label30.Location = new System.Drawing.Point(380, 355);
            this.label30.Name = "label30";
            this.label30.Size = new System.Drawing.Size(107, 20);
            this.label30.TabIndex = 22;
            this.label30.Text = "Desconocido";
            // 
            // palletEntryTab
            // 
            this.palletEntryTab.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.palletEntryTab.Controls.Add(this.groupBox10);
            this.palletEntryTab.Controls.Add(this.groupBox9);
            this.palletEntryTab.Location = new System.Drawing.Point(4, 39);
            this.palletEntryTab.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.palletEntryTab.Name = "palletEntryTab";
            this.palletEntryTab.Padding = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.palletEntryTab.Size = new System.Drawing.Size(795, 519);
            this.palletEntryTab.TabIndex = 3;
            this.palletEntryTab.Text = "Entrada Pallets";
            this.palletEntryTab.UseVisualStyleBackColor = true;
            // 
            // groupBox10
            // 
            this.groupBox10.Controls.Add(this.entryErrors);
            this.groupBox10.Dock = System.Windows.Forms.DockStyle.Left;
            this.groupBox10.Location = new System.Drawing.Point(3, 420);
            this.groupBox10.Name = "groupBox10";
            this.groupBox10.Size = new System.Drawing.Size(1062, 97);
            this.groupBox10.TabIndex = 31;
            this.groupBox10.TabStop = false;
            this.groupBox10.Text = "Alarmas";
            // 
            // entryErrors
            // 
            this.entryErrors.BackColor = System.Drawing.Color.White;
            this.entryErrors.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.entryErrors.Font = new System.Drawing.Font("Consolas", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.entryErrors.Location = new System.Drawing.Point(6, 18);
            this.entryErrors.Name = "entryErrors";
            this.entryErrors.Size = new System.Drawing.Size(778, 79);
            this.entryErrors.TabIndex = 24;
            this.entryErrors.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // groupBox9
            // 
            this.groupBox9.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("groupBox9.BackgroundImage")));
            this.groupBox9.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.groupBox9.Controls.Add(this.fCameraQrGood);
            this.groupBox9.Controls.Add(this.fCameraQrBad);
            this.groupBox9.Controls.Add(this.entryBCD2);
            this.groupBox9.Controls.Add(this.entryBCD1);
            this.groupBox9.Controls.Add(this.entryPalletLabel);
            this.groupBox9.Controls.Add(this.panelCarInB1);
            this.groupBox9.Controls.Add(this.fLToCarArrow);
            this.groupBox9.Controls.Add(this.fLToBCDArrow);
            this.groupBox9.Dock = System.Windows.Forms.DockStyle.Top;
            this.groupBox9.Location = new System.Drawing.Point(3, 2);
            this.groupBox9.Name = "groupBox9";
            this.groupBox9.Size = new System.Drawing.Size(789, 418);
            this.groupBox9.TabIndex = 30;
            this.groupBox9.TabStop = false;
            this.groupBox9.Text = "Ingreso de pallets";
            // 
            // entryBCD2
            // 
            this.entryBCD2.BackColor = System.Drawing.Color.Peru;
            this.entryBCD2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.entryBCD2.Font = new System.Drawing.Font("Consolas", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.entryBCD2.Location = new System.Drawing.Point(720, 151);
            this.entryBCD2.Name = "entryBCD2";
            this.entryBCD2.Size = new System.Drawing.Size(150, 150);
            this.entryBCD2.TabIndex = 37;
            this.entryBCD2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // entryBCD1
            // 
            this.entryBCD1.BackColor = System.Drawing.Color.Peru;
            this.entryBCD1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.entryBCD1.Font = new System.Drawing.Font("Consolas", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.entryBCD1.Location = new System.Drawing.Point(555, 151);
            this.entryBCD1.Name = "entryBCD1";
            this.entryBCD1.Size = new System.Drawing.Size(150, 150);
            this.entryBCD1.TabIndex = 36;
            this.entryBCD1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // entryPalletLabel
            // 
            this.entryPalletLabel.BackColor = System.Drawing.Color.Peru;
            this.entryPalletLabel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.entryPalletLabel.Font = new System.Drawing.Font("Consolas", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.entryPalletLabel.Location = new System.Drawing.Point(320, 151);
            this.entryPalletLabel.Name = "entryPalletLabel";
            this.entryPalletLabel.Size = new System.Drawing.Size(150, 150);
            this.entryPalletLabel.TabIndex = 35;
            this.entryPalletLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // panelCarInB1
            // 
            this.panelCarInB1.BackColor = System.Drawing.Color.Transparent;
            this.panelCarInB1.BackgroundImage = global::TcpHMIClient.Properties.Resources.CarrInPos1;
            this.panelCarInB1.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.panelCarInB1.Controls.Add(this.palletInCarPos1);
            this.panelCarInB1.Location = new System.Drawing.Point(41, 128);
            this.panelCarInB1.Name = "panelCarInB1";
            this.panelCarInB1.Size = new System.Drawing.Size(200, 200);
            this.panelCarInB1.TabIndex = 34;
            // 
            // palletInCarPos1
            // 
            this.palletInCarPos1.BackColor = System.Drawing.Color.Peru;
            this.palletInCarPos1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.palletInCarPos1.Font = new System.Drawing.Font("Consolas", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.palletInCarPos1.Location = new System.Drawing.Point(26, 23);
            this.palletInCarPos1.Name = "palletInCarPos1";
            this.palletInCarPos1.Size = new System.Drawing.Size(150, 150);
            this.palletInCarPos1.TabIndex = 22;
            this.palletInCarPos1.Text = " ";
            this.palletInCarPos1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // emb1Tab
            // 
            this.emb1Tab.Controls.Add(this.groupBox3);
            this.emb1Tab.Controls.Add(this.groupBox2);
            this.emb1Tab.Controls.Add(this.groupBox1);
            this.emb1Tab.Location = new System.Drawing.Point(4, 39);
            this.emb1Tab.Margin = new System.Windows.Forms.Padding(9, 8, 9, 8);
            this.emb1Tab.Name = "emb1Tab";
            this.emb1Tab.Padding = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.emb1Tab.Size = new System.Drawing.Size(795, 519);
            this.emb1Tab.TabIndex = 0;
            this.emb1Tab.Text = "Embolsadora 1";
            this.emb1Tab.UseVisualStyleBackColor = true;
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.bcd1Errors);
            this.groupBox3.Dock = System.Windows.Forms.DockStyle.Left;
            this.groupBox3.Location = new System.Drawing.Point(385, 337);
            this.groupBox3.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Padding = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.groupBox3.Size = new System.Drawing.Size(679, 180);
            this.groupBox3.TabIndex = 2;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Alarmas";
            // 
            // bcd1Errors
            // 
            this.bcd1Errors.BackColor = System.Drawing.Color.White;
            this.bcd1Errors.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.bcd1Errors.Font = new System.Drawing.Font("Consolas", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.bcd1Errors.Location = new System.Drawing.Point(12, 17);
            this.bcd1Errors.Name = "bcd1Errors";
            this.bcd1Errors.Size = new System.Drawing.Size(390, 155);
            this.bcd1Errors.TabIndex = 24;
            this.bcd1Errors.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.labelerStatus1);
            this.groupBox2.Controls.Add(this.erpStatus1);
            this.groupBox2.Controls.Add(this.emb1Status1);
            this.groupBox2.Controls.Add(this.qrStatus1);
            this.groupBox2.Controls.Add(this.masterPLCStatus1);
            this.groupBox2.Controls.Add(this.pcIndustStatus1);
            this.groupBox2.Dock = System.Windows.Forms.DockStyle.Left;
            this.groupBox2.Location = new System.Drawing.Point(3, 337);
            this.groupBox2.Margin = new System.Windows.Forms.Padding(27, 24, 27, 24);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Padding = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.groupBox2.Size = new System.Drawing.Size(382, 180);
            this.groupBox2.TabIndex = 1;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Conexiones";
            // 
            // labelerStatus1
            // 
            this.labelerStatus1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.labelerStatus1.Location = new System.Drawing.Point(210, 90);
            this.labelerStatus1.Name = "labelerStatus1";
            this.labelerStatus1.Size = new System.Drawing.Size(126, 32);
            this.labelerStatus1.TabIndex = 6;
            this.labelerStatus1.Text = "Impresora";
            this.labelerStatus1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // erpStatus1
            // 
            this.erpStatus1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.erpStatus1.Location = new System.Drawing.Point(210, 140);
            this.erpStatus1.Name = "erpStatus1";
            this.erpStatus1.Size = new System.Drawing.Size(126, 32);
            this.erpStatus1.TabIndex = 1;
            this.erpStatus1.Text = "ERP Wenco";
            this.erpStatus1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // emb1Status1
            // 
            this.emb1Status1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.emb1Status1.Location = new System.Drawing.Point(210, 40);
            this.emb1Status1.Name = "emb1Status1";
            this.emb1Status1.Size = new System.Drawing.Size(126, 32);
            this.emb1Status1.TabIndex = 5;
            this.emb1Status1.Text = "Embolsadora";
            this.emb1Status1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // qrStatus1
            // 
            this.qrStatus1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.qrStatus1.Location = new System.Drawing.Point(49, 140);
            this.qrStatus1.Name = "qrStatus1";
            this.qrStatus1.Size = new System.Drawing.Size(126, 32);
            this.qrStatus1.TabIndex = 2;
            this.qrStatus1.Text = "Cámara QR";
            this.qrStatus1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // masterPLCStatus1
            // 
            this.masterPLCStatus1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.masterPLCStatus1.Location = new System.Drawing.Point(49, 90);
            this.masterPLCStatus1.Name = "masterPLCStatus1";
            this.masterPLCStatus1.Size = new System.Drawing.Size(126, 32);
            this.masterPLCStatus1.TabIndex = 4;
            this.masterPLCStatus1.Text = "PLC Maestro";
            this.masterPLCStatus1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // pcIndustStatus1
            // 
            this.pcIndustStatus1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pcIndustStatus1.Location = new System.Drawing.Point(49, 40);
            this.pcIndustStatus1.Name = "pcIndustStatus1";
            this.pcIndustStatus1.Size = new System.Drawing.Size(126, 32);
            this.pcIndustStatus1.TabIndex = 3;
            this.pcIndustStatus1.Text = "PC Industrial";
            this.pcIndustStatus1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // groupBox1
            // 
            this.groupBox1.BackColor = System.Drawing.Color.Transparent;
            this.groupBox1.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("groupBox1.BackgroundImage")));
            this.groupBox1.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.groupBox1.Controls.Add(this.exitPallet1);
            this.groupBox1.Controls.Add(this.labelingEmb1);
            this.groupBox1.Controls.Add(this.enterEmb1);
            this.groupBox1.Controls.Add(this.fLabelEnter2);
            this.groupBox1.Controls.Add(this.emb1st5);
            this.groupBox1.Controls.Add(this.emb1st4);
            this.groupBox1.Controls.Add(this.emb1st3);
            this.groupBox1.Controls.Add(this.emb1st2);
            this.groupBox1.Dock = System.Windows.Forms.DockStyle.Top;
            this.groupBox1.Location = new System.Drawing.Point(3, 2);
            this.groupBox1.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Padding = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.groupBox1.Size = new System.Drawing.Size(789, 335);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Estado Actual";
            // 
            // exitPallet1
            // 
            this.exitPallet1.BackColor = System.Drawing.Color.Peru;
            this.exitPallet1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.exitPallet1.Location = new System.Drawing.Point(647, 11);
            this.exitPallet1.Name = "exitPallet1";
            this.exitPallet1.Size = new System.Drawing.Size(90, 90);
            this.exitPallet1.TabIndex = 23;
            this.exitPallet1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // labelingEmb1
            // 
            this.labelingEmb1.BackColor = System.Drawing.Color.Peru;
            this.labelingEmb1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.labelingEmb1.Location = new System.Drawing.Point(647, 161);
            this.labelingEmb1.Name = "labelingEmb1";
            this.labelingEmb1.Size = new System.Drawing.Size(90, 90);
            this.labelingEmb1.TabIndex = 22;
            this.labelingEmb1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // enterEmb1
            // 
            this.enterEmb1.BackColor = System.Drawing.Color.Peru;
            this.enterEmb1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.enterEmb1.Location = new System.Drawing.Point(54, 161);
            this.enterEmb1.Name = "enterEmb1";
            this.enterEmb1.Size = new System.Drawing.Size(90, 90);
            this.enterEmb1.TabIndex = 21;
            this.enterEmb1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // emb1st5
            // 
            this.emb1st5.BackColor = System.Drawing.Color.Peru;
            this.emb1st5.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.emb1st5.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F);
            this.emb1st5.Location = new System.Drawing.Point(495, 161);
            this.emb1st5.Name = "emb1st5";
            this.emb1st5.Size = new System.Drawing.Size(90, 90);
            this.emb1st5.TabIndex = 4;
            this.emb1st5.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.emb1st5.Click += new System.EventHandler(this.ShowDetails);
            // 
            // emb1st4
            // 
            this.emb1st4.BackColor = System.Drawing.Color.Peru;
            this.emb1st4.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.emb1st4.Location = new System.Drawing.Point(399, 161);
            this.emb1st4.Name = "emb1st4";
            this.emb1st4.Size = new System.Drawing.Size(90, 90);
            this.emb1st4.TabIndex = 3;
            this.emb1st4.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.emb1st4.Click += new System.EventHandler(this.ShowDetails);
            // 
            // emb1st3
            // 
            this.emb1st3.BackColor = System.Drawing.Color.Peru;
            this.emb1st3.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.emb1st3.Location = new System.Drawing.Point(302, 161);
            this.emb1st3.Name = "emb1st3";
            this.emb1st3.Size = new System.Drawing.Size(90, 90);
            this.emb1st3.TabIndex = 2;
            this.emb1st3.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.emb1st3.Click += new System.EventHandler(this.ShowDetails);
            // 
            // emb1st2
            // 
            this.emb1st2.BackColor = System.Drawing.Color.Peru;
            this.emb1st2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.emb1st2.Location = new System.Drawing.Point(205, 161);
            this.emb1st2.Name = "emb1st2";
            this.emb1st2.Size = new System.Drawing.Size(90, 90);
            this.emb1st2.TabIndex = 1;
            this.emb1st2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.emb1st2.Click += new System.EventHandler(this.ShowDetails);
            // 
            // statesTab
            // 
            this.statesTab.Controls.Add(this.machineStateTable);
            this.statesTab.Location = new System.Drawing.Point(4, 39);
            this.statesTab.Name = "statesTab";
            this.statesTab.Padding = new System.Windows.Forms.Padding(3);
            this.statesTab.Size = new System.Drawing.Size(795, 519);
            this.statesTab.TabIndex = 6;
            this.statesTab.Text = "Estados";
            this.statesTab.UseVisualStyleBackColor = true;
            // 
            // machineStateTable
            // 
            this.machineStateTable.AllowUserToAddRows = false;
            this.machineStateTable.AllowUserToDeleteRows = false;
            this.machineStateTable.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.machineStateTable.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.process,
            this.state});
            this.machineStateTable.Location = new System.Drawing.Point(14, 11);
            this.machineStateTable.Name = "machineStateTable";
            this.machineStateTable.ReadOnly = true;
            this.machineStateTable.RowHeadersWidth = 51;
            this.machineStateTable.RowTemplate.Height = 24;
            this.machineStateTable.Size = new System.Drawing.Size(773, 500);
            this.machineStateTable.TabIndex = 0;
            // 
            // process
            // 
            this.process.HeaderText = "Proceso";
            this.process.MinimumWidth = 6;
            this.process.Name = "process";
            this.process.ReadOnly = true;
            this.process.Width = 250;
            // 
            // state
            // 
            this.state.HeaderText = "Estado";
            this.state.MinimumWidth = 6;
            this.state.Name = "state";
            this.state.ReadOnly = true;
            this.state.Width = 300;
            // 
            // ClientWorker
            // 
            this.ClientWorker.WorkerReportsProgress = true;
            this.ClientWorker.WorkerSupportsCancellation = true;
            this.ClientWorker.DoWork += new System.ComponentModel.DoWorkEventHandler(this.CommunicateWithServer);
            this.ClientWorker.ProgressChanged += new System.ComponentModel.ProgressChangedEventHandler(this.UpdateHMI);
            // 
            // carToB1
            // 
            this.carToB1.BackColor = System.Drawing.Color.Transparent;
            this.carToB1.Location = new System.Drawing.Point(472, 174);
            this.carToB1.Name = "carToB1";
            this.carToB1.OffImage = null;
            this.carToB1.OnImage = global::TcpHMIClient.Properties.Resources.icons8_right_arrow_94;
            this.carToB1.Size = new System.Drawing.Size(50, 50);
            this.carToB1.TabIndex = 24;
            // 
            // carToB2
            // 
            this.carToB2.BackColor = System.Drawing.Color.Transparent;
            this.carToB2.Location = new System.Drawing.Point(247, 174);
            this.carToB2.Name = "carToB2";
            this.carToB2.OffImage = null;
            this.carToB2.OnImage = global::TcpHMIClient.Properties.Resources.icons8_left_arrow_94;
            this.carToB2.Size = new System.Drawing.Size(50, 50);
            this.carToB2.TabIndex = 23;
            // 
            // fCarUnkown
            // 
            this.fCarUnkown.BackColor = System.Drawing.Color.Transparent;
            this.fCarUnkown.Location = new System.Drawing.Point(328, 350);
            this.fCarUnkown.Name = "fCarUnkown";
            this.fCarUnkown.OffImage = global::TcpHMIClient.Properties.Resources.icons8_white_circle_96;
            this.fCarUnkown.OnImage = global::TcpHMIClient.Properties.Resources.icons8_green_circle_96;
            this.fCarUnkown.Size = new System.Drawing.Size(25, 25);
            this.fCarUnkown.TabIndex = 21;
            // 
            // fCameraQrGood
            // 
            this.fCameraQrGood.BackColor = System.Drawing.Color.Transparent;
            this.fCameraQrGood.Location = new System.Drawing.Point(401, 66);
            this.fCameraQrGood.Name = "fCameraQrGood";
            this.fCameraQrGood.OffImage = null;
            this.fCameraQrGood.OnImage = global::TcpHMIClient.Properties.Resources.icons8_camera_64;
            this.fCameraQrGood.Size = new System.Drawing.Size(50, 50);
            this.fCameraQrGood.TabIndex = 39;
            // 
            // fCameraQrBad
            // 
            this.fCameraQrBad.BackColor = System.Drawing.Color.Transparent;
            this.fCameraQrBad.Location = new System.Drawing.Point(401, 66);
            this.fCameraQrBad.Name = "fCameraQrBad";
            this.fCameraQrBad.OffImage = null;
            this.fCameraQrBad.OnImage = global::TcpHMIClient.Properties.Resources.icons8_camera_64_red;
            this.fCameraQrBad.Size = new System.Drawing.Size(50, 50);
            this.fCameraQrBad.TabIndex = 38;
            // 
            // fLToCarArrow
            // 
            this.fLToCarArrow.BackColor = System.Drawing.Color.Transparent;
            this.fLToCarArrow.Location = new System.Drawing.Point(247, 207);
            this.fLToCarArrow.Name = "fLToCarArrow";
            this.fLToCarArrow.OffImage = null;
            this.fLToCarArrow.OnImage = ((System.Drawing.Bitmap)(resources.GetObject("fLToCarArrow.OnImage")));
            this.fLToCarArrow.Size = new System.Drawing.Size(50, 50);
            this.fLToCarArrow.TabIndex = 31;
            // 
            // fLToBCDArrow
            // 
            this.fLToBCDArrow.BackColor = System.Drawing.Color.Transparent;
            this.fLToBCDArrow.Location = new System.Drawing.Point(486, 207);
            this.fLToBCDArrow.Name = "fLToBCDArrow";
            this.fLToBCDArrow.OffImage = null;
            this.fLToBCDArrow.OnImage = global::TcpHMIClient.Properties.Resources.icons8_right_arrow_94;
            this.fLToBCDArrow.Size = new System.Drawing.Size(50, 50);
            this.fLToBCDArrow.TabIndex = 30;
            // 
            // fLabelEnter2
            // 
            this.fLabelEnter2.BackColor = System.Drawing.Color.Transparent;
            this.fLabelEnter2.Location = new System.Drawing.Point(174, 195);
            this.fLabelEnter2.Name = "fLabelEnter2";
            this.fLabelEnter2.OffImage = null;
            this.fLabelEnter2.OnImage = global::TcpHMIClient.Properties.Resources.icons8_right_arrow_94;
            this.fLabelEnter2.Size = new System.Drawing.Size(31, 31);
            this.fLabelEnter2.TabIndex = 19;
            // 
            // HMIForm
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.ClientSize = new System.Drawing.Size(803, 562);
            this.Controls.Add(this.tabControl1);
            this.DoubleBuffered = true;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "HMIForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "HMI Embolsadoras Bocedi";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.StartClosing);
            this.Load += new System.EventHandler(this.HMIForm_Load);
            this.tabControl1.ResumeLayout(false);
            this.emb2Tab.ResumeLayout(false);
            this.groupBox8.ResumeLayout(false);
            this.groupBox6.ResumeLayout(false);
            this.groupBox5.ResumeLayout(false);
            this.panelCarInB2.ResumeLayout(false);
            this.carTab.ResumeLayout(false);
            this.groupBox7.ResumeLayout(false);
            this.groupBox4.ResumeLayout(false);
            this.groupBox4.PerformLayout();
            this.tabCarNoPos.ResumeLayout(false);
            this.tabCarB1.ResumeLayout(false);
            this.tabCarB2.ResumeLayout(false);
            this.palletEntryTab.ResumeLayout(false);
            this.groupBox10.ResumeLayout(false);
            this.groupBox9.ResumeLayout(false);
            this.panelCarInB1.ResumeLayout(false);
            this.emb1Tab.ResumeLayout(false);
            this.groupBox3.ResumeLayout(false);
            this.groupBox2.ResumeLayout(false);
            this.groupBox1.ResumeLayout(false);
            this.statesTab.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.machineStateTable)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage emb1Tab;
        private System.Windows.Forms.TabPage emb2Tab;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Label qrStatus1;
        private System.Windows.Forms.Label erpStatus1;
        private System.Windows.Forms.Label labelerStatus1;
        private System.Windows.Forms.Label emb1Status1;
        private System.Windows.Forms.Label masterPLCStatus1;
        private System.Windows.Forms.Label pcIndustStatus1;
        private System.Windows.Forms.Label emb1st5;
        private System.Windows.Forms.Label emb1st4;
        private System.Windows.Forms.Label emb1st3;
        private System.Windows.Forms.Label emb1st2;
        private System.Windows.Forms.GroupBox groupBox8;
        private System.Windows.Forms.Label labelerStatus2;
        private System.Windows.Forms.Label emb2Status2;
        private System.Windows.Forms.Label masterPLCStatus2;
        private System.Windows.Forms.Label pcIndustStatus2;
        private System.Windows.Forms.Label erpStatus2;
        private System.Windows.Forms.GroupBox groupBox6;
        private System.Windows.Forms.GroupBox groupBox5;
        private System.Windows.Forms.Label emb2st2;
        private System.Windows.Forms.Label emb2st4;
        private System.Windows.Forms.Label emb2st5;
        private System.Windows.Forms.Label emb2st3;
        private System.Windows.Forms.Label emb2st1;
        private System.Windows.Forms.TabPage palletEntryTab;
        private System.Windows.Forms.TabPage carTab;
        private System.ComponentModel.BackgroundWorker ClientWorker;
        private System.Windows.Forms.Label slavePLCStatus2;
        private System.Windows.Forms.Label labelingEmb1;
        private System.Windows.Forms.Label enterEmb1;
        private TcpHMIClient.FLabel fLabelEnter2;
        private System.Windows.Forms.TabPage statesTab;
        private System.Windows.Forms.DataGridView machineStateTable;
        private System.Windows.Forms.DataGridViewTextBoxColumn process;
        private System.Windows.Forms.DataGridViewTextBoxColumn state;
        private System.Windows.Forms.Label labelingEmb2;
        private System.Windows.Forms.Panel panelCarInB2;
        private System.Windows.Forms.Label palletInCarPos2;
        private System.Windows.Forms.GroupBox groupBox7;
        private System.Windows.Forms.GroupBox groupBox4;
        private TcpHMIClient.FLabel carToB1;
        private TcpHMIClient.FLabel carToB2;
        private System.Windows.Forms.Label label30;
        private TcpHMIClient.FLabel fCarUnkown;
        private System.Windows.Forms.Panel tabCarB2;
        private System.Windows.Forms.Label tabPalletB2;
        private System.Windows.Forms.Panel tabCarB1;
        private System.Windows.Forms.Label tabPalletB1;
        private System.Windows.Forms.Panel tabCarNoPos;
        private System.Windows.Forms.Label tabPalletNoPos;
        private System.Windows.Forms.GroupBox groupBox9;
        private TcpHMIClient.FLabel fCameraQrGood;
        private TcpHMIClient.FLabel fCameraQrBad;
        private System.Windows.Forms.Label entryBCD2;
        private System.Windows.Forms.Label entryBCD1;
        private System.Windows.Forms.Label entryPalletLabel;
        private System.Windows.Forms.Panel panelCarInB1;
        private System.Windows.Forms.Label palletInCarPos1;
        private TcpHMIClient.FLabel fLToCarArrow;
        private TcpHMIClient.FLabel fLToBCDArrow;
        private System.Windows.Forms.GroupBox groupBox10;
        private System.Windows.Forms.Label exitPallet1;
        private System.Windows.Forms.Label exitPallet2;
        private System.Windows.Forms.Label bcd2Errors;
        private System.Windows.Forms.Label carErrors;
        private System.Windows.Forms.Label entryErrors;
        private System.Windows.Forms.Label bcd1Errors;
    }
}

