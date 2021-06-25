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

namespace AntiFlagV2
{
    class Program
    {
        #region Config 
        private static string AppData { get; }         = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        private static string Roaming { get; }         = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        private static string Documents { get; }       = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        private static string ProgramData { get; }     = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        private static string AppDataLocal { get; }    = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        private static string ProgramFilesX86 { get; } = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);


        private static List<string> Files = new List<string>()
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

        private static List<string> Folders = new List<string>()
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
        private static List<RegistryKey> RegistryKeys = new List<RegistryKey>()
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
            catch { }
        }

        private static bool Kill(string processName)
        {
            Process[] procs = Process.GetProcessesByName(processName);

            if (procs.Count() == 0)
                return false;

            foreach (Process p in procs)
            {
                int attempts = 0;

                while(true)
                {
                    try
                    {
                        p.Kill();

                        Thread.Sleep(100);

                        if (p == null || p.HasExited || p.Handle == IntPtr.Zero)
                        {
                            Console.WriteLine($"> Killed: {p.ProcessName} [{p.Id}]");
                            Thread.Sleep(250);
                            break;
                        }

                        if(++attempts > 10)
                        {
                            Console.WriteLine($"> Failed to Kill: {p.ProcessName} [{p.Id}]");
                            break;
                        }
                    }
                    catch { }
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

                result += files.Count();

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
            catch
            {
                return 0;
            }
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
            {
                result += ClearDirectory(folder);
            }
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

        private static async Task<bool> CheckKey(string key)
        {
            try
            {
                Console.WriteLine("\nChecking Key...");
                await SXA.AuthKey(5, key);
                return true;
            }
            catch (Exception e)
            {
                if (e is WebException || e is AuthException)
                    Console.WriteLine(e.Message);
                else
                    Console.WriteLine("Unknown Error.");
            }

            return false;
        }

        private static void WarnUser()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("WARNING:");
      
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("As soon as you enter your Key, your Webbrowser, all Overwatch Instances and Battle.Net will be closed.\nAlso all your Browser Cookies will be cleared.\n");
     
            Console.ForegroundColor = ConsoleColor.White;
        }

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
                Console.WriteLine("\nRestarting PC in 5 seconds.");
                Process.Start("shutdown", "/r /t 5").WaitForExit();
            }

            Console.ForegroundColor = ConsoleColor.White;
        }
        #endregion


#pragma warning disable CS1998
        private static async Task Execute()
#pragma warning restore CS1998 
        {
            Watermark();
            AntiFlag();

            #region Ask for Key
#if RELEASE
            WarnUser();

            reattempt:

            Console.WriteLine("Enter your Product Key:");

            if (await CheckKey(Console.ReadLine().Replace(" ", "")) == false)
            {
                Console.WriteLine();
                goto reattempt;
            }
#endif
            #endregion

            #region Kill Instances

            Console.WriteLine("\nKilling Instances...");

            Kill("Battle.Net");
            Kill("Overwatch");
#if RELAESE
            Kill("Brave");
            Kill("Chrome");
            Kill("Opera");
            Kill("Firefox");
            Kill("msedge");
#endif 
            #endregion

            PatchAll();

            ClearBin();

            End();
        }

        private static void Main(string[] args) => Execute().Wait();
    }
}
