﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using Newtonsoft.Json;

namespace TcNo_Acc_Switcher_Globals
{
    internal static class NativeMethods
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GetConsoleWindow();
    }

    public class Globals
    {
#pragma warning disable CA2211 // Non-constant fields should not be visible - This is necessary due to it being a launch parameter.
	    public static bool VerboseMode;
#pragma warning restore CA2211 // Non-constant fields should not be visible
	    public static readonly string Version = "2021-07-08_00";
	    public static readonly string[] PlatformList = {"Steam", "Origin", "Ubisoft", "BattleNet", "Epic", "Riot", "Discord"};

	    #region LOGGER

	    public static void DebugWriteLine(string s)
	    {
		    // Toggle here so it only shows in Verbose mode etc.
		    if (VerboseMode) Console.WriteLine(s);
	    }

	    /// <summary>
	    /// Append line to log file
	    /// </summary>
	    public static void WriteToLog(string s)
	    {
		    var attempts = 0;
		    while (attempts <= 30) // Up to 3 seconds
		    {
			    try
			    {
				    File.AppendAllText(Path.Join(UserDataFolder, "log.txt"), s + Environment.NewLine);
				    break;
			    }
			    catch (IOException)
			    {
				    if (attempts == 5)
					    throw;
				    attempts++;
				    System.Threading.Thread.Sleep(100);
			    }
		    }

		    if (NativeMethods.GetConsoleWindow() != IntPtr.Zero) // Console exists
			    Console.WriteLine(s);
	    }

	    /// <summary>
	    /// Clear log files & Combine other log files.
	    /// </summary>
	    public static void ClearLogs()
	    {
		    // Clear original log file
		    if (File.Exists("log.txt")) File.Delete("log.txt");
		    // Check for other log files. Combine them and delete them.
		    var filepath = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location);
		    if (filepath == null) return;
		    var d = new DirectoryInfo(filepath);
		    var appendText = new List<string>();
		    var oldFilesFound = false;
		    foreach (var file in d.GetFiles("log*.txt"))
		    {
			    if (appendText.Count == 0) appendText.Add(Environment.NewLine + "-------- OLD --------");
			    try
			    {
				    appendText.AddRange(File.ReadAllLines(file.FullName));
				    File.Delete(file.FullName);
				    oldFilesFound = true;
			    }
			    catch (Exception)
			    {
				    //
			    }
		    }

		    if (oldFilesFound) appendText.Add(Environment.NewLine + "-------- END OF OLD --------");

		    // Insert their contents into the actual log file
		    try
		    {
			    File.WriteAllLines("log.txt", appendText);
		    }
		    catch (IOException)
		    {
			    // Could not write to log file.
			    // Probably in use.
			    // Just ignore. Don't crash.
			    // Crashing happened way too often because of this. Makes no sense to log these elsewhere.
		    }
	    }

	    /// <summary>
	    /// Exception handling for all programs
	    /// </summary>
	    public static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
	    {
		    // Set working directory to documents folder
		    Directory.SetCurrentDirectory(UserDataFolder);

		    // Log Unhandled Exception
		    try
		    {
			    var exceptionStr = e.ExceptionObject.ToString();
			    Directory.CreateDirectory("CrashLogs");
			    var filePath = $"CrashLogs\\AccSwitcher-Crashlog-{DateTime.Now:dd-MM-yy_hh-mm-ss.fff}.txt";
			    if (File.Exists(filePath))
			    {
				    Random r = new();
                    filePath = $"CrashLogs\\AccSwitcher-Crashlog-{DateTime.Now:dd-MM-yy_hh-mm-ss.fff}{r.Next(0, 100)}.txt";
			    }

                using (var sw = File.AppendText(filePath))
			    {
				    sw.WriteLine(
					    $"{DateTime.Now.ToString(CultureInfo.InvariantCulture)}({Version})\t{Strings.ErrUnhandledCrash}: {exceptionStr}{Environment.NewLine}{Environment.NewLine}");
			    }

			    WriteToLog(Strings.ErrUnhandledException + Path.GetFullPath(filePath));
			    WriteToLog(Strings.ErrSubmitCrashlog);

				MessageBox.Show("This crashlog will be automatically submitted next launch." + Environment.NewLine + Environment.NewLine + "Error: " + e.ExceptionObject, "Fatal error occurred!", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
            }
		    catch (Exception)
		    {
			    // This is just to prevent a complete crash. Sometimes there are multiple errors super close together, that cause it to break and we end up here.
		    }
	    }

	    #endregion

	    #region INITIALISATION

	    private static string _userDataFolder = "";
	    public static string UserDataFolder
        {
		    get {
			    
			    if (!string.IsNullOrEmpty(_userDataFolder)) return _userDataFolder;
			    // Has not yet been initialised
			    // Check if set to something different
			    if (File.Exists(Path.Join(AppDataFolder, "userdata_path.txt")))
				    _userDataFolder = File.ReadAllLines(Path.Join(AppDataFolder, "userdata_path.txt"))[0].Trim();
			    else
				    _userDataFolder = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TcNo Account Switcher\\");

			    return _userDataFolder;
		    }
		    set
		    {
			    _userDataFolder = value;
			    try
			    {
				    File.WriteAllText(Path.Join(AppDataFolder, "userdata_path.txt"), value);
                }
			    catch (Exception)
			    {
                    // Failed to write to file.
                    // This setter will be unused for now at least, so this isn't really necessary.
			    }
		    }
	    }
        
	    public static string AppDataFolder =>
		    Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location) ?? string.Empty;

	    public static string OriginalWwwroot => Path.Join(AppDataFolder, "originalwwwroot");
        
	    public static bool InstalledToProgramFiles()
	    {
		    var progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
		    var progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
		    return AppDataFolder.Contains(progFiles) || AppDataFolder.Contains(progFilesX86);
	    }

	    public static bool HasFolderAccess(string folderPath)
	    {
		    try
		    {
			    using var fs = File.Create(Path.Combine(folderPath, Path.GetRandomFileName()), 1, FileOptions.DeleteOnClose);
			    return true;
		    }
		    catch
		    {
			    return false;
		    }
	    }

        /// <summary>
        /// Move pre 2021-07-05 Documents userdata folder into AppData.
        /// </summary>
	    private static void OldDocumentsToAppData()
	    {
		    // Check to see if folder still located in My Documents (from Pre 2021-07-05)
		    var oldDocuments = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TcNo Account Switcher\\");
		    if (Directory.Exists(oldDocuments)) CopyFilesRecursive(oldDocuments, UserDataFolder, true);
            RecursiveDelete(new DirectoryInfo(oldDocuments), false);
        }

        /// <summary>
        /// Creates data folder in AppData
        /// </summary>
        /// <param name="overwrite">Whether files should be overwritten or not (Update)</param>
        public static void CreateDataFolder(bool overwrite)
        {
	        OldDocumentsToAppData();

	        var wwwroot = Directory.Exists(Path.Join(AppDataFolder, "wwwroot"))
				? Path.Join(AppDataFolder, "wwwroot")
				: Path.Join(AppDataFolder, "originalwwwroot");
			if (!Directory.Exists(UserDataFolder)) Directory.CreateDirectory(UserDataFolder);
		    else
		    {
			    var appFilesHash = GetDataFolderMd5(wwwroot) + GetDataFolderMd5(Path.Join(AppDataFolder, "themes"));
			    var userFilesHash = "";

                if (Directory.Exists(Path.Join(UserDataFolder, "wwwroot")) && Directory.Exists(Path.Join(UserDataFolder, "themes")))
				{
					userFilesHash = GetDataFolderMd5(Path.Join(UserDataFolder, "wwwroot")) +
					                GetDataFolderMd5(Path.Join(UserDataFolder, "themes"));
				}

			    if (appFilesHash != userFilesHash)
				    overwrite = true;
		    }

		    // Initialise folder:
		    InitWwwroot(wwwroot, overwrite);
		    InitFolder("themes", overwrite);
	    }

	    public static void ClearWebCache()
	    {
		    var cache = Path.Join(UserDataFolder, "EBWebView\\Default\\Cache");
		    var codeCache = Path.Join(UserDataFolder, "EBWebView\\Default\\Code Cache");
		    try
			{
				if (Directory.Exists(cache)) RecursiveDelete(new DirectoryInfo(cache), true);
				if (Directory.Exists(codeCache)) RecursiveDelete(new DirectoryInfo(codeCache), true);
			}
		    catch (Exception e)
		    {
				   // Clearing cache isn't REQUIRED but it's a nice-to-have.
		    }
	    }

	    /// <summary>
        /// Recursively copies directories from install dir to documents dir.
        /// </summary>
        /// <param name="f">Folder to recursively copy</param>
        /// <param name="overwrite">Whether files should be overwritten anyways</param>
        private static void InitFolder(string f, bool overwrite)
        {
	        if (overwrite || !Directory.Exists(Path.Join(UserDataFolder, f))) CopyFilesRecursive(Path.Join(AppDataFolder, f), Path.Join(UserDataFolder, f));
        }
        private static void InitWwwroot(string root, bool overwrite)
        {
	        if (Directory.Exists(root))
		        CopyFilesRecursive(root, Path.Join(UserDataFolder, "wwwroot"), overwrite);
        }

        #endregion

        #region FOLDERS
        /// <summary>
        ///  Gets a hash for the provided Data folder (Ignores specific folders in it).
        /// </summary>
        /// <param name="path">Directory to hash</param>
        /// <returns>Hash string</returns>
        public static string GetDataFolderMd5(string path)
        {
	        // assuming you want to include nested folders
	        var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories).OrderBy(p => p).ToList();

	        var md5 = MD5.Create();
	        byte[] lastBytes = null;
	        foreach (var file in files)
	        {
		        if (lastBytes != null)
		        {
			        md5.TransformBlock(lastBytes, 0, lastBytes.Length, lastBytes, 0);
                }
                
                if (file.Contains("profiles")) // Ignore user-customisable folders.
			        continue;
                
		        var pathBytes = Encoding.UTF8.GetBytes(file[(path.Length + 1)..].ToLower());
		        md5.TransformBlock(pathBytes, 0, pathBytes.Length, pathBytes, 0);

		        try
		        {
			        lastBytes = File.ReadAllBytes(file);
                }
		        catch (IOException)
		        {
			        // File is in use
		        }
	        }

	        if (lastBytes != null)
		        md5.TransformFinalBlock(lastBytes, 0, lastBytes.Length);

            return BitConverter.ToString(md5.Hash ?? Array.Empty<byte>()).Replace("-", "").ToLower();
        }

        public static void RecursiveDelete(DirectoryInfo baseDir, bool keepFolders)
        {
	        if (!baseDir.Exists)
		        return;

	        foreach (var dir in baseDir.EnumerateDirectories())
	        {
		        RecursiveDelete(dir, keepFolders);
	        }
	        var files = baseDir.GetFiles();
	        foreach (var file in files)
	        {
		        if (!file.Exists) continue;
		        file.IsReadOnly = false;
		        file.Delete();
	        }

	        if (keepFolders) return;
	        baseDir.Delete();
        }
        #endregion

        /// <summary>
        /// Gets the unix timestamp string.
        /// </summary>
        public static string GetUnixTime()
		{
			return ((int)DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds).ToString();
		}

        // Overload for below
        public static void CopyFilesRecursive(string inputFolder, string outputFolder) =>
	        CopyFilesRecursive(inputFolder, outputFolder, false);
        /// <summary>
        /// Recursively copy files and directories
        /// </summary>
        /// <param name="inputFolder">Folder to copy files recursively from</param>
        /// <param name="outputFolder">Destination folder</param>
        /// <param name="overwrite">Whether to overwrite files or not</param>
        public static void CopyFilesRecursive(string inputFolder, string outputFolder, bool overwrite)
        {
	        Directory.CreateDirectory(outputFolder);
	        //Now Create all of the directories
	        foreach (var dirPath in Directory.GetDirectories(inputFolder, "*", SearchOption.AllDirectories))
		        Directory.CreateDirectory(dirPath.Replace(inputFolder, outputFolder));

	        //Copy all the files & Replaces any files with the same name
	        foreach (var newPath in Directory.GetFiles(inputFolder, "*.*", SearchOption.AllDirectories))
	        {
		        var dest = newPath.Replace(inputFolder, outputFolder);
		        if (!overwrite && File.Exists(dest)) continue;

		        File.Copy(newPath, dest, true);
            }
        }

        /// <summary>
        /// Kills requested process. Will Write to Log and Console if unexpected output occurs (Doesn't start with "SUCCESS") 
        /// </summary>
        /// <param name="procName">Process name to kill (Will be used as {name}*)</param>
        public static void KillProcess(string procName)
        {
            var outputText = "";
            var startInfo = new ProcessStartInfo
            {
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden,
                FileName = "cmd.exe",
                Arguments = $"/C TASKKILL /F /T /IM {procName}*",
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true
            };
            var process = new Process { StartInfo = startInfo };
            process.OutputDataReceived += (_, e) => outputText += e.Data + "\n";
            process.Start();
            process.BeginOutputReadLine();
            process.WaitForExit();

            WriteToLog(outputText.StartsWith("SUCCESS") || outputText.Length <= 1
                ? $"Successfully closed {procName}."
                : $"Tried to close {procName}. Unexpected output from cmd:\r\n{outputText}");
        }

        public static string GetSha256HashString(string text)
        {
	        if (string.IsNullOrEmpty(text))
		        return string.Empty;

	        using var sha = new SHA256Managed();
	        var textData = Encoding.UTF8.GetBytes(text);
	        var hash = sha.ComputeHash(textData);
	        return BitConverter.ToString(hash).Replace("-", string.Empty);
        }

        #region TRAY
        /// <summary>
        /// Adds a user to the tray cache
        /// </summary>
        /// <param name="platform">Platform to switch account on</param>
        /// <param name="arg">Argument to launch and switch</param>
        /// <param name="name">Name to be displayed in the Tray</param>
        /// <param name="maxAccounts">(Optional) Number of accounts to keep and show in tray</param>
        public static void AddTrayUser(string platform, string arg, string name, int maxAccounts)
        {
            var trayUsers = TrayUser.ReadTrayUsers();
            TrayUser.AddUser(ref trayUsers, platform, new TrayUser { Arg = arg, Name = name }, maxAccounts);
            TrayUser.SaveUsers(trayUsers);
        }

        /// <summary>
        /// Removes a user to the tray cache
        /// </summary>
        /// <param name="platform">Platform to switch account on</param>
        /// <param name="name">Name to be displayed in the Tray</param>
        public static void RemoveTrayUser(string platform, string name)
        {
            var trayUsers = TrayUser.ReadTrayUsers();
            TrayUser.RemoveUser(ref trayUsers, platform, name);
            TrayUser.SaveUsers(trayUsers);
        }

        /// <summary>
        /// Removes a user to the tray cache (By argument)
        /// </summary>
        /// <param name="platform">Platform to switch account on</param>
        /// <param name="arg">Argument this account uses to switch</param>
        public static void RemoveTrayUserByArg(string platform, string arg)
        {
            var trayUsers = TrayUser.ReadTrayUsers();
            TrayUser.RemoveUserByArg(ref trayUsers, platform, arg);
            TrayUser.SaveUsers(trayUsers);
        }
        #endregion

        #region Hide and Show main window
        // For 'minimising to tray' while not being connected to it

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        private const int GwlExStyle = -20;
        public static readonly int WsExAppWindow = 0x00040000, WsExToolWindow = 0x00000080;

        [DllImport("user32")]
        private static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        public static bool BringToFront()
        {
            // This does not work in debug, as the console has the same name
            var proc = Process.GetProcessesByName("TcNo_Acc_Switcher").FirstOrDefault();
            if (proc == null || proc.MainWindowHandle == IntPtr.Zero) return false;
            const int swRestore = 9;
            var hwnd = proc.MainWindowHandle;
            ShowWindow(hwnd); // This seems to take ownership of some kind over the main process... So closing the tray closes the main switcher too ~
            ShowWindow(hwnd, swRestore);
            SetForegroundWindow(hwnd);
            return true;
        }

        public static void HideWindow(IntPtr handle)
        {
	        _ = SetWindowLong(handle, GwlExStyle, (GetWindowLong(handle, GwlExStyle) | WsExToolWindow) & ~WsExAppWindow);
        }

        public static void ShowWindow(IntPtr handle)
        {
	        _ = SetWindowLong(handle, GwlExStyle, (GetWindowLong(handle, GwlExStyle) ^ WsExToolWindow) | WsExAppWindow);
        }

        public static int GetWindow(IntPtr handle) => GetWindowLong(handle, GwlExStyle);

        public static string StartTrayIfNotRunning()
        {
            if (Process.GetProcessesByName("TcNo_Acc_Switcher_Tray").Length > 0) return "Already running";
            if (!File.Exists("Tray_Users.json")) return "Tray users not found";
            var startInfo = new ProcessStartInfo { FileName = "TcNo_Acc_Switcher_Tray.exe", CreateNoWindow = false, UseShellExecute = false };
            try
            {
                Process.Start(startInfo);
                return "Started Tray";
            }
            catch (System.ComponentModel.Win32Exception win32Exception)
            {
                if (win32Exception.HResult != -2147467259) throw; // Throw is error is not: Requires elevation
                try
                {
                    startInfo.UseShellExecute = true;
                    startInfo.Verb = "runas";
                    Process.Start(startInfo);
                    return "Started Tray";
                }

                catch (System.ComponentModel.Win32Exception win32Exception2)
                {
                    if (win32Exception2.HResult != -2147467259) throw; // Throw is error is not: cancelled by user
                }
            }

            return "Could not start tray";
        }
        #endregion

        #region WINDOWS_TRAY_MANAGEMENT
        // See https://stackoverflow.com/a/9500732
        [StructLayout(LayoutKind.Sequential)]
        public struct Rect
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass,
            string lpszWindow);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out Rect lpRect);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, int wParam, int lParam);

        public static void RefreshTrayArea()
        {
            // NOTE:
            // "User Promoted Notification Area", and "Notification Area"
            // Need to be translated into other localised languages to work across computers that are NOT english.
            // Not entirely sure where I can get the other locale strings for Windows for something like this.
            // Not to mention detecting language.
            // So, tldr this only works for English Windows at the moment, which is the majority of users.

            try
            {
                var systemTrayContainerHandle = FindWindow("Shell_TrayWnd", null);
                var systemTrayHandle = FindWindowEx(systemTrayContainerHandle, IntPtr.Zero, "TrayNotifyWnd", null);
                var sysPagerHandle = FindWindowEx(systemTrayHandle, IntPtr.Zero, "SysPager", null);
                var notificationAreaHandle = FindWindowEx(sysPagerHandle, IntPtr.Zero, "ToolbarWindow32", "Notification Area");
                if (notificationAreaHandle == IntPtr.Zero)
                {
                    notificationAreaHandle = FindWindowEx(sysPagerHandle, IntPtr.Zero, "ToolbarWindow32",
                        "User Promoted Notification Area");
                    var notifyIconOverflowWindowHandle = FindWindow("NotifyIconOverflowWindow", null);
                    var overflowNotificationAreaHandle = FindWindowEx(notifyIconOverflowWindowHandle, IntPtr.Zero,
                        "ToolbarWindow32", "Overflow Notification Area");
                    RefreshTrayArea(overflowNotificationAreaHandle);
                }
                RefreshTrayArea(notificationAreaHandle);
            }
            catch (Exception)
            {
                //
            }
        }

        private static void RefreshTrayArea(IntPtr windowHandle)
        {
            const uint wmMousemove = 0x0200;
            GetClientRect(windowHandle, out var rect);
            for (var x = 0; x < rect.right; x += 5)
            for (var y = 0; y < rect.bottom; y += 5)
                SendMessage(windowHandle, wmMousemove, 0, (y << 16) + x);
        }
        #endregion
    }

    public class TrayUser
    {
        public string Name { get; set; } = ""; // Name to display on list
        public string Arg { get; set; } = "";  // Argument used to switch to this account

        /// <summary>
        /// Reads Tray_Users.json, and returns a dictionary of strings, with a list of TrayUsers attached to them.
        /// </summary>
        /// <returns>Dictionary of keys, and associated lists of tray users</returns>
        public static Dictionary<string, List<TrayUser>> ReadTrayUsers()
        {
            if (!File.Exists("Tray_Users.json")) return new Dictionary<string, List<TrayUser>>();
            var json = File.ReadAllText("Tray_Users.json");
            return JsonConvert.DeserializeObject<Dictionary<string, List<TrayUser>>>(json) ?? new Dictionary<string, List<TrayUser>>();
        }
        
        /// <summary>
        /// Adds a user to the beginning of the [Key]s list of TrayUsers. Moves to position 0 if exists.
        /// </summary>
        /// <param name="trayUsers">Reference to Dictionary of keys & list of TrayUsers</param>
        /// <param name="key">Key to add user to</param>
        /// <param name="newUser">user to add to aforementioned key in dictionary</param>
        /// <param name="maxAccounts">(Optional) Number of accounts to keep and show in tray</param>
        public static void AddUser(ref Dictionary<string, List<TrayUser>> trayUsers, string key, TrayUser newUser, int maxAccounts)
        {
            // Create key and add item if doesn't exist already
            if (!trayUsers.ContainsKey(key))
            {
                trayUsers.Add(key, new List<TrayUser>(new[] {newUser}));
                return;
            }

            // If key contains -> Remove it
            trayUsers[key] = trayUsers[key].Where(x => x.Arg != newUser.Arg).ToList();
            // Add item into first slot
            trayUsers[key].Insert(0, newUser);
            // Shorten list to be a max of 3 (default)
            while (trayUsers[key].Count > maxAccounts) trayUsers[key].RemoveAt(trayUsers[key].Count - 1);
        }

        /// <summary>
        /// Remove user from the list of tray users
        /// </summary>
        /// <param name="trayUsers">Reference to list of TrayUsers to modify</param>
        /// <param name="platform">Platform to switch account on</param>
        /// <param name="name">Name to be displayed in the Tray</param>
        public static void RemoveUser(ref Dictionary<string, List<TrayUser>> trayUsers, string platform, string name)
        {
            // Return if does not have requested platform
            if (!trayUsers.ContainsKey(platform)) return;
            var toRemove = trayUsers[platform].Where(x => x.Name == name).ToList();
            if (toRemove.Count == 0) return;
            foreach (var tu in toRemove)
            {
                trayUsers[platform].Remove(tu);
            }
        }

        /// <summary>
        /// Remove user from the list of tray users (By argument)
        /// </summary>
        /// <param name="trayUsers">Reference to list of TrayUsers to modify</param>
        /// <param name="platform">Platform to switch account on</param>
        /// <param name="arg">Argument this account uses to switch</param>
        public static void RemoveUserByArg(ref Dictionary<string, List<TrayUser>> trayUsers, string platform, string arg)
        {
            // Return if does not have requested platform
            if (!trayUsers.ContainsKey(platform)) return;
            var toRemove = trayUsers[platform].Where(x => x.Arg == arg).ToList();
            if (toRemove.Count == 0) return;
            foreach (var tu in toRemove)
            {
                trayUsers[platform].Remove(tu);
            }
        }

        /// <summary>
        /// Saves trayUsers list to file.
        /// </summary>
        public static void SaveUsers(Dictionary<string, List<TrayUser>> trayUsers) => File.WriteAllText("Tray_Users.json", JsonConvert.SerializeObject(trayUsers));
    }
}
