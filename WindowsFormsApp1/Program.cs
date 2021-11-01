using System;
using System.Windows.Forms;
using System.Threading;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Session;
using System.IO.Ports;
using System.Xml;
using System.Drawing;
using System.Data;

namespace WindowsFormsApp1
{
    static class Program
    {
        static String turretState;
        static Mutex turretStateMutex;
        static DataSet calibrationData; // Conversion table from ID to Relay/Object values

        [STAThread]
        static void Main(string [] args)
            // -> ToDo: need to receive PID for Microscope application from PS launch script
        {           
            // Check that we're admin:
            if (!(TraceEventSession.IsElevated() ?? false))
            {
                MessageBox.Show("Please run as an Administrator....");
                return;
            }

            // Setup calibration data object: --> TODO Will need to sync up when data is updated...
            calibrationData =  new DataSet();
            calibrationData.ReadXml("C:\\Users\\P00ko\\Desktop\\PROJECTS\\Microscope\\turretParameters.xml");            
            
            // Watch Turret...
            turretStateMutex = new Mutex();
            Turret turret = new Turret();
            Thread turretWatcher = new Thread(new ThreadStart(turret.watch));
            turretWatcher.Start();

            // Start Watching process
            Thread watchProcess = new Thread(new ThreadStart(ProcessWatcher.starto));
            watchProcess.Start();
            ProcessWatcher.setProcessId(args[0]);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            //Set up the form
            Form1 f = new Form1();
            turret.TurretChanged += f.OnTurretChanged;
            Application.Run(f);
        }
        public static String getTurretState()
        {
            if (turretStateMutex.WaitOne(4000)) // wait at most 4 seconds for mutex
            {
                try
                {
                    return turretState;
                }
                finally
                {
                    turretStateMutex.ReleaseMutex();                    
                }
            }
            else
            {
                Console.WriteLine("Couldn't return the turretState... ");
                return null;
            }
        }
        public static void setTurretState(String value)
        {
            if (turretStateMutex.WaitOne(4000)) // wait at most 4 seconds for mutex
            {
                try
                {
                    turretState = value;
                }
                finally
                {
                    turretStateMutex.ReleaseMutex();
                }
            }
            else
            {
                Console.WriteLine("Couldn't update turretState! ");
            }
        }
    }
}

class Turret
{
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
                OnTurretChanged(newState);                
                oldState = newState;
                WindowsFormsApp1.Program.setTurretState(newState);
            }            
        }
        // not needed but why not... 
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
    static string ID;
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
            }; 
            
            kernelSession.EnableKernelProvider(/*KernelTraceEventParser.Keywords.Process | */
                                               KernelTraceEventParser.Keywords.FileIO |
                                               KernelTraceEventParser.Keywords.FileIOInit |
                                               KernelTraceEventParser.Keywords.DiskFileIO);

            kernelSession.Source.Kernel.FileIOQueryInfo += fileCreate;
            // Start processing data:
            kernelSession.Source.Process();
        }
    }
    public static void setProcessId(String pID)
    {
        ProcessWatcher.ID = pID;
    }
    private static void fileCreate(FileIOInfoTraceData data)
    {
        //if(data.ProcessID == Int32.Parse(ID)) // set as passed PID from PowerShell
        if (data.ProcessName == appName) 
        {
            if (data.FileName.Contains(fileExtension))
            {
                // ToDo --> need a filter to distinguish file write type of events + if same or not...

                // 1) open reffered file get file date/time stamp
                // 2) get nearest turret value to date/time stamp --> thread safe locked array
                // 3) lookup pitch data from LUT and write to tif header
                // 4) close file
                Console.WriteLine("Turret State : " + WindowsFormsApp1.Program.getTurretState());
                Console.WriteLine("ProcessID : " + ID);
                Console.WriteLine("Filename : " + data.FileName); // --> going to open file, add the data and close it
                // FileIO.readTiffFile(data.FileName);
            }
        }
    }
}

class FileIO {

    private const String filename = "C:\\Users\\P00ko\\Desktop\\PROJECTS\\Microscope\\turretParameters.xml"; // pass in....
    public static void readTiffFile(string fName)
    {
        string s = "HelloMama!";
        char[] vs = s.ToCharArray();
        byte[] ba = new byte[vs.Length];
        for (int i = 0; i < ba.Length; i++)
        {
            ba[i] = Convert.ToByte(vs[i]);
        }
        
        Image img = Image.FromFile(fName);
        Image newImg = new Bitmap(img);
        System.Drawing.Imaging.PropertyItem [] items = img.PropertyItems;
        for (int i = 0; i < items.Length; i++)
        {
            newImg.SetPropertyItem(items[i]);
        }
        
        // Do I really need to create a new file ?? mabe just add it to the old one ?
        System.Drawing.Imaging.PropertyItem item = img.PropertyItems[0];
        img.Dispose();
        item.Id = 6996;
        item.Len = vs.Length;
        item.Type = 2;
        item.Value = ba;
        
        newImg.SetPropertyItem(item);       
        newImg.Save(fName, System.Drawing.Imaging.ImageFormat.Tiff);
        newImg.Dispose();
    }
}