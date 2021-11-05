using System;
using System.Windows.Forms;
using System.Threading;
using System.IO;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Session;
using System.IO.Ports;
using System.Drawing;
using System.Data;

namespace WindowsFormsApp1
{
    static class Program
    {
        static String turretState;
        static Mutex turretStateMutex;

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

            // Start Watching process
            Thread watchProcess = new Thread(new ThreadStart(ProcessWatcher.starto));
            watchProcess.Start();
            ProcessWatcher.setProcessId(args[0]);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Watch Turret...
            turretStateMutex = new Mutex();
            Turret turret = new Turret();
            Thread turretWatcher = new Thread(new ThreadStart(turret.watch));
            turretWatcher.Start();

            //Set up the form
            Form1 f = new Form1();
            turret.TurretChanged += f.OnTurretChanged;
            Application.Run(f);            
        }
        public static String getTurretState()
        {
            
            if (turretStateMutex.WaitOne(4000)) // wait at most 4 seconds for mutex
            {
                String locTState = null;
                try
                {
                    locTState = turretState;
                }
                finally
                {                  
                    turretStateMutex.ReleaseMutex();                    
                }
                return locTState;
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
        Thread.Sleep(1000); // Allow main thread to build GUI
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
                if (newState.Contains("0")) newState = "0";
                else if (newState.Contains("1")) newState = "1";
                else if (newState.Contains("2")) newState = "2";
                else if (newState.Contains("3")) newState = "3";
                else if (newState.Contains("4")) newState = "4";
                else if (newState.Contains("5")) newState = "5";
                else if (newState.Contains("6")) newState = "6";
                else if (newState.Contains("7")) newState = "7";
                else continue;

                oldState = newState;
                WindowsFormsApp1.Program.setTurretState(newState);
                OnTurretChanged(newState); // getCalState assumes new State is set
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
    //static string appName = "notepad";
    static string ID;
    static string fileExtension = ".tif"; // change to .tif
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
            
            kernelSession.EnableKernelProvider(KernelTraceEventParser.Keywords.FileIO |
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
        if (data.ProcessID.ToString().Equals(ID) )
        {
            if (data.FileName.Contains(fileExtension))
            {
                Console.WriteLine("Turret State : " + WindowsFormsApp1.Program.getTurretState());
                Console.WriteLine("ProcessID : " + ID);
                Console.WriteLine("Filename : " + data.FileName); // --> going to open file, add the data and close it
                FileIO.addCalDataToTiffFile(data.FileName);
            }
        }
    }
}
class FileIO {
    // using config text file for now --> will move to registry when building installer...
    private static String calPath = "C:\\Users\\P00ko\\Desktop\\PROJECTS\\Microscope\\";
    private static String CONFIG = "turretWatcher.config";
    private static String calSetName;
    private static String currentCalFile;
    private static DataSet LUT;
    private static FileIO fileIO;
    private static bool WatcherMutex;

    private FileIO()
    {
        // Instantiate from config file...
        String configPath = calPath + CONFIG;
        String[] lines = System.IO.File.ReadAllLines(configPath);
        calSetName = lines[0];
        currentCalFile = calPath + calSetName + ".xml";
        initializeTurretObjectiveRelayLUT();

    }

    public static FileIO getInstance()
    {
        if(FileIO.fileIO == null)
        {
            FileIO.fileIO = new FileIO();
            return fileIO;
        }
        else
        {
            return fileIO;
        }
    }

    public static void addCalDataToTiffFile(string fName) // Will be called over and over... --> Need to handle many calls
    {
        if (WatcherMutex) return;
        WatcherMutex = true;
        try
        {
                        
            Image img = Image.FromFile(fName);
            Image newImg = new Bitmap(img);
            System.Drawing.Imaging.PropertyItem[] items = img.PropertyItems;
            
            // Check that file hasn't already been stamped:
            bool hasValue = false;
            foreach (System.Drawing.Imaging.PropertyItem item in items)
            {
                if (item.Id == 6996) hasValue = true;
            }

            // If not, add stamp:
            if (!hasValue)
            {
                // Build string to write to file:
                string[] data = fileIO.getCalibration(WindowsFormsApp1.Program.getTurretState());
                string s = "\n[Calibration]\n" +
                           "Objective = " + data[0] + "\n" +
                           "Relay = " + data[1] + "\n" +
                           "PixelPitch = " + data[2] + "\n\n" +
                           LUT.Tables[0].Rows[0]["MicroscopeInfo"].ToString() + "\n";
                char[] vs = s.ToCharArray();
                byte[] ba = new byte[vs.Length];
                for (int i = 0; i < ba.Length; i++)
                {
                    ba[i] = Convert.ToByte(vs[i]);
                }
                for (int i = 0; i < items.Length; i++)
                {
                    newImg.SetPropertyItem(items[i]);
                }
                System.Drawing.Imaging.PropertyItem item = img.PropertyItems[0];
                img.Dispose();
                item.Id = 6996;
                item.Len = vs.Length;
                item.Type = 2;
                item.Value = ba;
                newImg.SetPropertyItem(item);
                newImg.Save(fName, System.Drawing.Imaging.ImageFormat.Tiff);
            }
            newImg.Dispose();            
        }
        finally
        {
            WatcherMutex = false;
        }

    }

    public static void createNewTurretObjectiveRelayXML(DataSet ds)
    {        
        calSetName = "turretWatcher" + "_" + DateTimeOffset.Now.ToUnixTimeSeconds();
        currentCalFile = calPath + calSetName + ".xml";
        try
        {
            ds.WriteXml(currentCalFile);
        }
        catch(Exception e)
        {
            MessageBox.Show("Couldn't write to the new .xml file. \n Exception :" + e.Message);
            Environment.Exit(0);
        }
        updateConfigFile(calSetName);
        initializeTurretObjectiveRelayLUT();
    }

    public static void updateConfigFile(String newSetName)
    {
        String configPath = calPath + CONFIG;
        try
        {            
            StreamWriter sw = new StreamWriter(configPath);            
            sw.WriteLine(newSetName);
            sw.Close();
        }
        catch (Exception e)
        {
            MessageBox.Show("Couldn't read the turretWatcher.config file. \n Exception: " + e.Message);
            Environment.Exit(0);
        }        
    }

    public static string getCurrentCalibrationXML()
    {
        return currentCalFile;
    }

    private static void initializeTurretObjectiveRelayLUT()
    {
        LUT = new DataSet(); // Tables[0] --> Microscope data, Tables[1] Objective/Relay - Pitch Combos (See .xml file)
        try
        {
            LUT.ReadXml(currentCalFile);
        }
        catch(FileNotFoundException e)
        {
            MessageBox.Show("Something went wrong reading the XML calibration file. You can: " +
                            "\n 1) Check that the turretWatcher.config file is refering to an \n   existing .xml file" +
                            "\n 2) Verify that the .xml file is correctly structured");
            Environment.Exit(0);
        }
        
    }

    public String[] getRelayObjective(String rO)
    {
        String[] data = { " ", " " };
        DataTable tb = LUT.Tables[1];
        foreach (DataRow dr in tb.Rows)
        {
            if (dr["ID"].ToString().Equals(rO))
            {
                data[0] = dr["Objective"].ToString();
                data[1] = dr["Relay"].ToString();
            }
        }
        return data;
    }
    public String[] getCalibration(String rO)
    {
        String[] data = { " ", " " , " " };
        DataTable tb = LUT.Tables[1];
        foreach (DataRow dr in tb.Rows)
        {
            if (dr["ID"].ToString().Equals(rO))
            {
                data[0] = dr["Objective"].ToString();
                data[1] = dr["Relay"].ToString();
                data[2] = dr["Pitch"].ToString();
            }
        }
        return data;
    }

}