using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SXAuth;
using System.Net;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Runtime.InteropServices;
using System.IO;
using System.Reflection;

namespace AntiFlagV2
{
    class Program
    {
        private static string CurDir { get; } = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        private static string ExeFileLocation { get => Process.GetCurrentProcess().MainModule.FileName; }

#region Config 
        private static string AppData { get; }         = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        private static string Roaming { get; }         = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        private static string Documents { get; }       = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        private static string ProgramData { get; }     = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        private static string AppDataLocal { get; }    = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        private static string ProgramFilesX86 { get; } = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);


        private static readonly List<string> Files = new()
        {
            ProgramFilesX86 + @"\Overwatch\.patch.result",
            ProgramFilesX86 + @"\Overwatch\.product.db",
            ProgramFilesX86 + @"\Overwatch\Launcher.db",
            ProgramFilesX86 + @"\Overwatch\.product.db.old",
            ProgramFilesX86 + @"\Overwatch\Launcher.db.old",
            ProgramFilesX86 + @"\Overwatch\.product.db.new",
            ProgramFilesX86 + @"\Overwatch\Launcher.db.new",

            ProgramFilesX86 + @"\Battle.net\.product.db",
            ProgramFilesX86 + @"\Battle.net\Launcher.db",
            ProgramFilesX86 + @"\Battle.net\.product.db.new",
            ProgramFilesX86 + @"\Battle.net\.product.db.old",
            ProgramFilesX86 + @"\Battle.net\Launcher.db.new",
            ProgramFilesX86 + @"\Battle.net\Launcher.db.old",

            ProgramFilesX86 + @"\Battle.net\.build.info",
            ProgramFilesX86 + @"\Battle.net\.patch.result",

            ProgramData + @"\Battle.net\Agent\.patch.result",
            ProgramData + @"\Battle.net\Agent\.product.db",
            ProgramData + @"\Battle.net\Agent\product.db"
        };

        private static readonly List<string> Folders = new()
        {
            AppDataLocal + @"\Blizzard\",

            AppData + @"\Battle.Net\",
            AppData + @"\Blizzard Entertainment\",
            
            Roaming + @"\Battle.net\",

            Documents+ @"\Overwatch\Logs\",

            ProgramData + @"\Battle.net\Setup\",
            ProgramData + @"\Battle.net\Agent\data\",
            ProgramData + @"\Battle.net\Agent\Logs\",
            ProgramData + @"\Blizzard Entertainment\",

            ProgramFilesX86 + @"\Overwatch\_retail_\cache\",
            ProgramFilesX86 + @"\Overwatch\_retail_\GPUCache\",

            Path.GetTempPath()
        };

#pragma warning disable CA1416 
        private static readonly List<RegistryKey> RegistryKeys = new()
        {
            Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Overwatch", true),
            Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Battle.net", true),
            Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Blizzard Entertainment", true),
            Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Blizzard Entertainment", true),
            Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Activision", true),
            Registry.ClassesRoot.OpenSubKey(@"Applications\Overwatch.exe", true),
            Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone\NonPackaged\C:#Program Files (x86)#Overwatch#_retail_#Overwatch.exe", true),
            Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\RADAR\HeapLeakDetection\DiagnosedApplications\Overwatch.exe", true),
            Registry.ClassesRoot.OpenSubKey(@"VirtualStore\MACHINE\SOFTWARE\WOW6432Node\Activision", true),
            Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Classes\VirtualStore\MACHINE\SOFTWARE\WOW6432Node\Activision", true)
        };
#pragma warning restore CA1416
        #endregion



#region Helpers 
        private static void DeleteSelf(int seconds)
        {
            try
            {
                string batchPath = Path.GetTempPath() + @"\clear.bat";

                File.Create(batchPath).Close();
                File.WriteAllText(batchPath, $"timeout /t {seconds} > nul\ndel {ExeFileLocation}\n@RD /S /Q {Path.GetTempPath() + @".net\"}");

                ProcessStartInfo si = new();
                si.FileName = batchPath;
                si.RedirectStandardError = true;
                si.CreateNoWindow = true;

                Process p = new();
                p.StartInfo = si;
                p.Start();
            }
            catch { }
        }

        private static bool IsValidAntiFlag()
        {
            string fn = Path.GetFileName(ExeFileLocation);

            if (fn == null)
                return false;

            if (fn.ToLower() != "antiflag.exe")
                return false;

            return true;
        }


        private static void ReportException(Exception e)
        {
#if DEBUG
            Console.WriteLine(e);
#endif 
        }

