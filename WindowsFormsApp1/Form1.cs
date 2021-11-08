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
    public partial class Form1 : Form        
    {
        FileIO fileIO;
        public Form1()
        {
            InitializeComponent();
            this.fileIO = FileIO.getInstance();

        }

        private void button1_Click(object sender, EventArgs e)
        {
            Form2 f = new Form2(); // Calibration Dialog....
            f.Show();            
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
           e.Cancel = true;            
        }       
        public void OnTurretChanged(object source, TurretChangedEventArgs e)
        {
            String [] state = fileIO.getRelayObjective(e.value); // returns [0] Objective and [1] Relay
            textBox1.Invoke((MethodInvoker)delegate
            {
                if(e.value != null) textBox1.Text = state[0];
            });
            textBox2.Invoke((MethodInvoker)delegate
            {
                if (e.value != null) textBox2.Text = state[1];  
            });
        }

       
    }
}
