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

            //HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://107.170.16.100/Plugins/PSO2DamageDump.dll");
            //request.Method = "HEAD";
            //HttpWebResponse resp = (HttpWebResponse)request.GetResponse();
            //Console.WriteLine(resp.LastModified);

            Directory.CreateDirectory("logs");
            this.Top = Properties.Settings.Default.Top;
            this.Left = Properties.Settings.Default.Left;
            this.Height = Properties.Settings.Default.Height;
            this.Width = Properties.Settings.Default.Width;
            AutoEndEncounters.IsChecked = Properties.Settings.Default.AutoEndEncounters;
            SeparateZanverse.IsChecked = Properties.Settings.Default.SeparateZanverse;
            ClickthroughMode.IsChecked = Properties.Settings.Default.ClickthroughEnabled;
            LogToClipboard.IsChecked = Properties.Settings.Default.LogToClipboard;
            AlwaysOnTop.IsChecked = Properties.Settings.Default.AlwaysOnTop;
            HotkeyManager.Current.AddOrReplace("Increment", Key.E, ModifierKeys.Control | ModifierKeys.Shift, EndEncounter_Key);
            if (Properties.Settings.Default.Maximized)
            {
                WindowState = WindowState.Maximized;
            }

            string[] tmp = File.ReadAllLines("skills.csv");
            foreach (string s in tmp)
            {
                string[] split = s.Split(',');
                skillDict.Add(split[1], split[0]);
            }

            encounterlog = new Log(Properties.Settings.Default.Path);
            Console.WriteLine(Properties.Settings.Default.Path);
            UpdateForm(null, null);

            System.Windows.Threading.DispatcherTimer damageTimer = new System.Windows.Threading.DispatcherTimer();
            damageTimer.Tick += new EventHandler(UpdateForm);
            damageTimer.Interval = new TimeSpan(0, 0, 1);
            damageTimer.Start();

            System.Windows.Threading.DispatcherTimer logCheckTimer = new System.Windows.Threading.DispatcherTimer();
            logCheckTimer.Tick += new EventHandler(CheckForNewLog);
            logCheckTimer.Interval = new TimeSpan(0, 0, 10);
            logCheckTimer.Start();
        }

        private void CheckForNewLog(object sender, EventArgs e)
        {
            Console.WriteLine("tick");
            DirectoryInfo directory = new DirectoryInfo($"{Properties.Settings.Default.Path}\\damagelogs");
            if (!directory.Exists)
            {
                return;
            }
            if (directory.GetFiles().Count() == 0)
            {
                return;
            }

            FileInfo log = directory.GetFiles().OrderByDescending(f => f.LastWriteTime).First();

            if (log.Name != encounterlog.filename)
            {
                Console.WriteLine($"BYEEEEEEEEEEEE");
                encounterlog = new Log(Properties.Settings.Default.Path);
            }

        }

        private void EndEncounter_Key(object sender, HotkeyEventArgs e)
        {
            EndEncounter_Click(null, null);
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
                EncounterIndicator.Fill = new SolidColorBrush(Color.FromArgb(255, 255, 255, 100));
            }

            if (encounterlog.running)
            {
                EncounterIndicator.Fill = new SolidColorBrush(Color.FromArgb(255, 100, 255, 100));
            }

            if (encounterlog.running)
            {
                CombatantData.Items.Clear();

                foreach (Combatant c in encounterlog.combatants)
                {
                    if (c.isAlly || FilterPlayers.IsChecked)
                    {
                        CombatantData.Items.Add(c);
                    }
                }

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

                if (Properties.Settings.Default.AutoEndEncounters)
                {
                    int unixTimestamp = (int)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                    if ((unixTimestamp - encounterlog.newTimestamp) >= Properties.Settings.Default.EncounterTimeout)
                    {
                        encounterlog = new Log(Properties.Settings.Default.Path);
                    }
                }
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
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
            encounterlog.WriteLog();
            if (Properties.Settings.Default.LogToClipboard)
            {
                encounterlog.WriteClipboard();
            }

            encounterlog = new Log(Properties.Settings.Default.Path);
        }

        private void OpenLogsFolder_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine(Directory.GetCurrentDirectory() + "\\logs");
            Process.Start(Directory.GetCurrentDirectory() + "\\logs");
        }

        private void AutoEndEncounters_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.AutoEndEncounters = AutoEndEncounters.IsChecked;
        }

        private void SeparateZanverse_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.SeparateZanverse = SeparateZanverse.IsChecked;
        }

        private void SetEncounterTimeout_Click(object sender, RoutedEventArgs e)
        {
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
            System.Diagnostics.Process.Start("http://www.tyronesama.moe/");
        }

        private void PSOWorld_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("http://www.pso-world.com/forums/showthread.php?t=232386");
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
            Console.WriteLine($"Attack generated: {ID} - {Damage}");
        }
    }
}