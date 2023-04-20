using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;

namespace TcpHMIClient
{
    public partial class FLabel : UserControl
    {
        enum States
        {
            On,
            Off,
            Blinking,
        }
        States State { get; set; }
        bool On { get; set; }

        readonly System.Timers.Timer blinkTimer;

        [Category("Appearance")]
        [Description("The image that indicates this label is ON")]
        [Bindable(true)]
        public Bitmap OnImage { get; set; }

        private Bitmap offImage;

        [Category("Appearance")]
        [Description("The image that indicates this label is Off")]
        [Bindable(true)]
        public Bitmap OffImage
        {
            get { return offImage; }
            set {
                offImage = value;
                Repaint();
            }
        }

        public FLabel()
        {
            On = false;
            State = States.Off;
            blinkTimer = new System.Timers.Timer(500)
            {
                Enabled = false
            };
            blinkTimer.Elapsed += new ElapsedEventHandler(Repaint);
            InitializeComponent();
        }

        public void TurnOn()
        {
            State = States.On;
            blinkTimer.Enabled = false;
            Repaint();
        }

        public void TurnOff()
        {
            State = States.Off;
            blinkTimer.Enabled = false;
            Repaint();
        }

        public void Blink()
        {
            State = States.Blinking;
            blinkTimer.Enabled = true;
        }

        private void Repaint(object source = null, ElapsedEventArgs e = null)
        {
            switch (State)
            {
                case States.On:
                    pictureBox.Image = OnImage;
                    break;
                case States.Off:
                    pictureBox.Image = OffImage;
                    break;
                case States.Blinking:
                    if (On)
                    {
                        On = false;
                        pictureBox.Image = OffImage;
                    }
                    else
                    {
                        On = true;
                        pictureBox.Image = OnImage;
                    }
                    break;
            }
        }
    }
}
