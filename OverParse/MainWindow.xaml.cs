using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Windows.Interop;
using NHotkey.Wpf;
using NHotkey;
using System.Linq;
using System.Diagnostics;
using System.Net;
using Newtonsoft.Json.Linq;
using System.Reflection;

namespace OverParse
{
    public partial class MainWindow : Window
    {
        Log encounterlog;
        public static Dictionary<string, string> skillDict = new Dictionary<string, string>();
        IntPtr hwndcontainer;
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

            //HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://107.170.16.100/Plugins/PSO2DamageDump.dll");
            //request.Method = "HEAD";
            //HttpWebResponse resp = (HttpWebResponse)request.GetResponse();
            //Console.WriteLine(resp.LastModified);

            try { Directory.CreateDirectory("Logs"); }
            catch
            {
                MessageBox.Show("OverParse doesn't have write access to its folder, and won't be able to save logs. This usually happens when you run it from Program Files.\n\nThis is a Windows restriction, and unfortunately I can't do anything about it.\n\nPlease run OverParse as administrator, or move it somewhere else. Sorry for the inconvenience!", "OverParse Setup", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }

            Directory.CreateDirectory("Debug");

            FileStream filestream = new FileStream("Debug\\log_" + string.Format("{0:yyyy-MM-dd_hh-mm-ss-tt}", DateTime.Now) + ".txt", FileMode.Create);
            var streamwriter = new StreamWriter(filestream);
            streamwriter.AutoFlush = true;
            Console.SetOut(streamwriter);
            Console.SetError(streamwriter);

            Console.WriteLine("OVERPARSE V." + Assembly.GetExecutingAssembly().GetName().Version);

            if (Properties.Settings.Default.UpgradeRequired)
            {
                Console.WriteLine("Upgrading settings");
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.UpgradeRequired = false;
                Properties.Settings.Default.FirstRun = true;
            }

            Console.WriteLine("Loading settings from file");
            this.Top = Properties.Settings.Default.Top;
            this.Left = Properties.Settings.Default.Left;
            this.Height = Properties.Settings.Default.Height;
            this.Width = Properties.Settings.Default.Width;
            AutoEndEncounters.IsChecked = Properties.Settings.Default.AutoEndEncounters;
            SetEncounterTimeout.IsEnabled = AutoEndEncounters.IsChecked;
            SeparateZanverse.IsChecked = Properties.Settings.Default.SeparateZanverse;
            ClickthroughMode.IsChecked = Properties.Settings.Default.ClickthroughEnabled;
            LogToClipboard.IsChecked = Properties.Settings.Default.LogToClipboard;
            AlwaysOnTop.IsChecked = Properties.Settings.Default.AlwaysOnTop;

            CompactMode.IsChecked = Properties.Settings.Default.CompactMode; CompactMode_Click(null, null);
            HandleOpacity();

            if (Properties.Settings.Default.Maximized)
            {
                WindowState = WindowState.Maximized;
            }

            Console.WriteLine("Initializing hotkeys");
            try
            {
                HotkeyManager.Current.AddOrReplace("End Encounter", Key.E, ModifierKeys.Control | ModifierKeys.Shift, EndEncounter_Key);
                HotkeyManager.Current.AddOrReplace("Debug Menu", Key.F11, ModifierKeys.Control | ModifierKeys.Shift, DebugMenu_Key);
            } catch
            {
                Console.WriteLine("Hotkeys failed to initialize");
                MessageBox.Show("OverParse failed to initialize hotkeys. This is usually because something else is already using them.\n\nThe program will still work, but hotkeys will not function. Sorry for the inconvenience!","OverParse Setup",MessageBoxButton.OK,MessageBoxImage.Information);
            }


            Console.WriteLine("Reading skills.csv");
            string[] tmp = File.ReadAllLines("skills.csv");
            foreach (string s in tmp)
            {
                string[] split = s.Split(',');
                skillDict.Add(split[1], split[0]);
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
                    }
                }
            } catch (Exception ex) { Console.WriteLine($"Failed to update check: {ex.ToString()}"); }

