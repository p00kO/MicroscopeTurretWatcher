using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

using System.Text;
using System.Threading;
using Microsoft.Diagnostics.Symbols;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Session;
using System.IO.Ports;


namespace WindowsFormsApp1
{
    static class Program
    {
        [STAThread]
        static void Main()
            // -> ToDo: need to receive PID for Microscope application from PS launch script
        {
            // Check that we're admin:
            if (!(TraceEventSession.IsElevated() ?? false))
            {
                MessageBox.Show("Please run as an Administrator....");
                return;
            }
            // Watch Turret...            
            Turret turret = new Turret();
            Thread turretWatcher = new Thread(new ThreadStart(turret.watch));
            turretWatcher.Start();

            // Start Watching process
            Thread watchProcess = new Thread(new ThreadStart(ProcessWatcher.starto));
            watchProcess.Start();
            
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Form1 f = new Form1();
            turret.TurretChanged += f.OnTurretChanged;
            Application.Run(f);

        }

    }   
}

class Turret
{
    // 1-Define a delegate
    // 2- Define an event 
    // 3- Raise the event
    public delegate void TurretChangedEventHandler(object source, TurretChangedEventArgs args);
    public event TurretChangedEventHandler TurretChanged;

    SerialPort currentPort;
    bool portFound;

    public void watch()
    {  
        SetComPort();
        if (!portFound)
        {
            MessageBox.Show("There's a problem with your detector connection. Please check and try again");
            System.Environment.Exit(0);
        }        
        currentPort.Open();
        String oldState ="  ";
        while (true)
        {
            String newState = currentPort.ReadLine();
            if (!oldState.Equals(newState))
            {
                Console.WriteLine(newState);
                OnTurretChanged(newState);                
                oldState = newState;
            }            
        }
        // not needed but... 
        currentPort.Close();
    }

    protected virtual void OnTurretChanged(string value) 
    {
        if(TurretChanged != null)
        {
            TurretChanged(this, new TurretChangedEventArgs(value)); // GUI update            
            // add to a list with time stamp.... -> for checking by process thread...
        }
    }

    private void SetComPort()
    {
        try
        {
            string[] ports = SerialPort.GetPortNames();                        
            if(ports.Length > 1)
            {
                MessageBox.Show("There's too many serial devices. I've become confused");
                System.Environment.Exit(0);
                portFound = false;
            }
            currentPort = new SerialPort(ports[0], 9600);
            portFound = true;
        }
        catch (Exception e)
        {
            MessageBox.Show("There's a problem with your detector. Please check connection or get help from service group! ;-) ");
            System.Environment.Exit(0); 
        }
    }
}

public class TurretChangedEventArgs : EventArgs
{
    public TurretChangedEventArgs(String value)
    {
        this.value = value;
    }
    public string value { get; }
}

class ProcessWatcher
{
    // will be changed to PID passed from PS script
    static string appName = "notepad";
    static string fileExtension = ".txt"; // change to .tif
    static int counter = 0;
    static String lastFileName = "  ";
    public static void starto()
    {
        using (var kernelSession = new TraceEventSession("test"))
        {
            // Handle ctrl C :
            Console.WriteLine("Setup cancel keys:");
            Console.CancelKeyPress += delegate (object sender, ConsoleCancelEventArgs e) {
                kernelSession.Dispose();
                Environment.Exit(0);
            }; // ??? 
               // Subscriptions to certain events... 
            kernelSession.EnableKernelProvider(/*KernelTraceEventParser.Keywords.Process | */
                                               KernelTraceEventParser.Keywords.FileIO |
                                               KernelTraceEventParser.Keywords.FileIOInit |
                                               KernelTraceEventParser.Keywords.DiskFileIO);

            kernelSession.Source.Kernel.FileIOQueryInfo += fileCreate;
            // Start processing data:
            kernelSession.Source.Process();
        }

    }

    private static void fileCreate(FileIOInfoTraceData data)
    {
        if (data.ProcessName == appName) // set as passed PID from PowerShell
        {
            if (data.FileName.Contains(fileExtension))
            {
                // ToDo --> need a filter to distinguish file write type of events + if same or not...
                
                // 1) open reffered file get file date/time stamp
                // 2) get nearest turret value to date/time stamp --> thread safe locked array
                // 3) lookup pitch data from LUT and write to tif header
                // 4) close file
                Console.WriteLine("Sending : " + data.FileName + "  " + counter); // --> going to open file, add the data and close it
            }
        }
    }
}