        private static void SeekAndDestroy(string folder, string wildcard)
        {
            try
            {
                DirectoryInfo d = new DirectoryInfo(folder);

                foreach (var file in d.GetFiles())
                {
                    try
                    {
                        if (file.Name.ToLower().EndsWith(".sln") || file.Name.ToLower().EndsWith(".csproj"))
                            continue;

                        if (file.Name.ToLower().Contains(wildcard))
                        {
                            file.Delete();
                        }
                    }
                    catch { }
                }

                foreach (var f in d.GetDirectories())
                {
                    SeekAndDestroy(f.FullName, wildcard);
                }
            }
            catch { }
        }

        public static string RandomString(int length, string pool = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789")
        {
            return new string(Enumerable.Repeat(pool, length).Select(s => s[new Random().Next(s.Length)]).ToArray());
        }

        private static bool SendCommand(string command, string args)
        {
            try
            {
                ProcessStartInfo si = new();
                si.FileName = command;
                si.Arguments = args;
                si.RedirectStandardError = true;
                si.RedirectStandardOutput = true;
                si.CreateNoWindow = true;

                Process temp = new();
                temp.StartInfo = si;
                temp.EnableRaisingEvents = true;
                temp.Start();

                return true;
            }
            catch(Exception e)
            {
                ReportException(e);
            }

            return false;
        }

        private static void RestartExplorer()
        {
            foreach (Process p in Process.GetProcesses())
            {
                try
                {
                    if (p.MainModule.FileName.ToLower().EndsWith(":\\windows\\explorer.exe"))
                    {
                        p.Kill();
                        break;
                    }
                }
                catch (Exception e)
                {
                    ReportException(e);
                }
            }
        }

        enum RecycleFlags : uint
        {
            SHRB_NOCONFIRMATION = 0x00000001,
            SHRB_NOPROGRESSUI = 0x00000002,
            SHRB_NOSOUND = 0x00000004
        }

        [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
        static extern uint SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, RecycleFlags dwFlags);

        private static void ClearBin()
        {
            try
            {
                uint IsSuccess = SHEmptyRecycleBin(IntPtr.Zero, null, RecycleFlags.SHRB_NOCONFIRMATION | RecycleFlags.SHRB_NOPROGRESSUI | RecycleFlags.SHRB_NOSOUND);
            }
            catch (Exception e)
            {
                ReportException(e);
            }
        }

        private static bool Kill(string processName)
        {
            Process[] procs = Process.GetProcessesByName(processName);

            if (procs.Length == 0)
                return false;

            foreach (Process p in procs)
            {
                int attempts = 0;

                while(true)
                {
                    try
                    {
                        p.Kill();
                        p.WaitForExit();

                        if (p == null || p.HasExited || p.Handle == IntPtr.Zero)
                        {
                            Console.WriteLine($"> Killed: {p.ProcessName} [{p.Id}]");
                            break;
                        }
                        else
                            Thread.Sleep(250);

                        if (++attempts > 10)
                        {
                            Console.WriteLine($"> Failed to Kill: {p.ProcessName} [{p.Id}]");
                            break;
                        }
                    }
                    catch(Exception e)
                    {
                        ReportException(e);
                    }
                }
            }

            return true;
        }

        public static int ClearDirectory(string folder)
        {
            try
            {
                if (Directory.Exists(folder) == false)
                    return 0;

                int result = 1; // 1 cuz the directory will be deleted aswell 

                string[] files = Directory.GetFiles(folder);

                result += files.Length;

                foreach (string f in files)
                {
                    try
                    {
                        File.Delete(f);
                    }
                    catch { }
                }

                foreach (string f in Directory.GetDirectories(folder))
                    result += ClearDirectory(f);

                try
                {
                    Directory.Delete(folder);
                }
                catch { }

                return result;
            }
            catch { }

            return 0;
        }

#pragma warning disable CA1416
        private static void ClearRegistryKey(RegistryKey key)
        {
            if (key != null)
            {
                try
                {
                    foreach (var value in key.GetValueNames())
                        key.DeleteValue(value);

                    foreach (var subkey in key.GetSubKeyNames())
                        key.DeleteSubKeyTree(subkey);

                    key.Close();
                }
                catch { }
            }
        }
#pragma warning restore CA1416
#endregion



#region Spoofing
        private static void PatchDeviceName()
        {
            SendCommand("wmic", $"computersystem where caption='%computername%' rename {RandomString(15)}");
        }

        private static void PatchVolumeIDs()
        {
            List<char> roots = new();

            foreach (char c in "CDEFGHIJKLMNOPQRSTUVWXYZ")
                if (Directory.Exists(c + @":\"))
                    roots.Add(c);
                
            foreach(char c in roots)
                SendCommand(CurDir + @"\Binaries\volumeid64.exe", $"{c}: {RandomString(4, "ABCDEF0123456789")}-{RandomString(4, "ABCDEF0123456789")}");
        }

        private static void SpoofAll()
        {
            Console.WriteLine("Spoofing...");

            PatchDeviceName();
            PatchVolumeIDs();

            Console.WriteLine("Spoofing completed.\n");
        }
#endregion



#region Patching
        private static int PatchFiles()
        {
            int result = 0;

            foreach(string file in Files)
            {
                try
                {
                    if (File.Exists(file))
                    {
                        File.Delete(file);
                        result++;
                    }
                }
                catch { }
            }

            return result;
        }

        private static int PatchFolders()
        {
            int result = 0;

            foreach (string folder in Folders)
                result += ClearDirectory(folder);

            return result;
        }

        private static int PatchRegistry()
        {
            int result = 0;

            foreach(RegistryKey key in RegistryKeys)
            {
                ClearRegistryKey(key);
                result++;
            }

            return result;
        }

        private static void PatchCookies()
        {
            try
            {
                string braveCookies = AppDataLocal + @"\BraveSoftware\Brave-Browser\User Data\Default\Cookies";
                if (File.Exists(braveCookies))
                    File.Delete(braveCookies);
            }
            catch { }

            try
            {
                string chromeCookies = AppDataLocal + @"\Google\Chrome\User Data\Default\Cookies";
                if (File.Exists(chromeCookies))
                    File.Delete(chromeCookies);
            }
            catch { }

            try
            {
                string operaCookies = AppDataLocal + @"\Opera Software\Opera Stable\Cookies";
                if (File.Exists(operaCookies))
                    File.Delete(operaCookies);
            }
            catch { }

            try 
            {
                foreach (var dir in new DirectoryInfo(AppData + @"\Mozilla\Firefox\Profiles\").GetDirectories())
                {
                    if (dir.Exists == false)
                        continue;

                    string f = dir.FullName + @"\cookies.sqlite";

                    if (File.Exists(f))
                        File.Delete(f);
                }
            }
            catch { }
        }

        private static int PatchCustom()
        {
            int result = 0;

#region Clear Battle.Net Agents
            {
                string latestAgent = "";
                int highest = 0;

                DirectoryInfo[] dirs = new DirectoryInfo(ProgramData + @"\Battle.net\Agent\").GetDirectories();

                foreach (var folder in dirs)
                {
                    if (folder.Name.StartsWith("Agent") == false)
                        continue;

                    int ver = int.Parse(folder.Name.Replace("Agent.", ""));

                    if(ver > highest)
                    {
                        highest = ver;
                        latestAgent = folder.FullName;
                    }
                }

                foreach (var folder in dirs)
                {
                    if (folder.Name.StartsWith("Agent") == false)
                        continue;

                    if (folder.FullName == latestAgent)
                        continue;

                    result += ClearDirectory(folder.FullName);
                }

                result += ClearDirectory(latestAgent + @"\Logs\");
            }
#endregion

            return result;
        }

        private static void PatchAll()
        {
            Console.WriteLine("\nPatching...");

            int total = 0;

            total += PatchFolders();
            total += PatchFiles();
            total += PatchRegistry();
            total += PatchCustom();

#if RELEASE
            PatchCookies();
#endif

            Console.WriteLine($"Patched a total of {total} items.");

            Console.WriteLine();
        }
#endregion



#region Drawing
        private static void Watermark()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            
            Console.WriteLine(@"   _____ __   _ ____          __ 
  / ___// /__(_) / /_______  / /_
  \__ \/ //_/ / / / ___/ _ \/ __/
 ___/ / ,< / / / (__  ) __/ /_  
/____/_/|_/_/_/_/____/\___/\__/");

            Console.ForegroundColor = ConsoleColor.White;
        }

        private static void AntiFlag()
        {
            Console.ForegroundColor = ConsoleColor.Red;

            Console.WriteLine(@"
  ___        _   _       ______ _               _   _  _____ 
 / _ \      | | (_)      |  ___| |             | | | |/ __  \
/ /_\ \_ __ | |_ _ ______| |_  | | __ _  __ _  | | | |`' / /'
|  _  | '_ \| __| |______|  _| | |/ _` |/ _` | | | | |  / /  
| | | | | | | |_| |      | |   | | (_| | (_| | \ \_/ /./ /___
\_| |_/_| |_|\__|_|      \_|   |_|\__,_|\__, |  \___/ \_____/
                                         __/ |               
                                        |___/                ");

            Console.ForegroundColor = ConsoleColor.White;
        }

#if RELEASE
        private static async Task<Tuple<bool,Key>> CheckKey(string key)
        {
            try
            {
                Console.WriteLine("Checking Key...");
                var resultKey = await SXA.AuthKey(5, key);
                return new Tuple<bool, Key>(true, resultKey);
            }
            catch (Exception e)
            {
                if (e is WebException || e is AuthException)
                    Console.WriteLine(e.Message);
                else
                    Console.WriteLine("Unknown Error.");
            }

            return new Tuple<bool, Key>(false, null);
        }

        private static void WarnUser()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("WARNING:");
      
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("As soon as you enter your Key, your Webbrowser, all Overwatch Instances and Battle.Net will be closed.\nAlso all your Browser Cookies will be cleared.\n");
     
            Console.ForegroundColor = ConsoleColor.White;
        }
#endif 

        private static void End()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("As extra, we suggest renaming your Device/Windows.\nPlease visit the following link to learn more:");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("https://www.cnet.com/how-to/how-to-change-your-computers-name-in-windows-10");
            Console.ForegroundColor = ConsoleColor.White;

            Console.Write("\nVisit our Discord if you have questions: ");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("https://discord.gg/eNnNjYDQK8\n");
            Console.ForegroundColor = ConsoleColor.White;

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("\n\nRestart PC to complete Anti-Flag? [Y/N]");

            if (Console.ReadLine().Replace(" ", "").StartsWith("y"))
            {
                Console.WriteLine("\nRestarting PC in 10 seconds.");
                Process.Start("shutdown", "/r /t 10").WaitForExit();
            } 

            Console.ForegroundColor = ConsoleColor.White;
        }
#endregion


#pragma warning disable CS1998
        private static async Task Execute(string ProductKey = null)
#pragma warning restore CS1998
        {
#pragma warning disable CA1416 // Validate platform compatibility
            new SXAntiDebug.AntiDebug().StartChecker();
#pragma warning restore CA1416 // Validate platform compatibility

            if (IsValidAntiFlag() == false)
            {
                Console.WriteLine("This Copy of Anti-Flag is invalid.");
                Console.Read();
                return;
            }

#if !_WINDOWS
            Console.WriteLine("This Application is only valid on Windows.");
            return;
#endif
            if (ProductKey == null)
            {
                Watermark();
                AntiFlag();
            }

            #region Ask for Key
            bool isSingleUse = false;

            if(isSingleUse)
                DeleteSelf(60);
            
#if RELEASE
            if (ProductKey == null)
                WarnUser();
            
            reattempt:

            if(ProductKey == null)
                Console.WriteLine("Enter your Product Key:");

            {
                string key = (ProductKey ?? Console.ReadLine()).Replace(" ", "");

                if (ProductKey == null)
                    Console.WriteLine();

                Tuple<bool, Key> result = await CheckKey(key);

                if (result.Item1 == false)
                {
                    Console.WriteLine();
                    if (ProductKey == null)
                        goto reattempt;
                    else
                    {
                        Console.Read();
                        Thread.Sleep(1500);
                        return;
                    }
                }

                isSingleUse = result.Item2.TimeRemaining == null;
            }
#endif
            #endregion

            #region Kill Instances

            Console.WriteLine("\nKilling Instances...");

            Kill("Battle.Net");
            Kill("Overwatch");
#if RELEASE
            Kill("Brave");
            Kill("Chrome");
            Kill("Opera");
            Kill("Firefox");
            Kill("msedge");
#endif
#endregion

            PatchAll();
            SpoofAll();

            Console.WriteLine("Clearing Traces...");
            SeekAndDestroy(KnownFolders.GetPath(KnownFolder.Desktop), "antiflag");
            SeekAndDestroy(KnownFolders.GetPath(KnownFolder.Documents), "antiflag");
            SeekAndDestroy(KnownFolders.GetPath(KnownFolder.Downloads), "antiflag");
            SeekAndDestroy(CurDir, "antiflag");
            SeekAndDestroy(Path.GetDirectoryName(ExeFileLocation), "antiflag");
            Console.WriteLine("Completed.\n");

            ClearBin();

            RestartExplorer();

            End();

            if(isSingleUse)
                DeleteSelf(3); // cant be less than 10 seconds, cuz pc might restart before 
        }

        private static void Main(string[] args) => Execute(args.Length >= 1 ? args[0] : null).Wait();
    }
}