            Console.WriteLine("End of MainWindow constructor");
        }

        private void CheckForNewLog(object sender, EventArgs e)
        {
            DirectoryInfo directory = new DirectoryInfo($"{Properties.Settings.Default.Path}\\damagelogs");
            if (!directory.Exists)
            {
                return;
            }
            if (directory.GetFiles().Count() == 0)
            {
                return;
            }

            FileInfo log = directory.GetFiles().OrderByDescending(f => f.Name).First();

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

        private void EndEncounter_Key(object sender, HotkeyEventArgs e)
        {
            Console.WriteLine("Encounter hotkey pressed");
            EndEncounter_Click(null, null);
            e.Handled = true;
        }

        private void DebugMenu_Key(object sender, HotkeyEventArgs e)
        {
            Console.WriteLine("Debug hotkey pressed");
            DebugMenu.Visibility = Visibility.Visible;
            e.Handled = true;
        }

        private void ClickthroughToggle(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.ClickthroughEnabled = ClickthroughMode.IsChecked;
        }

        private void AlwaysOnTop_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.AlwaysOnTop = AlwaysOnTop.IsChecked;
        }

        private void Opacity_0_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.Opacity = 0;
            HandleOpacity();
        }

        private void Opacity_25_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.Opacity = .25;
            HandleOpacity();
        }

        private void Opacity_50_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.Opacity = .50;
            HandleOpacity();
        }

        private void Opacity_75_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.Opacity = .75;
            HandleOpacity();
        }

        private void Opacity_100_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.Opacity = 1;
            HandleOpacity();
        }

        private void HandleOpacity()
        {
            TheWindow.Opacity = Properties.Settings.Default.Opacity;
            // ACHTUNG ACHTUNG ACHTUNG ACHTUNG ACHTUNG ACHTUNG ACHTUNG ACHTUNG
            Opacity_0.IsChecked = false;
            Opacity_25.IsChecked = false;
            Opacity_50.IsChecked = false;
            Opacity_75.IsChecked = false;
            Opacity_100.IsChecked = false;

            if (Properties.Settings.Default.Opacity == 0)
            {
                Opacity_0.IsChecked = true;
            }
            else if (Properties.Settings.Default.Opacity == .25)
            {
                Opacity_25.IsChecked = true;
            } else if (Properties.Settings.Default.Opacity == .50)
            {
                Opacity_50.IsChecked = true;
            }
            else if (Properties.Settings.Default.Opacity == .75)
            {
                Opacity_75.IsChecked = true;
            }
            else if (Properties.Settings.Default.Opacity == 1)
            {
                Opacity_100.IsChecked = true;
            }

        }

        private void CompactMode_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.CompactMode = CompactMode.IsChecked;
            if (CompactMode.IsChecked)
            {
                MaxHitHelper.Width = new GridLength(0, GridUnitType.Star);
            } else
            {
                MaxHitHelper.Width = new GridLength(3, GridUnitType.Star);
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
            encounterlog.UpdateLog(this, null);
            EncounterStatus.Content = encounterlog.logStatus();
            EncounterIndicator.Fill = new SolidColorBrush(Color.FromArgb(255, 255, 100, 100));

            if (encounterlog.valid && encounterlog.notEmpty)
            {
                EncounterIndicator.Fill = new SolidColorBrush(Color.FromArgb(255, 255, 255, 0));
            }

            if (encounterlog.running)
            {
                EncounterIndicator.Fill = new SolidColorBrush(Color.FromArgb(255, 100, 255, 100));
            }

            if (encounterlog.running)
            {
                SeparateZanverse.IsEnabled = false;

                CombatantData.Items.Clear();

                int index = -1; // there's probably a way better way of doing this, maybe someday i'll learn LINQ
                Combatant reorder = null;
                foreach (Combatant c in encounterlog.combatants)
                {
                    if (c.Name == "Zanverse")
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

                foreach (Combatant c in encounterlog.combatants)
                {
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
                        encounterlog = new Log(Properties.Settings.Default.Path);
                    }
                }
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Console.WriteLine("Closing...");
            if (WindowState == WindowState.Maximized)
            {
                // Use the RestoreBounds as the current values will be 0, 0 and the size of the screen
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

            Properties.Settings.Default.Save();
            encounterlog.WriteLog();
        }

        private void LogToClipboard_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.LogToClipboard = LogToClipboard.IsChecked;
        }

        private void EndEncounter_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("Ending encounter");
            encounterlog.WriteLog();
            if (Properties.Settings.Default.LogToClipboard)
            {
                encounterlog.WriteClipboard();
            }
            Console.WriteLine("Reinitializing log");
            SeparateZanverse.IsEnabled = true;
            encounterlog = new Log(Properties.Settings.Default.Path);
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
            MessageBox.Show($"OverParse v{version}\nAnything and everything may be broken.\n\nPlease use damage information responsibly.", "OverParse");
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
            Process.Start("http://www.pso-world.com/forums/showthread.php?t=232386");
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