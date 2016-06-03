using Newtonsoft.Json.Linq;
using NHotkey;
using NHotkey.Wpf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace OverParse
{
    public partial class MainWindow : Window
    {
        private Log encounterlog;
        public static Dictionary<string, string> skillDict = new Dictionary<string, string>();
        private List<string> sessionLogFilenames = new List<string>();
        private string lastStatus = "";
        private IntPtr hwndcontainer;

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            // Get this window's handle
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            hwndcontainer = hwnd;
        }

        public MainWindow()
        {
            InitializeComponent();

            this.Dispatcher.UnhandledException += Panic;

            try { Directory.CreateDirectory("Logs"); }
            catch
            {
                MessageBox.Show("OverParse doesn't have write access to its folder, and won't be able to save logs or update skill mappings. This usually happens when you run it from Program Files.\n\nThis is a Windows restriction, and unfortunately I can't do anything about it.\n\nPlease run OverParse as administrator, or move it somewhere else. Sorry for the inconvenience!", "OverParse Setup", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }

            /* ABANDON ALL HOPE YE WHO ENTER HERE
            string pluginURL = "http://107.170.16.100/Plugins/PSO2DamageDump.dll";
            HttpWebRequest dateRequest = (HttpWebRequest)WebRequest.Create(pluginURL);
            dateRequest.Method = "HEAD";
            HttpWebResponse resp = (HttpWebResponse)dateRequest.GetResponse();
            DateTime remoteDate = resp.LastModified.ToUniversalTime();
            DateTime localDate = File.GetLastWriteTimeUtc(@"Resources/PSO2DamageDump.dll");
            if (localDate < remoteDate)
            {
                MessageBox.Show("local file's old");
                WebClient webClient = new WebClient();
                webClient.DownloadFile(pluginURL, @"Resources/PSO2DamageDump.dll");
            } */

            Directory.CreateDirectory("Debug");

            FileStream filestream = new FileStream("Debug\\log_" + string.Format("{0:yyyy-MM-dd_hh-mm-ss-tt}", DateTime.Now) + ".txt", FileMode.Create);
            var streamwriter = new StreamWriter(filestream);
            streamwriter.AutoFlush = true;
            Console.SetOut(streamwriter);
            Console.SetError(streamwriter);

            Console.WriteLine("OVERPARSE V." + Assembly.GetExecutingAssembly().GetName().Version);

            if (Properties.Settings.Default.UpgradeRequired && !Properties.Settings.Default.ResetInvoked)
            {
                Console.WriteLine("Upgrading settings");
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.UpgradeRequired = false;
            }

            Properties.Settings.Default.ResetInvoked = false;

            Console.WriteLine("Applying UI settings");
            Console.WriteLine(this.Top = Properties.Settings.Default.Top);
            Console.WriteLine(this.Left = Properties.Settings.Default.Left);
            Console.WriteLine(this.Height = Properties.Settings.Default.Height);
            Console.WriteLine(this.Width = Properties.Settings.Default.Width);

            bool outOfBounds = (this.Left <= SystemParameters.VirtualScreenLeft - this.Width) ||
                (this.Top <= SystemParameters.VirtualScreenTop - this.Height) ||
                (SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth <= this.Left) ||
                (SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight <= this.Top);

            if (outOfBounds)
            {
                Console.WriteLine("Window's off-screen, resetting");
                this.Top = 50;
                this.Left = 50;
            }

            Console.WriteLine(AutoEndEncounters.IsChecked = Properties.Settings.Default.AutoEndEncounters);
            Console.WriteLine(SetEncounterTimeout.IsEnabled = AutoEndEncounters.IsChecked);
            Console.WriteLine(SeparateZanverse.IsChecked = Properties.Settings.Default.SeparateZanverse);
            Console.WriteLine(ClickthroughMode.IsChecked = Properties.Settings.Default.ClickthroughEnabled);
            Console.WriteLine(LogToClipboard.IsChecked = Properties.Settings.Default.LogToClipboard);
            Console.WriteLine(AlwaysOnTop.IsChecked = Properties.Settings.Default.AlwaysOnTop);
            Console.WriteLine(AutoHideWindow.IsChecked = Properties.Settings.Default.AutoHideWindow);
            Console.WriteLine("Finished applying settings");

            ShowDamageGraph.IsChecked = Properties.Settings.Default.ShowDamageGraph; ShowDamageGraph_Click(null, null);
            ShowRawDPS.IsChecked = Properties.Settings.Default.ShowRawDPS; ShowRawDPS_Click(null, null);
            CompactMode.IsChecked = Properties.Settings.Default.CompactMode; CompactMode_Click(null, null);
            AnonymizeNames.IsChecked = Properties.Settings.Default.AnonymizeNames; AnonymizeNames_Click(null, null);
            HighlightYourDamage.IsChecked = Properties.Settings.Default.HighlightYourDamage; HighlightYourDamage_Click(null, null);
            HandleWindowOpacity(); HandleListOpacity();

            Console.WriteLine($"Launch method: {Properties.Settings.Default.LaunchMethod}");

            if (Properties.Settings.Default.Maximized)
            {
                WindowState = WindowState.Maximized;
            }

            Console.WriteLine("Initializing hotkeys");
            try
            {
                HotkeyManager.Current.AddOrReplace("End Encounter", Key.E, ModifierKeys.Control | ModifierKeys.Shift, EndEncounter_Key);
                HotkeyManager.Current.AddOrReplace("End Encounter (No log)", Key.R, ModifierKeys.Control | ModifierKeys.Shift, EndEncounterNoLog_Key);
                HotkeyManager.Current.AddOrReplace("Debug Menu", Key.F11, ModifierKeys.Control | ModifierKeys.Shift, DebugMenu_Key);
                HotkeyManager.Current.AddOrReplace("Always On Top", Key.A, ModifierKeys.Control | ModifierKeys.Shift, AlwaysOnTop_Key);
            }
            catch
            {
                Console.WriteLine("Hotkeys failed to initialize");
                MessageBox.Show("OverParse failed to initialize hotkeys. This is usually because something else is already using them.\n\nThe program will still work, but hotkeys will not function. Sorry for the inconvenience!", "OverParse Setup", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            Console.WriteLine("Updating skills.csv");
            string[] tmp;
            try
            {
                WebClient client = new WebClient();
                Stream stream = client.OpenRead("https://raw.githubusercontent.com/VariantXYZ/PSO2ACT/master/PSO2ACT/skills.csv");
                StreamReader webreader = new StreamReader(stream);
                String content = webreader.ReadToEnd();

                tmp = content.Split('\n');
                File.WriteAllText("skills.csv", content);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"skills.csv update failed: {ex.ToString()}");
                if (File.Exists("skills.csv"))
                {
                    MessageBox.Show("OverParse failed to update its skill mappings. This usually means your connection hiccuped for a moment.\n\nA local copy will be used instead. If you'd like to try and update again, just relaunch OverParse.", "OverParse Setup", MessageBoxButton.OK, MessageBoxImage.Information);
                    tmp = File.ReadAllLines("skills.csv");
                }
                else
                {
                    MessageBox.Show("OverParse failed to update its skill mappings. This usually means your connection hiccuped for a moment.\n\nSince you have no skill mappings downloaded, all attacks will be marked as \"Unknown\". If you'd like to try and update again, please relaunch OverParse.", "OverParse Setup", MessageBoxButton.OK, MessageBoxImage.Information);
                    tmp = new string[0];
                }
            }

            Console.WriteLine("Parsing skills.csv");

            foreach (string s in tmp)
            {
                string[] split = s.Split(',');
                if (split.Length > 1)
                {
                    skillDict.Add(split[1], split[0]);
                    //Console.WriteLine(s);
                    //Console.WriteLine(split[1] + " " + split[0]);
                }
            }
            Console.WriteLine("Keys in skill dict: " + skillDict.Count());

            Console.WriteLine("Initializing default log");
            encounterlog = new Log(Properties.Settings.Default.Path);
            UpdateForm(null, null);

            Console.WriteLine("Initializing damageTimer");
            System.Windows.Threading.DispatcherTimer damageTimer = new System.Windows.Threading.DispatcherTimer();
            damageTimer.Tick += new EventHandler(UpdateForm);
            damageTimer.Interval = new TimeSpan(0, 0, 1);
            damageTimer.Start();

            Console.WriteLine("Initializing damageTimer");
            System.Windows.Threading.DispatcherTimer inactiveTimer = new System.Windows.Threading.DispatcherTimer();
            inactiveTimer.Tick += new EventHandler(HideIfInactive);
            inactiveTimer.Interval = TimeSpan.FromMilliseconds(200);
            inactiveTimer.Start();

            Console.WriteLine("Initializing logCheckTimer");
            System.Windows.Threading.DispatcherTimer logCheckTimer = new System.Windows.Threading.DispatcherTimer();
            logCheckTimer.Tick += new EventHandler(CheckForNewLog);
            logCheckTimer.Interval = new TimeSpan(0, 0, 10);
            logCheckTimer.Start();

            Console.WriteLine("Checking for release updates");
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://api.github.com/repos/tyronesama/overparse/releases/latest");
                request.UserAgent = "OverParse";
                WebResponse response = request.GetResponse();
                Stream dataStream = response.GetResponseStream();
                StreamReader reader = new StreamReader(dataStream);
                string responseFromServer = reader.ReadToEnd();
                Console.WriteLine(responseFromServer);
                reader.Close();
                response.Close();
                JObject responseJSON = JObject.Parse(responseFromServer);
                string responseVersion = Version.Parse(responseJSON["tag_name"].ToString()).ToString();
                string thisVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();

                while (thisVersion.Substring(Math.Max(0, thisVersion.Length - 2)) == ".0")
                {
                    thisVersion = thisVersion.Substring(0, thisVersion.Length - 2);
                }

                while (responseVersion.Substring(Math.Max(0, responseVersion.Length - 2)) == ".0")
                {
                    responseVersion = responseVersion.Substring(0, responseVersion.Length - 2);
                }

                Console.WriteLine($"JSON version: {responseVersion} / Assembly version: {thisVersion}");
                if (responseVersion != thisVersion)
                {
                    MessageBoxResult result = MessageBox.Show($"There's a new version of OverParse available!\n\nYou're running version {thisVersion}. The latest version is version {responseVersion}.\n\nWould you like to download it now from GitHub?", "OverParse Update", MessageBoxButton.YesNo, MessageBoxImage.Information);
                    if (result == MessageBoxResult.Yes)
                    {
                        Process.Start("https://github.com/TyroneSama/OverParse/releases/latest");
                        Environment.Exit(-1);
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine($"Failed to update check: {ex.ToString()}"); }

            Console.WriteLine("End of MainWindow constructor");
        }

        private void HideIfInactive(object sender, EventArgs e)
        {
            if (!Properties.Settings.Default.AutoHideWindow)
                return;

            string title = WindowsServices.GetActiveWindowTitle();
            string[] relevant = { "OverParse", "OverParse Setup", "OverParse Error", "Encounter Timeout", "Phantasy Star Online 2" };

            if (!relevant.Contains(title))
            {
                this.Opacity = 0;
            }
            else
            {
                HandleWindowOpacity();
            }
        }

        private void CheckForNewLog(object sender, EventArgs e)
        {
            DirectoryInfo directory = encounterlog.logDirectory;
            if (!directory.Exists)
            {
                return;
            }
            if (directory.GetFiles().Count() == 0)
            {
                return;
            }

            FileInfo log = directory.GetFiles().Where(f => Regex.IsMatch(f.Name, @"\d+\.csv")).OrderByDescending(f => f.Name).First();

            if (log.Name != encounterlog.filename)
            {
                Console.WriteLine($"Found a new log file ({log.Name}), switching...");
                encounterlog = new Log(Properties.Settings.Default.Path);
            }
        }

        private void Panic(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            string errorMessage = string.Format("Oops. This is embarassing.\n\nOverParse encountered an unexpected error. If this happens again, please complain to TyroneSama at your earliest convenience. Include your log from OverParse\\logs and the following message:\n\n{0}\n\nSorry for the inconvenience!", e.Exception.Message);
            Console.WriteLine("=== UNHANDLED EXCEPTION ===");
            Console.WriteLine(e.Exception.ToString());
            MessageBox.Show(errorMessage, "OverParse Error - 素晴らしく運がないね君は!", MessageBoxButton.OK, MessageBoxImage.Error);
            Environment.Exit(-1);
        }

        private void UpdatePlugin_Click(object sender, RoutedEventArgs e)
        {
            if (Properties.Settings.Default.LaunchMethod == "Tweaker")
            {
                MessageBox.Show("You can install the parsing plugin from the PSO2 Tweaker's orb menu, under \"Plugins\".\n\nIf you don't use the PSO2 tweaker, use \"Help > Reset OverParse...\" to go through setup again.");
                return;
            }
            encounterlog.UpdatePlugin(Properties.Settings.Default.Path);
            EndEncounterNoLog_Click(this, null);
        }

        private void ResetLogFolder_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.Path = "Z:\\OBVIOUSLY\\BROKEN\\DEFAULT\\PATH";
            EndEncounterNoLog_Click(this, null);
        }

        private void ResetOverParse(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show("Are you SURE you want to reset OverParse?\n\nThis will clear all of your application settings and allow you to reselect your pso2_bin folder, but won't delete your stored logs.", "OverParse Setup", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (result != MessageBoxResult.Yes)
                return;

            Console.WriteLine("Resetting");
            Properties.Settings.Default.Reset();
            Properties.Settings.Default.ResetInvoked = true;
            Properties.Settings.Default.Save();

            Process.Start(Application.ResourceAssembly.Location);
            Application.Current.Shutdown();
        }

        private void EndEncounter_Key(object sender, HotkeyEventArgs e)
        {
            Console.WriteLine("Encounter hotkey pressed");
            EndEncounter_Click(null, null);
            e.Handled = true;
        }

        private void EndEncounterNoLog_Key(object sender, HotkeyEventArgs e)
        {
            Console.WriteLine("Encounter hotkey (no log) pressed");
            EndEncounterNoLog_Click(null, null);
            e.Handled = true;
        }

        private void AlwaysOnTop_Key(object sender, HotkeyEventArgs e)
        {
            Console.WriteLine("Always-on-top hotkey pressed");
            AlwaysOnTop.IsChecked = !AlwaysOnTop.IsChecked;
            IntPtr wasActive = WindowsServices.GetForegroundWindow();

            // hack for activating overparse window
            this.WindowState = WindowState.Minimized;
            this.Show();
            this.WindowState = WindowState.Normal;

            this.Topmost = AlwaysOnTop.IsChecked;
            AlwaysOnTop_Click(null, null);
            WindowsServices.SetForegroundWindow(wasActive);
            e.Handled = true;
        }

        private void DebugMenu_Key(object sender, HotkeyEventArgs e)
        {
            Console.WriteLine("Debug hotkey pressed");
            DebugMenu.Visibility = Visibility.Visible;
            e.Handled = true;
        }

        private void AutoHideWindow_Click(object sender, RoutedEventArgs e)
        {
            if (AutoHideWindow.IsChecked && Properties.Settings.Default.AutoHideWindowWarning)
            {
                MessageBox.Show("This will make the OverParse window invisible whenever PSO2 or OverParse are not in the foreground.\n\nTo show the window, Alt+Tab into OverParse, or click the icon on your taskbar.", "OverParse Setup", MessageBoxButton.OK, MessageBoxImage.Information);
                Properties.Settings.Default.AutoHideWindowWarning = false;
            }
            Properties.Settings.Default.AutoHideWindow = AutoHideWindow.IsChecked;
        }

        private void ClickthroughToggle(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.ClickthroughEnabled = ClickthroughMode.IsChecked;
        }

        private void ShowDamageGraph_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.ShowDamageGraph = ShowDamageGraph.IsChecked;
            UpdateForm(null, null);
        }

        private void ShowRawDPS_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.ShowRawDPS = ShowRawDPS.IsChecked;
            DPSColumn.Header = ShowRawDPS.IsChecked ? "DPS" : "%";
            UpdateForm(null, null);
        }

        private void AlwaysOnTop_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.AlwaysOnTop = AlwaysOnTop.IsChecked;
            this.OnActivated(e);
        }

        private void WindowOpacity_0_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.WindowOpacity = 0;
            HandleWindowOpacity();
        }

        private void WindowOpacity_25_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.WindowOpacity = .25;
            HandleWindowOpacity();
        }

        private void WindowOpacity_50_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.WindowOpacity = .50;
            HandleWindowOpacity();
        }

        private void WindowOpacity_75_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.WindowOpacity = .75;
            HandleWindowOpacity();
        }

        private void WindowOpacity_100_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.WindowOpacity = 1;
            HandleWindowOpacity();
        }

        public void HandleWindowOpacity()
        {
            TheWindow.Opacity = Properties.Settings.Default.WindowOpacity;
            // ACHTUNG ACHTUNG ACHTUNG ACHTUNG ACHTUNG ACHTUNG ACHTUNG ACHTUNG
            WinOpacity_0.IsChecked = false;
            WinOpacity_25.IsChecked = false;
            Winopacity_50.IsChecked = false;
            WinOpacity_75.IsChecked = false;
            WinOpacity_100.IsChecked = false;

            if (Properties.Settings.Default.WindowOpacity == 0)
            {
                WinOpacity_0.IsChecked = true;
            }
            else if (Properties.Settings.Default.WindowOpacity == .25)
            {
                WinOpacity_25.IsChecked = true;
            }
            else if (Properties.Settings.Default.WindowOpacity == .50)
            {
                Winopacity_50.IsChecked = true;
            }
            else if (Properties.Settings.Default.WindowOpacity == .75)
            {
                WinOpacity_75.IsChecked = true;
            }
            else if (Properties.Settings.Default.WindowOpacity == 1)
            {
                WinOpacity_100.IsChecked = true;
            }
        }

        // HAHAHAHAHAHAHAHAHAHAHAHAHA

        private void ListOpacity_0_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.ListOpacity = 0;
            HandleListOpacity();
        }

        private void ListOpacity_25_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.ListOpacity = .25;
            HandleListOpacity();
        }

        private void ListOpacity_50_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.ListOpacity = .50;
            HandleListOpacity();
        }

        private void ListOpacity_75_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.ListOpacity = .75;
            HandleListOpacity();
        }

        private void ListOpacity_100_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.ListOpacity = 1;
            HandleListOpacity();
        }

        public void HandleListOpacity()
        {
            WinBorderBackground.Opacity = Properties.Settings.Default.ListOpacity;
            ListOpacity_0.IsChecked = false;
            ListOpacity_25.IsChecked = false;
            Listopacity_50.IsChecked = false;
            ListOpacity_75.IsChecked = false;
            ListOpacity_100.IsChecked = false;

            if (Properties.Settings.Default.ListOpacity == 0)
            {
                ListOpacity_0.IsChecked = true;
            }
            else if (Properties.Settings.Default.ListOpacity == .25)
            {
                ListOpacity_25.IsChecked = true;
            }
            else if (Properties.Settings.Default.ListOpacity == .50)
            {
                Listopacity_50.IsChecked = true;
            }
            else if (Properties.Settings.Default.ListOpacity == .75)
            {
                ListOpacity_75.IsChecked = true;
            }
            else if (Properties.Settings.Default.ListOpacity == 1)
            {
                ListOpacity_100.IsChecked = true;
            }
        }

        private void HighlightYourDamage_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.HighlightYourDamage = HighlightYourDamage.IsChecked;
            UpdateForm(null, null);
        }

        private void AnonymizeNames_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.AnonymizeNames = AnonymizeNames.IsChecked;
            UpdateForm(null, null);
        }

        private void CompactMode_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.CompactMode = CompactMode.IsChecked;
            if (CompactMode.IsChecked)
            {
                MaxHitHelperColumn.Width = new GridLength(0, GridUnitType.Star);
            }
            else
            {
                MaxHitHelperColumn.Width = new GridLength(3, GridUnitType.Star);
            }
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            Window window = (Window)sender;
            window.Topmost = AlwaysOnTop.IsChecked;
            if (Properties.Settings.Default.ClickthroughEnabled)
            {
                int extendedStyle = WindowsServices.GetWindowLong(hwndcontainer, WindowsServices.GWL_EXSTYLE);
                WindowsServices.SetWindowLong(hwndcontainer, WindowsServices.GWL_EXSTYLE, extendedStyle | WindowsServices.WS_EX_TRANSPARENT);
            }
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
            }
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            HandleWindowOpacity();
            Window window = (Window)sender;
            window.Topmost = AlwaysOnTop.IsChecked;
            if (Properties.Settings.Default.ClickthroughEnabled)
            {
                int extendedStyle = WindowsServices.GetWindowLong(hwndcontainer, WindowsServices.GWL_EXSTYLE);
                WindowsServices.SetWindowLong(hwndcontainer, WindowsServices.GWL_EXSTYLE, extendedStyle & ~WindowsServices.WS_EX_TRANSPARENT);
            }
        }

        public void UpdateForm(object sender, EventArgs e)
        {
            if (encounterlog == null)
                return;

            encounterlog.UpdateLog(this, null);

            EncounterStatus.Content = encounterlog.logStatus();

            // every part of this section is fucking stupid

            EncounterIndicator.Fill = new SolidColorBrush(Color.FromArgb(255, 255, 100, 100));
            EncounterStatus.Content = encounterlog.logStatus();

            if (encounterlog.valid && encounterlog.notEmpty)
            {
                EncounterIndicator.Fill = new SolidColorBrush(Color.FromArgb(255, 255, 255, 0));
                EncounterStatus.Content = $"Waiting - {lastStatus}";
                if (lastStatus == "")
                    EncounterStatus.Content = "Waiting for combat data...";

                CombatantData.Items.Refresh();
            }

            if (encounterlog.running)
            {
                EncounterIndicator.Fill = new SolidColorBrush(Color.FromArgb(255, 100, 255, 100));
                EncounterStatus.Content = encounterlog.logStatus();
                lastStatus = encounterlog.logStatus();
            }

            if (encounterlog.running)
            {
                SeparateZanverse.IsEnabled = false;

                CombatantData.Items.Clear();

                int index = -1; // there's probably a way better way of doing this, maybe someday i'll learn LINQ
                Combatant reorder = null;
                foreach (Combatant c in encounterlog.combatants)
                {
                    if (c.isZanverse)
                    {
                        index = encounterlog.combatants.IndexOf(c);
                        reorder = c;
                    }
                }
                if (index != -1)
                {
                    encounterlog.combatants.RemoveAt(index);
                    encounterlog.combatants.Add(reorder);
                }


                Combatant.maxShare = 0;
                foreach (Combatant c in encounterlog.combatants)
                {
                    if ((c.isAlly && !c.isZanverse) && c.Damage > Combatant.maxShare)
                        Combatant.maxShare = c.Damage;
                    if (c.isAlly || !FilterPlayers.IsChecked)
                    {
                        CombatantData.Items.Add(c);
                    }
                }

                if (Properties.Settings.Default.AutoEndEncounters)
                {
                    int unixTimestamp = (int)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                    if ((unixTimestamp - encounterlog.newTimestamp) >= Properties.Settings.Default.EncounterTimeout)
                    {
                        Console.WriteLine("Automatically ending an encounter");
                        EndEncounter_Click(null, null);
                    }
                }
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Console.WriteLine("Closing...");

            if (!Properties.Settings.Default.ResetInvoked)
            {
                if (WindowState == WindowState.Maximized)
                {
                    Properties.Settings.Default.Top = RestoreBounds.Top;
                    Properties.Settings.Default.Left = RestoreBounds.Left;
                    Properties.Settings.Default.Height = RestoreBounds.Height;
                    Properties.Settings.Default.Width = RestoreBounds.Width;
                    Properties.Settings.Default.Maximized = true;
                }
                else
                {
                    Properties.Settings.Default.Top = this.Top;
                    Properties.Settings.Default.Left = this.Left;
                    Properties.Settings.Default.Height = this.Height;
                    Properties.Settings.Default.Width = this.Width;
                    Properties.Settings.Default.Maximized = false;
                }
            }

            encounterlog.WriteLog();

            Properties.Settings.Default.Save();
        }

        private void LogToClipboard_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.LogToClipboard = LogToClipboard.IsChecked;
        }

        private void EndEncounterNoLog_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("Ending encounter (no log)");
            encounterlog.combatants.Clear();
            bool temp = Properties.Settings.Default.AutoEndEncounters;
            Properties.Settings.Default.AutoEndEncounters = false;
            UpdateForm(null, null);
            Properties.Settings.Default.AutoEndEncounters = temp;
            Console.WriteLine("Reinitializing log");
            SeparateZanverse.IsEnabled = true;
            lastStatus = "";
            encounterlog = new Log(Properties.Settings.Default.Path);
        }

        private void EndEncounter_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("Ending encounter");
            bool temp = Properties.Settings.Default.AutoEndEncounters;
            Properties.Settings.Default.AutoEndEncounters = false;
            UpdateForm(null, null); // I'M FUCKING STUPID
            Properties.Settings.Default.AutoEndEncounters = temp;
            string filename = encounterlog.WriteLog();
            if (filename != null)
            {
                if ((SessionLogs.Items[0] as MenuItem).Name == "SessionLogPlaceholder")
                    SessionLogs.Items.Clear();
                int items = SessionLogs.Items.Count;

                string prettyName = filename.Split('/').LastOrDefault();

                sessionLogFilenames.Add(filename);

                var menuItem = new MenuItem() { Name = "SessionLog_" + items.ToString(), Header = prettyName };
                menuItem.Click += OpenRecentLog_Click;
                SessionLogs.Items.Add(menuItem);
            }
            if (Properties.Settings.Default.LogToClipboard)
            {
                encounterlog.WriteClipboard();
            }
            Console.WriteLine("Reinitializing log");
            SeparateZanverse.IsEnabled = true;
            encounterlog = new Log(Properties.Settings.Default.Path);
        }

        private void OpenRecentLog_Click(object sender, RoutedEventArgs e)
        {
            string filename = sessionLogFilenames[SessionLogs.Items.IndexOf((e.OriginalSource as MenuItem))];
            Console.WriteLine($"attempting to open {filename}");
            Process.Start(Directory.GetCurrentDirectory() + "\\" + filename);
        }

        private void OpenLogsFolder_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(Directory.GetCurrentDirectory() + "\\Logs");
        }

        private void AutoEndEncounters_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.AutoEndEncounters = AutoEndEncounters.IsChecked;
            SetEncounterTimeout.IsEnabled = AutoEndEncounters.IsChecked;
        }

        private void SeparateZanverse_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.SeparateZanverse = SeparateZanverse.IsChecked;
        }

        private void SetEncounterTimeout_Click(object sender, RoutedEventArgs e)
        {
            AlwaysOnTop.IsChecked = false;

            int x;
            string input = Microsoft.VisualBasic.Interaction.InputBox("How many seconds should the system wait before stopping an encounter?", "Encounter Timeout", Properties.Settings.Default.EncounterTimeout.ToString());
            if (Int32.TryParse(input, out x))
            {
                if (x > 0)
                {
                    Properties.Settings.Default.EncounterTimeout = x;
                }
                else
                {
                    MessageBox.Show("What.");
                }
            }
            else
            {
                if (input.Length > 0)
                {
                    MessageBox.Show("Couldn't parse your input. Enter only a number.");
                }
            }

            AlwaysOnTop.IsChecked = Properties.Settings.Default.AlwaysOnTop;
        }

        private void Placeholder_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("This doesn't actually do anything yet.");
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            MessageBox.Show($"OverParse v{version}\nA lightweight self-auditing tool.\n\nShoutouts to WaifuDfnseForce.\nAdditional shoutouts to Variant, AIDA, and everyone else who makes the Tweaker plugin possible.\n\nPlease use damage information responsibly.", "OverParse");
        }

        private void Website_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("http://www.tyronesama.moe/");
        }

        private void PSOWorld_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("http://www.pso-world.com/forums/showthread.php?t=232386");
        }

        private void GitHub_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://github.com/TyroneSama/OverParse");
        }

        private void EQSchedule_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://calendar.google.com/calendar/embed?src=pso2emgquest@gmail.com&mode=agenda");
        }

        private void ArksLayer_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("http://arks-layer.com/");
        }

        private void Bumped_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("http://www.bumped.org/psublog/");
        }

        private void Fulldive_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("http://www.fulldive.nu/");
        }

        private void ShamelessPlug_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("http://twitch.tv/tyronesama");
        }

        private void SWiki_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("http://pso2.swiki.jp/");
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void GenerateFakeEntries_Click(object sender, RoutedEventArgs e)
        {
            encounterlog.GenerateFakeEntries();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void WindowStats_Click(object sender, RoutedEventArgs e)
        {
            string result = "";
            result += $"menu bar: {MenuBar.Width.ToString()} width {MenuBar.Height.ToString()} height\n";
            result += $"menu bar: {MenuBar.Padding} padding {MenuBar.Margin} margin\n";
            result += $"menu item: {MenuSystem.Width.ToString()} width {MenuSystem.Height.ToString()} height\n";
            result += $"menu item: {MenuSystem.Padding} padding {MenuSystem.Margin} margin\n";
            result += $"menu item: {AutoEndEncounters.Foreground} fg {AutoEndEncounters.Background} bg\n";
            result += $"menu item: {MenuSystem.FontFamily} {MenuSystem.FontSize} {MenuSystem.FontWeight} {MenuSystem.FontStyle}\n";
            result += $"image: {image.Width} w {image.Height} h {image.Margin} m\n";
            MessageBox.Show(result);
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void CurrentLogFilename_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(encounterlog.filename);
        }
    }

    public class Attack
    {
        public string ID;
        public int Damage;
        public int Timestamp;

        public Attack(string initID, int initDamage, int initTimestamp)
        {
            ID = initID;
            Damage = initDamage;
            Timestamp = initTimestamp;
        }
    }
}