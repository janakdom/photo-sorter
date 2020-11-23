using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PhotoSorting
{
    public partial class Report : Form
    {
        public Report(String data)
        {
            InitializeComponent();
            contentTextbox.Text = data;
        }

        private void closeButton_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            SaveFileDialog file = new SaveFileDialog();
            file.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            file.Filter = "TXT file|*.txt";
            file.FileName = "LOG_" + DateTime.Now.ToString("yyyy-dd-MM_H-mm-ss") + ".txt";

            if (file.ShowDialog() == DialogResult.OK)
            {
               File.WriteAllText(file.FileName, contentTextbox.Text);
            }
        }
    }
}
