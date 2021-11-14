using System;
using System.Windows.Forms;
using System.Threading;
using System.IO;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Session;
using System.IO.Ports;
using System.Drawing;
using System.Drawing.Imaging;
using System.Data;
using System.Reflection;
using System.Management;

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
            ProcessWatcher.killCameraApp();
            System.Environment.Exit(0);
        }        
        currentPort.Open();
        String oldState ="  ";
        while (true)
        {
            try
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
            catch(Exception e)
            {
                MessageBox.Show("There's a problem with your detector connection. Please check and try again \n" + e.Message);
                ProcessWatcher.killCameraApp();
                System.Environment.Exit(0);
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
        }
    }

    private void SetComPort()
    {
        portFound = false;
        try
        {
           string[] ports = SerialPort.GetPortNames();
           if (ports.Length > 0)
           {
                // Arduino:
                String pId = "2341";
                String vId = "0043";
                // Arduino Nanao:
/*                String pId = "1A86";
                String vId = "7523";*/
                ManagementObjectSearcher searcher =
                                new ManagementObjectSearcher(
                                    @"\\localhost\root\CIMV2", 
                                    "SELECT * FROM Win32_PnPEntity WHERE ConfigManagerErrorCode = 0"
                                );
                foreach (ManagementObject queryObj in searcher.Get())
                {
                    String s = queryObj["DeviceID"].ToString();
                    if (s.Contains(pId) && s.Contains(vId))
                    {
                        
                        for (int i = 0; i < ports.Length; i++)
                        {                            
                            if (queryObj["Name"].ToString().Contains(ports[i]))
                            {
                                currentPort = new SerialPort(ports[i], 9600);
                                portFound = true;
                            }
                        }
                    }
                } 
            }
            else
            {
                MessageBox.Show(" Sorry, I couldn't find the turret watcher port.\n");
                portFound = false;
                ProcessWatcher.killCameraApp();
                System.Environment.Exit(0);
            }
        }
        catch (Exception e)
        {
            MessageBox.Show("There's a problem with your detector. Please check connection or get help from service group! ;-) ");
            ProcessWatcher.killCameraApp();
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
    static string ID;
    static string fileExtension = ".tif"; // change to .tif    
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
    public static void killCameraApp()
    {
        KillProcessAndChildren(Int32.Parse(ID));
    }
    public static void setProcessId(String pID)
    {
        ProcessWatcher.ID = pID;
    }
    private static void fileCreate(FileIOInfoTraceData data)
    {
        if (data.ProcessID.ToString().Equals(ID) )
        {
            if (data.FileName.Contains(fileExtension))
            {
                FileIO.addCalDataToTiffFile(data.FileName);
            }
        }
    }
    private static void KillProcessAndChildren(int pid)
    {
        // Cannot close 'system idle process'.
        if (pid == 0)
        {
            return;
        }
        Console.WriteLine("Trying to close: " + pid);
        System.Management.ManagementObjectSearcher searcher = new System.Management.ManagementObjectSearcher
                ("Select * From Win32_Process Where ParentProcessID=" + pid);
        System.Management.ManagementObjectCollection moc = searcher.Get();
        foreach (System.Management.ManagementObject mo in moc)
        {
            KillProcessAndChildren(Convert.ToInt32(mo["ProcessID"]));
        }
        try
        {            
            System.Diagnostics.Process proc = System.Diagnostics.Process.GetProcessById(pid);
            proc.Kill();
        }
        catch (ArgumentException)
        {
            // Process already exited.
        }
    }
}
class FileIO
{
    // using config text file for now --> will move to registry when building installer...
    private static String calPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + "\\";
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
    private static bool isFileLocked(string FileName)
    {
        FileStream fs = null;
        try
        {
            fs = File.Open(FileName, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        catch (UnauthorizedAccessException) // https://msdn.microsoft.com/en-us/library/y973b725(v=vs.110).aspx
        {            
            try
            {
                fs = File.Open(FileName, FileMode.Open, FileAccess.Read, FileShare.None);
            }
            catch (Exception)
            {
                return true; // This file has been locked, we can't even open it to read
            }
        }
        catch (Exception)
        {
            return true; // This file has been locked
        }
        finally
        {
            if (fs != null)
                fs.Close();
        }
        return false;
    }
    public static void addCalDataToTiffFile(string fName) // Will be called over and over... --> Need to handle many calls
    {
        if (WatcherMutex) return;        
        if (isFileLocked(fName)) return;
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
            // ...if not, add stamp:
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
                // Copy over tags from original image:
                for (int i = 0; i < items.Length; i++)
                {
                    newImg.SetPropertyItem(items[i]);
                }

                // Generate a 24bit RGB image with no compression:
                ImageCodecInfo myImageCodecInfo;
                Encoder myEncoder;
                Encoder myEncoderCol;
                EncoderParameter myEncoderParameter;
                EncoderParameters myEncoderParameters;
                myImageCodecInfo = GetEncoderInfo("image/tiff");
                myEncoder = Encoder.Compression;
                myEncoderCol = Encoder.ColorDepth;
                myEncoderParameters = new EncoderParameters(2);
                myEncoderParameter = new EncoderParameter(
                                    myEncoder,
                                    (long)EncoderValue.CompressionNone);
                myEncoderParameters.Param[0] = myEncoderParameter;
                myEncoderParameter = new EncoderParameter(myEncoderCol, 24L);
                myEncoderParameters.Param[1] = myEncoderParameter;

                // Build metadata for the image:
                System.Drawing.Imaging.PropertyItem item = img.PropertyItems[0];
                img.Dispose();
                item.Id = 6996;
                item.Len = vs.Length;
                item.Type = 2;
                item.Value = ba;
                newImg.SetPropertyItem(item);   
                
                //Save image:
                newImg.Save(fName, myImageCodecInfo, myEncoderParameters);
            }
            newImg.Dispose();
        } 
        catch (Exception e)
        {
            MessageBox.Show("Couldn't handle the file path passed by watcher. \n Exception: " + e.Message);
        }
        finally
        {
            WatcherMutex = false;
        }
    }
    private static ImageCodecInfo GetEncoderInfo(String mimeType)
    {
        int j;
        ImageCodecInfo[] encoders;
        encoders = ImageCodecInfo.GetImageEncoders();
        for (j = 0; j < encoders.Length; ++j)
        {
            if (encoders[j].MimeType == mimeType)
                return encoders[j];
        }
        return null;
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
            ProcessWatcher.killCameraApp();
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
            ProcessWatcher.killCameraApp();
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
            ProcessWatcher.killCameraApp();
            Environment.Exit(0);
        }
        
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