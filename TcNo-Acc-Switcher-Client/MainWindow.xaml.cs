﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TcNo_Acc_Switcher;
using System.Threading;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32.TaskScheduler;
using TcNo_Acc_Switcher.Pages.Steam;
using TcNo_Acc_Switcher.Shared;
using TcNo_Acc_Switcher_Client.Classes;

using Index = TcNo_Acc_Switcher.Pages.Index;
using Strings = TcNo_Acc_Switcher_Client.Localisation.Strings;

namespace TcNo_Acc_Switcher_Client
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>

    public partial class MainWindow : Window
    {
        readonly Index.UserSteamSettings _persistentSettings = new Index.UserSteamSettings();
        private readonly Thread _server = new Thread(RunServer);
        private static void RunServer() { TcNo_Acc_Switcher.Program.Main(new string[0]); }
        private readonly TrayUsers _trayUsers = new TrayUsers();

        public MainWindowViewModel MainViewmodel = new MainWindowViewModel();

        public MainWindow()
        {
            // Start web server
            _server.IsBackground = true;
            _server.Start();
            
            // Initialise and connect to web server above
            // Somehow check ports and find a different one if it doesn't work? We'll see...
            InitializeComponent();

            // Load settings (If they exist, otherwise creates).
            _persistentSettings = SteamSwitcherFuncs.LoadSettings();
            CheckSteamLocation();


            //MView2.Source = new Uri("http://localhost:44305/");
            MView2.Source = new Uri("http://localhost:5000/");
            MView2.NavigationStarting += UrlChanged;
            MView2.CoreWebView2InitializationCompleted += WebView_CoreWebView2Ready;
            //MView2.MouseDown += MViewMDown;

            this.Height = _persistentSettings.WindowSize.Height;
            this.Width = _persistentSettings.WindowSize.Width;
        }

        #region Windows Shortcuts
        //private void CheckShortcuts()
        //{
        //    //MainViewmodel.DesktopShortcut = ;
        //   //MainViewmodel.StartWithWindows = CheckStartWithWindows();
        //    //MainViewmodel.StartMenuIcon = ShortcutExist(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), @"TcNo Account Switcher\"));
        //}

        // Library class used to see if a shortcut exists.
        private static bool ShortcutExist(string location) => File.Exists(System.IO.Path.Combine(location, "TcNo Account Switcher - Steam.lnk"));
        private static string ParentDirectory(string dir) =>
            dir.Substring(0, dir.LastIndexOf(System.IO.Path.DirectorySeparatorChar));
        private static void WriteShortcut(string exe, string selfLocation, string iconDirectory, string description, string settingsLink, string arguments)
        {
            if (File.Exists(settingsLink)) return;
            if (File.Exists("CreateShortcut.vbs"))
                File.Delete("CreateShortcut.vbs");
            
            string[] lines = {"set WshShell = WScript.CreateObject(\"WScript.Shell\")",
                "set oShellLink = WshShell.CreateShortcut(\"" + settingsLink  + "\")",
                "oShellLink.TargetPath = \"" + exe + "\"",
                "oShellLink.WindowStyle = 1",
                "oShellLink.IconLocation = \"" + iconDirectory + "\"",
                "oShellLink.Description = \"" + description + "\"",
                "oShellLink.WorkingDirectory = \"" + selfLocation + "\"",
                "oShellLink.Arguments = \"" + arguments + "\"",
                "oShellLink.Save()"
            };
            File.WriteAllLines("CreateShortcut.vbs", lines);

            var vbsProcess = new Process
            {
                StartInfo =
                {
                    FileName = "cscript",
                    Arguments = "//nologo \"" + System.IO.Path.GetFullPath("CreateShortcut.vbs") + "\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };


            vbsProcess.Start();
            vbsProcess.StandardOutput.ReadToEnd();
            vbsProcess.Close();

            File.Delete("CreateShortcut.vbs");
            MessageBox.Show("Shortcut created!\n\nLocation: " + settingsLink);
        }

        private static void CreateShortcut(string location)
        {
            Directory.CreateDirectory(location);
            // Starts the main picker, with the Steam argument.
            string selfExe = System.IO.Path.Combine(ParentDirectory(Directory.GetCurrentDirectory()), "TcNo Account Switcher.exe"),
                selfLocation = ParentDirectory(Directory.GetCurrentDirectory()),
                iconDirectory = System.IO.Path.Combine(selfLocation, "wwwroot/favicon.ico"),
                settingsLink = System.IO.Path.Combine(location, "TcNo Account Switcher - Steam.lnk");
            const string description = "TcNo Account Switcher - Steam";
            const string arguments = "+steam";

            WriteShortcut(selfExe, selfLocation, iconDirectory, description, settingsLink, arguments);
        }
        private static void DeleteShortcut(string location, string name, bool delFolder)
        {
            var settingsLink = System.IO.Path.Combine(location, name);
            if (File.Exists(settingsLink))
                File.Delete(settingsLink);
            if (delFolder)
            {
                if (Directory.GetFiles(location).Length == 0)
                    Directory.Delete(location);
                else
                    MessageBox.Show($"{Strings.ErrDeleteFolderNonempty} {location}");
            }
            MessageBox.Show(Strings.InfoShortcutDeleted.Replace("{}", name));
        }
        private static void CreateTrayShortcut(string location)
        {
            string selfExe = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "TcNo Acc Switcher SteamTray.exe"),
                selfLocation = Directory.GetCurrentDirectory(),
                iconDirectory = System.IO.Path.Combine(selfLocation, "icon.ico"),
                settingsLink = System.IO.Path.Combine(location, "TcNo Account Switcher - Steam tray.lnk");
            const string description = "TcNo Account Switcher - Steam tray";
            const string arguments = "";

            WriteShortcut(selfExe, selfLocation, iconDirectory, description, settingsLink, arguments);
        }

        // ICON - Desktop Icon
        private static bool DesktopShortcut_Exists() =>
            ShortcutExist(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
        private static void DesktopShortcut_Toggle()
        {
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (!DesktopShortcut_Exists())
                CreateShortcut(desktopPath);
            else
                DeleteShortcut(desktopPath, "TcNo Account Switcher - Steam.lnk", false);
        }
        // -------------------

        // ICON - Start Menu
        private static bool StartMenuIcon_Exists() => ShortcutExist(
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                @"TcNo Account Switcher\"));
        private static void StartMenuIcon_Toggle()
        {
            string programsPath = Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                shortcutFolder = System.IO.Path.Combine(programsPath, @"TcNo Account Switcher\");
            if (!StartMenuIcon_Exists())
            {
                CreateShortcut(shortcutFolder);
                CreateTrayShortcut(shortcutFolder);
            }
            else
            {
                DeleteShortcut(shortcutFolder, "TcNo Account Switcher - Steam.lnk", false);
                DeleteShortcut(shortcutFolder, "TcNo Account Switcher - Steam tray.lnk", true);
            }
        }
        // -------------------

        // TRAY - Run when Windows starts
        private static bool StartWithWindows_Enabled()
        {
            using (var ts = new TaskService())
            {
                var tasks = ts.RootFolder.Tasks;
                return tasks.Exists("TcNo Account Switcher - Tray start with logon");
            }
        }
        private static void StartWithWindows_Toggle()
        {
            if (!StartWithWindows_Enabled())
            {
                var ts = new TaskService();
                var td = ts.NewTask();
                td.Principal.RunLevel = TaskRunLevel.Highest;
                td.Triggers.AddNew(TaskTriggerType.Logon);
                var programPath = System.IO.Path.GetFullPath("TcNo Acc Switcher SteamTray.exe");
                td.Actions.Add(new ExecAction(programPath));
                ts.RootFolder.RegisterTaskDefinition("TcNo Account Switcher - Steam Tray start with logon", td);
                MessageBox.Show(Strings.InfoTrayWindowsStart);
            }
            else
            {
                var ts = new TaskService();
                ts.RootFolder.DeleteTask("TcNo Account Switcher - Steam Tray start with logon");
                MessageBox.Show(Strings.InfoTrayWindowsStartOff);
            }
        }
        // -------------------
        #endregion











        private void SaveSettings()
        {
            _persistentSettings.WindowSize = new System.Drawing.Size(Convert.ToInt32(this.Width), Convert.ToInt32(this.Height));
            SteamSwitcherFuncs.SaveSettings(_persistentSettings);
        }


        public void PickSteamFolder()
        {
            var oldLocation = _persistentSettings.SteamFolder;

            var validSteamFound = SetAndCheckSteamFolder(true);
            if (validSteamFound) return;
            _persistentSettings.SteamFolder = oldLocation;
            MainViewmodel.InputFolderDialogResponse = oldLocation;
            MessageBox.Show($"{Strings.ErrSteamLocation} {oldLocation}");
        }
        private void CheckSteamLocation()
        {
            var validSteamFound = (File.Exists(_persistentSettings.SteamExe()));
            //bool validSteamFound = false; // Testing
            if (!validSteamFound)
            {
                validSteamFound = SetAndCheckSteamFolder(false);
                if (!validSteamFound)
                {
                    MessageBox.Show(Strings.RequiredPickSteamDir);
                    Environment.Exit(1);
                    // this.Close() won't work, because the main window hasn't appeared just yet. Still needs to be populated with Steam Accounts.
                }
            }
            if (File.Exists("Tray_Users.json"))
                _trayUsers.LoadTrayUsers();
        }

        private bool SetAndCheckSteamFolder(bool manual)
        {
            if (!manual)
            {
                MainViewmodel.SteamNotFound = true;
                const string programFiles = "C:\\Program Files\\Steam\\Steam.exe";
                const string programFiles86 = "C:\\Program Files (x86)\\Steam\\Steam.exe";
                bool exists = File.Exists(programFiles),
                    exists86 = File.Exists(programFiles86);

                if (exists86)
                    _persistentSettings.SteamFolder = Directory.GetParent(programFiles86).ToString();
                else if (exists)
                    _persistentSettings.SteamFolder = Directory.GetParent(programFiles).ToString();

                if (exists86 || exists)
                {
                    SaveSettings();
                    return (File.Exists(_persistentSettings.SteamExe()));
                }
            }
            else
                MainViewmodel.SteamNotFound = false;

            var getInputFolderDialog = new SteamFolderInput { DataContext = MainViewmodel };
            getInputFolderDialog.ShowDialog();
            if (!string.IsNullOrEmpty(MainViewmodel.InputFolderDialogResponse))
            {
                _persistentSettings.SteamFolder = MainViewmodel.InputFolderDialogResponse;
                SaveSettings();
                return (File.Exists(_persistentSettings.SteamExe()));
            }
            else
                return false;
        }


        public class MainWindowViewModel : INotifyPropertyChanged
        {
            public MainWindowViewModel()
            {
                InputFolderDialogResponse = "";
                SteamNotFound = new bool();
                StartAsAdmin = new bool();
                ShowSteamId = new bool();
                ShowVacStatus = new bool();
                StartMenuIcon = new bool();
                StartWithWindows = new bool();
                DesktopShortcut = new bool();
                ProgramVersion = "";
                ForgetAccountEnabled = new bool();
                TrayAccounts = 3;
                TrayAccountAccNames = new bool();
                ImageLifetime = 7;
            }
            public string InputFolderDialogResponse { get; set; }
            public bool ShowSteamId { get; set; }
            public bool ShowVacStatus { get; set; }
            public bool StartMenuIcon { get; set; }
            public bool DesktopShortcut { get; set; }
            public bool StartWithWindows { get; set; }
            public bool StartAsAdmin { get; set; }
            public bool SteamNotFound { get; set; }
            public bool ForgetAccountEnabled { get; set; }
            public string ProgramVersion { get; set; }
            public int TrayAccounts { get; set; }
            public int ImageLifetime { get; set; }
            public bool TrayAccountAccNames { get; set; }
            public event PropertyChangedEventHandler PropertyChanged;
            protected void NotifyPropertyChanged(string info)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(info));
            }
        }










        // For draggable regions:
        // https://github.com/MicrosoftEdge/WebView2Feedback/issues/200
        private void WebView_CoreWebView2Ready(object sender, EventArgs e)
        {
            var eventForwarder = new Headerbar.EventForwarder(new WindowInteropHelper(this).Handle);

            MView2.CoreWebView2.AddHostObjectToScript("eventForwarder", eventForwarder);
        }

        private void UrlChanged(object sender, CoreWebView2NavigationStartingEventArgs args)
        {
            var uri = args.Uri.Split("/").Last();
            Console.WriteLine(args.Uri);
            switch (uri)
            {
                case "Win_min":
                    args.Cancel = true;
                    this.WindowState = WindowState.Minimized;
                    break;
                case "Win_max":
                    args.Cancel = true;
                    this.WindowState = WindowState.Maximized;
                    break;
                case "Win_restore":
                    args.Cancel = true;
                    this.WindowState = WindowState.Normal;
                    break;
                case "Win_close":
                    args.Cancel = true;
                    SaveSettings();
                    Environment.Exit(1);
                    break;
                case "show_settings":
                    args.Cancel = true;
                    var settingsWindow = new Settings { DataContext = MainViewmodel, Owner = this };
                    settingsWindow.ShareMainWindow(this);
                    settingsWindow.ShowDialog();
                    break;

            }
            if (uri.Contains("Win_min"))
            {
                args.Cancel = true;
                this.WindowState = WindowState.Minimized;
            }
        }
    }
}