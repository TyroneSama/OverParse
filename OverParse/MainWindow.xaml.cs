using System;
using System.Collections.Generic;
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
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Windows.Interop;
using System.Runtime.InteropServices;

namespace OverParse
{
    public static class WindowsServices
    {
        public const int WS_EX_TRANSPARENT = 0x00000020;
        public const int GWL_EXSTYLE = (-20);

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        public static void SetWindowExTransparent(IntPtr hwnd)
        {
            var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT);
        }
    }

    public partial class MainWindow : Window
    {
        Log encounterlog;
        public static Dictionary<string, string> skillDict = new Dictionary<string, string>();
        IntPtr hwndcontainer;
        bool isClickthrough = false;

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

            Directory.CreateDirectory("logs");

            this.Top = Properties.Settings.Default.Top;
            this.Left = Properties.Settings.Default.Left;
            this.Height = Properties.Settings.Default.Height;
            this.Width = Properties.Settings.Default.Width;

            AutoEndEncounters.IsChecked = Properties.Settings.Default.AutoEndEncounters;
            SeparateZanverse.IsChecked = Properties.Settings.Default.SeparateZanverse;


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

            RoutedCommand newCmd = new RoutedCommand();
            newCmd.InputGestures.Add(new KeyGesture(Key.O, ModifierKeys.Control));
            CommandBindings.Add(new CommandBinding(newCmd, ClickthroughToggle));


            encounterlog = new Log(Properties.Settings.Default.Path);
            Console.WriteLine(Properties.Settings.Default.Path);

            UpdateForm(null, null);
            System.Windows.Threading.DispatcherTimer dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
            dispatcherTimer.Tick += new EventHandler(UpdateForm);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimer.Start();
        }

        private void ClickthroughToggle(object sender, RoutedEventArgs e)
        {
            int extendedStyle = WindowsServices.GetWindowLong(hwndcontainer, WindowsServices.GWL_EXSTYLE);
            if (isClickthrough)
            {
                MessageBox.Show("Clickthrough disabled.");
                WindowsServices.SetWindowLong(hwndcontainer, WindowsServices.GWL_EXSTYLE, extendedStyle & ~WindowsServices.WS_EX_TRANSPARENT);
                isClickthrough = false;
            } else
            {
                MessageBox.Show("Clickthrough enabled. Click the taskbar icon and press CTRL+O to disable.");
                WindowsServices.SetWindowLong(hwndcontainer, WindowsServices.GWL_EXSTYLE, extendedStyle | WindowsServices.WS_EX_TRANSPARENT);
                isClickthrough = true;
            }

            ClickthroughMode.IsChecked = isClickthrough;

        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            Window window = (Window)sender;
            window.Topmost = AlwaysOnTop.IsChecked;
        }

        public void UpdateForm(object sender, EventArgs e)
        {
            encounterlog.UpdateLog(this, null);
            Application.Current.MainWindow.Title = "OverParse WDF Alpha - " + encounterlog.logStatus();
            EncounterStatus.Content = encounterlog.logStatus();

            if (encounterlog.running)
            {
                CombatantData.Items.Clear();

                foreach (Combatant c in encounterlog.combatants)
                {
                    if (Int32.Parse(c.ID) >= 10000000 || FilterPlayers.IsChecked)
                    {
                        CombatantData.Items.Add(c);
                    }
                }
                if (Properties.Settings.Default.AutoEndEncounters) {
                    int unixTimestamp = (int)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                    if ((unixTimestamp - encounterlog.newTimestamp) >= Properties.Settings.Default.EncounterTimeout)
                    {
                        encounterlog = new Log(Properties.Settings.Default.Path);
                    }
                }

            }   
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Select your pso2_bin folder.\n\nThis is the same folder you selected when setting up the Tweaker.\n\nIf you installed to the default location, it will be at \"C:\\PHANTASYSTARONLINE2\\pso2_bin\".", "Help");

            var dlg = new CommonOpenFileDialog();
            dlg.Title = "Select your pso2_bin folder...";
            dlg.IsFolderPicker = true;
            dlg.InitialDirectory = Directory.GetCurrentDirectory();

            dlg.AddToMostRecentlyUsedList = false;
            dlg.AllowNonFileSystemItems = false;
            dlg.DefaultDirectory = Directory.GetCurrentDirectory();
            dlg.EnsureFileExists = true;
            dlg.EnsurePathExists = true;
            dlg.EnsureReadOnly = false;
            dlg.EnsureValidNames = true;
            dlg.Multiselect = false;
            dlg.ShowPlacesList = true;

            if (dlg.ShowDialog() != CommonFileDialogResult.Ok) { return; }

            var folder = dlg.FileName;
            Console.WriteLine(folder);
            Properties.Settings.Default.Path = folder;
            encounterlog = new Log(folder);
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
        }

        private void EndEncounter_Click(object sender, RoutedEventArgs e)
        {
            encounterlog.WriteLog();
            encounterlog = new Log(Properties.Settings.Default.Path);
        }

        private void AutoEndEncounters_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.AutoEndEncounters = AutoEndEncounters.IsChecked;
            MessageBox.Show(Properties.Settings.Default.AutoEndEncounters.ToString());
        }

        private void SeparateZanverse_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.SeparateZanverse = SeparateZanverse.IsChecked;
            MessageBox.Show(Properties.Settings.Default.SeparateZanverse.ToString());
        }

        private void SetEncounterTimeout_Click(object sender, RoutedEventArgs e)
        {
            int x;
            string input = Microsoft.VisualBasic.Interaction.InputBox("How many seconds should the system wait before stopping an encounter?", "Encounter Timeout", Properties.Settings.Default.EncounterTimeout.ToString());
            if (Int32.TryParse(input, out x)) {
                if (x > 0) {
                    Properties.Settings.Default.EncounterTimeout = x;
                } else
                {
                    MessageBox.Show("What.");
                }
            } else
            {
                if (input.Length > 0) { MessageBox.Show("Couldn't parse your input. Enter only a number."); }
            }
        }

        private void Placeholder_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("This doesn't actually do anything yet.");
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("OverParse v.0.0.4.0 - WDF Alpha\nAnything and everything may be broken.\n\nPlease use damage information responsibly.", "OverParse");
        }

        private void Website_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("http://www.tyronesama.moe/");
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void GenerateFakeEntries_Click(object sender, RoutedEventArgs e)
        {
            encounterlog.GenerateFakeEntries();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }
    }

    public class Combatant
    {
        public int Damage { get; set; }
        public int Healing { get; set; }
        public string ID { get; set; }
        public string Name { get; set; }
        public int MaxHitNum { get; set; }
        public string MaxHitID { get; set; }
        public float DPS { get; set; }
        public float PercentDPS { get; set; }

        public string MaxHit
        {
            get
            {
                string attack = "Unknown";
                if (MainWindow.skillDict.ContainsKey(MaxHitID))
                {
                    attack = MainWindow.skillDict[MaxHitID];
                }
                return attack + " - " + MaxHitNum.ToString("N0");
            }
        }

        public string DPSReadout
        {
            get
            {
                if (PercentDPS == -1)
                {
                    return "--";
                } else
                {
                    return string.Format("{0:0.0}", PercentDPS) + "%";
                }
            }
        }

        public string DamageReadout
        {
            get { return Damage.ToString("N0"); }
        }

        public Combatant(string id, string name)
        {
            ID = id;
            Name = name;
            Damage = 0;
            Healing = 0;
            MaxHitNum = 0;
            MaxHitID = "none";
            DPS = 0;
            PercentDPS = -1;
        }
    }

    public class Log
    {
        bool valid;
        public bool running;
        int startTimestamp = 0;
        public int newTimestamp = 0;
        string encounterData;
        StreamReader logreader;
        public List<Combatant> combatants = new List<Combatant>();
        Random random = new Random();

        public Log(string attemptDirectory)
        {
            valid = false;
            DirectoryInfo directory = new DirectoryInfo($"{attemptDirectory}\\damagelogs");
            if (!directory.Exists) { Complain(); return; }
            if (directory.GetFiles().Count() == 0) { Complain(); return; }

            valid = true;
            running = false;

            FileInfo log = directory.GetFiles().OrderByDescending(f => f.LastWriteTime).First();
            Console.WriteLine($"Reading from {log.DirectoryName}\\{log.Name}");
            FileStream fileStream = File.Open(log.DirectoryName + "\\" + log.Name, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            fileStream.Seek(0, SeekOrigin.End);
            logreader = new StreamReader(fileStream);
        }

        public void Complain()
        {
            MessageBox.Show("No damage logs were found.\n\nPlease use \"System > Locate damagelogs folder...\" to select your installation directory, and make sure that the Damage Parser plugin is enabled in the Tweaker.", "Error");
        }

        public void WriteLog()
        {
            if (combatants.Count != 0)
            {
                int elapsed = newTimestamp - startTimestamp;
                TimeSpan timespan = TimeSpan.FromSeconds(elapsed);
                string timer = timespan.ToString(@"mm\:ss");

                string log = string.Format("{0:yyyy-MM-dd_hh-mm-ss-tt}", DateTime.Now) + " | " + timer + Environment.NewLine;
                foreach (Combatant c in combatants)
                {
                    log += $"{c.Name} | {c.Damage} Damage | {c.DPSReadout} Contribution | {c.DPS} DPS | Max Hit: {c.MaxHit}" + Environment.NewLine;
                }

                File.WriteAllText("logs/OverParse Log - " + string.Format("{0:yyyy-MM-dd_hh-mm-ss-tt}", DateTime.Now) + ".txt", log);
            }
        }

        public string logStatus()
        {
            if (!valid)
            {
                return "No damagelogs found...";
            }
            if (!running)
            {
                return "Waiting for combat data...";
            }
            return encounterData;
        }

        public void GenerateFakeEntries()
        {
            for (int i = 0; i <=9 ; i++)
            {
                Combatant temp = new Combatant("1000000" + i.ToString(),"TestPlayer_" + i.ToString());
                temp.PercentDPS = (float)random.Next(0, 10000) / 100;
                temp.DPS = random.Next(0, 1000);
                temp.Damage = random.Next(0, 10000000);
                temp.MaxHitNum = random.Next(0, 1000000);
                temp.MaxHitID = "2368738938";
                combatants.Add(temp);
            }
            for (int i = 0; i <= 9; i++)
            {
                Combatant temp = new Combatant(i.ToString(), "TestEnemy_" + i.ToString());
                temp.PercentDPS = -1;
                temp.DPS = random.Next(0, 1000);
                temp.Damage = random.Next(0, 10000000);
                temp.MaxHitNum = random.Next(0, 1000000);
                temp.MaxHitID = "1612949165";
                combatants.Add(temp);
            }
        }

        public void UpdateLog(object sender, EventArgs e)
        {
            if (!valid) { return; }
            string newLines = logreader.ReadToEnd();
            if (newLines != "")
            {
                string[] result = newLines.Split('\n');
                foreach (string str in result)
                {
                    if (str != "")
                    {
                        string[] parts = str.Split(',');

                        string lineTimestamp = parts[0];
                        string sourceID = parts[2];
                        string sourceName = parts[3];
                        int hitDamage = Int32.Parse(parts[7]);
                        string attackID = parts[6];
                        string isMisc = parts[11];

                        int index = -1;
                        foreach (Combatant x in combatants)
                        {
                            if (x.ID == sourceID) { index = combatants.IndexOf(x); }
                        }

                        if (attackID == "2106601422" && Properties.Settings.Default.SeparateZanverse) {
                            index = -1;
                            foreach (Combatant x in combatants)
                            {
                                if (x.ID == "94857493" && x.Name == "Zanverse") { index = combatants.IndexOf(x); }
                            }
                            sourceID = "94857493";
                            sourceName = "Zanverse";
                        }


                        if (index == -1)
                        {
                            combatants.Add(new Combatant(sourceID, sourceName));
                            index = combatants.Count - 1;
                        }

                        Combatant source = combatants[index];

                        if (hitDamage > 0)
                        {
                            source.Damage += hitDamage;
                            newTimestamp = Int32.Parse(lineTimestamp);
                            if (startTimestamp == 0) { startTimestamp = newTimestamp; }
                            running = true;
                        }
                        else
                        {
                            if (startTimestamp != 0) { source.Healing -= hitDamage; }
                        }

                        if (source.MaxHitNum < hitDamage)
                        {
                            source.MaxHitNum = hitDamage;
                            source.MaxHitID = attackID;
                        }
                    }
                }


                combatants.Sort((x, y) => y.Damage.CompareTo(x.Damage));

                if (startTimestamp != 0 && newTimestamp != startTimestamp)
                {
                    int elapsed = newTimestamp - startTimestamp;
                    float partyDPS = 0;
                    int filtered = 0;

                    TimeSpan timespan = TimeSpan.FromSeconds(elapsed);
                    string timer = timespan.ToString(@"mm\:ss");

                    encounterData = $"{timer}";

                    foreach (Combatant x in combatants)
                    {
                        if (Int32.Parse(x.ID) >= 10000000)
                        {

                            float dps = x.Damage / (newTimestamp - startTimestamp);
                            x.DPS = dps;
                            partyDPS += dps;
                        }
                        else
                        {
                            filtered++;
                        }
                    }

                    foreach (Combatant x in combatants)
                    {
                        if (Int32.Parse(x.ID) >= 10000000)
                        {
                            x.PercentDPS = (x.DPS / partyDPS * 100);
                        }
                        else
                        {
                            x.PercentDPS = -1;
                        }
                    }

                    encounterData += $" - {partyDPS.ToString("0.00")} DPS";
                }

            }
        }
    }
}
