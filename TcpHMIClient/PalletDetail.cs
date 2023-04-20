using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TcpHMIClient
{
    public partial class PalletDetail : Form
    {
        HMIClient client;
        HMIClient.DeletePallet pallet;
        public PalletDetail()
        {
            InitializeComponent();
        }

        public void Init(int packager, int position, HMIClient.Pallet pallet, HMIClient client)
        {
            this.pallet = new HMIClient.DeletePallet() { Packager = packager, Position = position, Pallet = pallet};
            this.client = client;
            listView.Items[0].SubItems.Add(pallet.Qr);
            listView.Items[1].SubItems.Add(pallet.Id);
            listView.Items[2].SubItems.Add(pallet.Injector);
            listView.Items[3].SubItems.Add(pallet.Recipe);
            listView.Items[4].SubItems.Add(pallet.Labeling ? "Si" : "No");
        }

        private void deleteBtn_Click(object sender, EventArgs e)
        {
            client.needToDelete = true;
            client.deletePallet = pallet;
            client.detailForm = this;
            exitBtn.Enabled = false;
            deleteBtn.Enabled = false;
        }
    }
}
