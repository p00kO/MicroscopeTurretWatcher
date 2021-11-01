using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowsFormsApp1
{
    public partial class Form2 : Form
    {
        private bool calibrationValueshanged = false;
        public Form2()
        {
            InitializeComponent();
            DataSet ds = new DataSet();
            ds.ReadXml("C:\\Users\\P00ko\\Desktop\\PROJECTS\\Microscope\\turretParameters.xml");
            dataGridView1.DataSource = ds.Tables[1];
            textBox1.Text = ds.Tables[0].Rows[0]["MicroscopeInfo"].ToString();
            calibrationValueshanged = false;
        }

        private void button1_Click(object sender, EventArgs e)
        {

        }

        private void button2_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (this.calibrationValueshanged)
            {
                MessageBox.Show("You made some changes to the Microscope calibrations. Do you want to save them ?");
                this.calibrationValueshanged = false;
            }
            Close();
        }

        private void dataGridView1_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            calibrationValueshanged = true;
        }
        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            calibrationValueshanged = true;
        }

    }
}
// 1) Need a .xml file to pull data from 
// 2) Need to populate form
// 3) Need to allow modification and updates to form with related updates to .xml file 
// 4) Need to backup old files not to loose Calibration history