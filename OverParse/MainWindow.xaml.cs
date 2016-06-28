using Newtonsoft.Json.Linq;
using NHotkey;
using NHotkey.Wpf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Resources;
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
        private List<Combatant> lastCombatants = new List<Combatant>();
        public static Dictionary<string, string> skillDict = new Dictionary<string, string>();
        private List<string> sessionLogFilenames = new List<string>();
        private string lastStatus = "";
        private IntPtr hwndcontainer;
        List<Combatant> workingList;
        ResourceManager MWR;

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
            MWR = new ResourceManager("OverParse.Strings.MainWindow", Assembly.GetExecutingAssembly());

            //this.Dispatcher.UnhandledException += Panic;

            try { Directory.CreateDirectory("Logs"); }
            catch
            {
                MessageBox.Show(MWR.GetString("UI_LOG_FAIL", CultureInfo.CurrentUICulture), MWR.GetString("UI_SetupTitle", CultureInfo.CurrentUICulture), MessageBoxButton.OK, MessageBoxImage.Error);
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

            FileStream filestream = new FileStream("Debug\\log_" + string.Format(CultureInfo.CurrentUICulture, "{0:yyyy-MM-dd_hh-mm-ss-tt}", DateTime.Now) + ".txt", FileMode.Create);
            var streamwriter = new StreamWriter(filestream);
            streamwriter.AutoFlush = true;
            Console.SetOut(streamwriter);
            Console.SetError(streamwriter);

            Console.WriteLine(String.Format(CultureInfo.InvariantCulture, MWR.GetString("CON_VersionStamp", CultureInfo.CurrentUICulture), Assembly.GetExecutingAssembly().GetName().Version));

            if (Properties.Settings.Default.UpgradeRequired && !Properties.Settings.Default.ResetInvoked)
            {
                Console.WriteLine(MWR.GetString("CON_Upgrade", CultureInfo.CurrentUICulture));
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.UpgradeRequired = false;
            }

            Properties.Settings.Default.ResetInvoked = false;

            Console.WriteLine(MWR.GetString("CON_ApplyUI", CultureInfo.CurrentUICulture));
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
                Console.WriteLine(MWR.GetString("CON_outOfBounds", CultureInfo.CurrentUICulture));
                this.Top = 50;
                this.Left = 50;
            }

            Console.WriteLine(AutoEndEncounters.IsChecked = Properties.Settings.Default.AutoEndEncounters);
            Console.WriteLine(SetEncounterTimeout.IsEnabled = AutoEndEncounters.IsChecked);
            Console.WriteLine(SeparateZanverse.IsChecked = Properties.Settings.Default.SeparateZanverse);
            Console.WriteLine(SeparateAIS.IsChecked = Properties.Settings.Default.SeparateAIS);
            Console.WriteLine(ClickthroughMode.IsChecked = Properties.Settings.Default.ClickthroughEnabled);
            Console.WriteLine(LogToClipboard.IsChecked = Properties.Settings.Default.LogToClipboard);
            Console.WriteLine(AlwaysOnTop.IsChecked = Properties.Settings.Default.AlwaysOnTop);
            Console.WriteLine(AutoHideWindow.IsChecked = Properties.Settings.Default.AutoHideWindow);
            Console.WriteLine(MWR.GetString("CON_Setting_END", CultureInfo.CurrentUICulture));

            ShowDamageGraph.IsChecked = Properties.Settings.Default.ShowDamageGraph; ShowDamageGraph_Click(null, null);
            ShowRawDPS.IsChecked = Properties.Settings.Default.ShowRawDPS; ShowRawDPS_Click(null, null);
            CompactMode.IsChecked = Properties.Settings.Default.CompactMode; CompactMode_Click(null, null);
            AnonymizeNames.IsChecked = Properties.Settings.Default.AnonymizeNames; AnonymizeNames_Click(null, null);
            HighlightYourDamage.IsChecked = Properties.Settings.Default.HighlightYourDamage; HighlightYourDamage_Click(null, null);
            HandleWindowOpacity(); HandleListOpacity(); SeparateAIS_Click(null, null);

            Console.WriteLine(String.Format(CultureInfo.CurrentUICulture, MWR.GetString("CON_Launch", CultureInfo.CurrentUICulture), Properties.Settings.Default.LaunchMethod));

            if (Properties.Settings.Default.Maximized)
            {
                WindowState = WindowState.Maximized;
            }

            Console.WriteLine(MWR.GetString("CON_Init_HotKeys", CultureInfo.CurrentUICulture));
            try
            {
                HotkeyManager.Current.AddOrReplace("End Encounter", Key.E, ModifierKeys.Control | ModifierKeys.Shift, EndEncounter_Key);
                HotkeyManager.Current.AddOrReplace("End Encounter (No log)", Key.R, ModifierKeys.Control | ModifierKeys.Shift, EndEncounterNoLog_Key);
                HotkeyManager.Current.AddOrReplace("Debug Menu", Key.F11, ModifierKeys.Control | ModifierKeys.Shift, DebugMenu_Key);
                HotkeyManager.Current.AddOrReplace("Always On Top", Key.A, ModifierKeys.Control | ModifierKeys.Shift, AlwaysOnTop_Key);
            }
            catch
            {
                Console.WriteLine(MWR.GetString("CON_Init_HotKeys_FAIL", CultureInfo.CurrentUICulture));
                MessageBox.Show(MWR.GetString("UI_Init_HotKeys_FAIL", CultureInfo.CurrentUICulture), MWR.GetString("UI_SetupTitle", CultureInfo.CurrentUICulture), MessageBoxButton.OK, MessageBoxImage.Information);
            }

            Console.WriteLine(MWR.GetString("CON_SKILL_Update", CultureInfo.CurrentUICulture));
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
                Console.WriteLine(String.Format(CultureInfo.CurrentUICulture, MWR.GetString("CON_SKILL_Update_FAIL", CultureInfo.CurrentUICulture), ex.ToString()));
                if (File.Exists("skills.csv"))
                {
                    MessageBox.Show(MWR.GetString("UI_SKILL_Update_FAIL_LOCAL", CultureInfo.CurrentUICulture), MWR.GetString("UI_SetupTitle", CultureInfo.CurrentUICulture), MessageBoxButton.OK, MessageBoxImage.Information);
                    tmp = File.ReadAllLines("skills.csv");
                }
                else
                {
                    var CR = new ResourceManager("OverParse.Strings.Combatant", Assembly.GetExecutingAssembly());
                    var BoxText = String.Format(CultureInfo.InvariantCulture, MWR.GetString("UI_SKILL_Update_FAIL", CultureInfo.CurrentUICulture), CR.GetString("UI_Unknown", CultureInfo.CurrentUICulture));
                    MessageBox.Show(BoxText, MWR.GetString("UI_SetupTitle", CultureInfo.CurrentUICulture), MessageBoxButton.OK, MessageBoxImage.Information);
                    tmp = new string[0];
                }
            }

            Console.WriteLine(MWR.GetString("CON_SKILL_PROCESS", CultureInfo.CurrentUICulture));

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
            Console.WriteLine(String.Format(CultureInfo.CurrentUICulture, MWR.GetString("CON_SKILL_COUNT", CultureInfo.CurrentUICulture), skillDict.Count()));

            Console.WriteLine(MWR.GetString("CON_LOG_INIT", CultureInfo.CurrentUICulture));
            encounterlog = new Log(Properties.Settings.Default.Path);
            UpdateForm(null, null);

            Console.WriteLine(MWR.GetString("CON_damageTimer_INIT", CultureInfo.CurrentUICulture));
            System.Windows.Threading.DispatcherTimer damageTimer = new System.Windows.Threading.DispatcherTimer();
            damageTimer.Tick += new EventHandler(UpdateForm);
            damageTimer.Interval = new TimeSpan(0, 0, 1);
            damageTimer.Start();

            Console.WriteLine(MWR.GetString("CON_inactiveTimer_INIT", CultureInfo.CurrentUICulture));
            System.Windows.Threading.DispatcherTimer inactiveTimer = new System.Windows.Threading.DispatcherTimer();
            inactiveTimer.Tick += new EventHandler(HideIfInactive);
            inactiveTimer.Interval = TimeSpan.FromMilliseconds(200);
            inactiveTimer.Start();

            Console.WriteLine(MWR.GetString("CON_logCheckTimer_INIT", CultureInfo.CurrentUICulture));
            System.Windows.Threading.DispatcherTimer logCheckTimer = new System.Windows.Threading.DispatcherTimer();
            logCheckTimer.Tick += new EventHandler(CheckForNewLog);
            logCheckTimer.Interval = new TimeSpan(0, 0, 10);
            logCheckTimer.Start();

            Console.WriteLine(MWR.GetString("CON_Update_INIT", CultureInfo.CurrentUICulture));
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://api.github.com/repos/tyronesama/overparse/releases/latest");
                request.UserAgent = "OverParse";
                WebResponse response = request.GetResponse();
                Stream dataStream = response.GetResponseStream();
                StreamReader reader = new StreamReader(dataStream);
                string responseFromServer = reader.ReadToEnd();
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

                Console.WriteLine(String.Format(CultureInfo.CurrentUICulture, MWR.GetString("CON_Update_DIFF", CultureInfo.CurrentUICulture), responseVersion, thisVersion));
                if (responseVersion != thisVersion)
                {
                    var SF = String.Format(CultureInfo.CurrentUICulture, MWR.GetString("UI_UpdateReady", CultureInfo.CurrentUICulture), responseVersion, thisVersion);
                    MessageBoxResult result = MessageBox.Show(SF, MWR.GetString("UI_UpdateTitle", CultureInfo.CurrentUICulture), MessageBoxButton.YesNo, MessageBoxImage.Information);
                    if (result == MessageBoxResult.Yes)
                    {
                        Process.Start("https://github.com/TyroneSama/OverParse/releases/latest");
                        Environment.Exit(-1);
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine(String.Format(CultureInfo.CurrentUICulture, MWR.GetString("CON_Update_FAIL", CultureInfo.CurrentUICulture), ex.ToString())); }

            Console.WriteLine(MWR.GetString("CON_MainWindow_END", CultureInfo.CurrentUICulture));
        }

        private void HideIfInactive(object sender, EventArgs e)
        {
            if (!Properties.Settings.Default.AutoHideWindow)
                return;

            string title = WindowsServices.GetActiveWindowTitle();
            string[] relevant = { "OverParse", MWR.GetString("UI_SetupTitle", CultureInfo.CurrentUICulture), "OverParse Error", "Encounter Timeout", "Phantasy Star Online 2" };

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
                Console.WriteLine(String.Format(CultureInfo.CurrentUICulture, MWR.GetString("CON_CheckForNewLog", CultureInfo.CurrentUICulture), log.Name));
                encounterlog = new Log(Properties.Settings.Default.Path);
            }
        }

        private void Panic(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            string errorMessage = string.Format(CultureInfo.CurrentUICulture, MWR.GetString("UI_Panic", CultureInfo.CurrentUICulture), e.Exception.Message);
            Console.WriteLine(MWR.GetString("CON_Panic", CultureInfo.CurrentUICulture));
            Console.WriteLine(e.Exception.ToString());
            MessageBox.Show(errorMessage, MWR.GetString("UI_Panic_Title", CultureInfo.CurrentUICulture), MessageBoxButton.OK, MessageBoxImage.Error);
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
            MessageBoxResult result = MessageBox.Show(MWR.GetString("UI_ResetOverParse", CultureInfo.CurrentUICulture), MWR.GetString("UI_SetupTitle", CultureInfo.CurrentUICulture), MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (result != MessageBoxResult.Yes)
                return;

            Console.WriteLine(MWR.GetString("CON_ResetOverParse", CultureInfo.CurrentUICulture));
            Properties.Settings.Default.Reset();
            Properties.Settings.Default.ResetInvoked = true;
            Properties.Settings.Default.Save();

            Process.Start(Application.ResourceAssembly.Location);
            Application.Current.Shutdown();
        }

        private void EndEncounter_Key(object sender, HotkeyEventArgs e)
        {
            Console.WriteLine(MWR.GetString("CON_EndEncounter_Key", CultureInfo.CurrentUICulture));
            EndEncounter_Click(null, null);
            e.Handled = true;
        }

        private void EndEncounterNoLog_Key(object sender, HotkeyEventArgs e)
        {
            Console.WriteLine(MWR.GetString("CON_EndEncounterNoLog_Key", CultureInfo.CurrentUICulture));
            EndEncounterNoLog_Click(null, null);
            e.Handled = true;
        }

        private void AlwaysOnTop_Key(object sender, HotkeyEventArgs e)
        {
            Console.WriteLine(MWR.GetString("CON_AlwaysOnTop_Key", CultureInfo.CurrentUICulture));
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
            Console.WriteLine(MWR.GetString("CON_DebugMenu_Key"));
            DebugMenu.Visibility = Visibility.Visible;
            e.Handled = true;
        }

        private void AutoHideWindow_Click(object sender, RoutedEventArgs e)
        {
            if (AutoHideWindow.IsChecked && Properties.Settings.Default.AutoHideWindowWarning)
            {
                MessageBox.Show(MWR.GetString("UI_AutoHideWindow_Click", CultureInfo.CurrentUICulture), MWR.GetString("UI_SetupTitle", CultureInfo.CurrentUICulture), MessageBoxButton.OK, MessageBoxImage.Information);
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
            UpdateForm(null, null);
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

            // get a copy of the right combatants
            List<Combatant> targetList = (encounterlog.running ? encounterlog.combatants : lastCombatants);
            workingList = new List<Combatant>();
            foreach (Combatant c in targetList)
            {
                Combatant temp = new Combatant(c.ID, c.Name, c.isTemporary);
                foreach (Attack a in c.Attacks) 
                    temp.Attacks.Add(new Attack(a.ID, a.Damage, a.Timestamp));
                temp.ActiveTime = c.ActiveTime;
                workingList.Add(temp);
            }

            // clear out the list
            CombatantData.Items.Clear();
            //workingList.RemoveAll(c => c.isTemporary != "no");

            // for zanverse dummy and status bar because WHAT IS GOOD STRUCTURE
            int elapsed = 0;
            Combatant stealActiveTimeDummy = workingList.FirstOrDefault();
            if (stealActiveTimeDummy != null)
                elapsed = stealActiveTimeDummy.ActiveTime;
            Console.WriteLine(elapsed);

            // create and sort dummy AIS combatants
            if (Properties.Settings.Default.SeparateAIS)
            {
                List<Combatant> pendingCombatants = new List<Combatant>();

                foreach (Combatant c in workingList)
                {
                    if (!c.isAlly)
                        continue;
                    if (c.AISDamage > 0)
                    {
                        Combatant AISHolder = new Combatant(c.ID, "AIS|" + c.Name, "AIS");
                        List<Attack> targetAttacks = c.Attacks.Where(a => Combatant.AISAttackIDs.Contains(a.ID)).ToList();
                        c.Attacks = c.Attacks.Except(targetAttacks).ToList();
                        AISHolder.Attacks.AddRange(targetAttacks);
                        AISHolder.ActiveTime = elapsed;
                        pendingCombatants.Add(AISHolder);
                    }
                }

                workingList.AddRange(pendingCombatants);
            }

            // force resort here to neatly shuffle AIS parses back into place
            workingList.Sort((x, y) => y.ReadDamage.CompareTo(x.ReadDamage));

            // make dummy zanverse combatant if necessary
            if (Properties.Settings.Default.SeparateZanverse)
            {
                int totalZanverse = workingList.Where(c => c.isAlly == true).Sum(x => x.ZanverseDamage);
                if (totalZanverse > 0)
                {
                    Combatant zanverseHolder = new Combatant("99999999", "Zanverse", "Zanverse");
                    foreach (Combatant c in workingList)
                    {
                        if (c.isAlly)
                        {
                            List<Attack> targetAttacks = c.Attacks.Where(a => a.ID == "2106601422").ToList();
                            zanverseHolder.Attacks.AddRange(targetAttacks);
                            c.Attacks = c.Attacks.Except(targetAttacks).ToList();
                        }
                    }
                    zanverseHolder.ActiveTime = elapsed;
                    workingList.Add(zanverseHolder);
                }
            }

            // get group damage totals
            int totalReadDamage = workingList.Where(c => (c.isAlly || c.isZanverse)).Sum(x => x.ReadDamage);

            // dps calcs!
            foreach (Combatant c in workingList)
            {
                if (c.isAlly || c.isZanverse)
                {
                    c.PercentReadDPS = c.ReadDamage / (float)totalReadDamage * 100;
                }
                else
                {
                    c.PercentDPS = -1;
                    c.PercentReadDPS = -1;
                }
            }

            // damage graph stuff
            Combatant.maxShare = 0;
            foreach (Combatant c in workingList)
            {
                if ((c.isAlly) && c.ReadDamage > Combatant.maxShare)
                    Combatant.maxShare = c.ReadDamage;

                bool filtered = true;
                if (Properties.Settings.Default.SeparateAIS)
                {
                    if (c.isAlly && c.isTemporary == "no" && !HidePlayers.IsChecked)
                        filtered = false;
                    if (c.isAlly && c.isTemporary == "AIS" && !HideAIS.IsChecked)
                        filtered = false;
                    if (c.isZanverse)
                        filtered = false;
                } else
                {
                    if ((c.isAlly || c.isZanverse || !FilterPlayers.IsChecked) && (c.Damage > 0))
                        filtered = false;
                }

                if (!filtered && c.Damage > 0)
                    CombatantData.Items.Add(c);
            }

            // status pane updates
            EncounterIndicator.Fill = new SolidColorBrush(Color.FromArgb(255, 255, 100, 100));
            EncounterStatus.Content = encounterlog.logStatus();

            if (encounterlog.valid && encounterlog.notEmpty)
            {
                EncounterIndicator.Fill = new SolidColorBrush(Color.FromArgb(255, 255, 255, 0));
                
                if (lastStatus == "")
                    EncounterStatus.Content = "Waiting for combat data...";
                else
                    EncounterStatus.Content = String.Format(CultureInfo.InvariantCulture, MWR.GetString("UI_UpdateForm_WAIT", CultureInfo.CurrentUICulture), lastStatus); 

                CombatantData.Items.Refresh();
            }

            if (encounterlog.running)
            {
                EncounterIndicator.Fill = new SolidColorBrush(Color.FromArgb(255, 100, 255, 100));

                TimeSpan timespan = TimeSpan.FromSeconds(elapsed);
                EncounterStatus.Content = timespan.ToString(@"mm\:ss", CultureInfo.InvariantCulture);

                float totalDPS = totalReadDamage / (float)elapsed;

                if (totalDPS > 0)
                {
                    EncounterStatus.Content += String.Format(CultureInfo.InvariantCulture, MWR.GetString("UI_UpdateForm_DPS", CultureInfo.CurrentUICulture), totalDPS.ToString("N2", CultureInfo.InvariantCulture));
                }

                if (Properties.Settings.Default.CompactMode)
                {
                    string FMT = MWR.GetString("UI_UpdateForm_MAX", CultureInfo.CurrentUICulture);
                    foreach (Combatant c in workingList)
                        if (c.isYou)
                            EncounterStatus.Content += String.Format(CultureInfo.InvariantCulture, FMT, c.MaxHitNum.ToString("N0", CultureInfo.InvariantCulture));
                }

                lastStatus = EncounterStatus.Content.ToString();
            }

            // autoend
            if (encounterlog.running)
            {
                if (Properties.Settings.Default.AutoEndEncounters)
                {
                    int unixTimestamp = (int)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                    if ((unixTimestamp - encounterlog.newTimestamp) >= Properties.Settings.Default.EncounterTimeout)
                    {
                        Console.WriteLine(MWR.GetString("CON_UpdateForm", CultureInfo.CurrentUICulture));
                        EndEncounter_Click(null, null);
                    }
                }
            }

        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Console.WriteLine(MWR.GetString("CON_Window_Closing", CultureInfo.CurrentUICulture));

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
            Console.WriteLine(MWR.GetString("CON_EndEncounterNoLog_Click_END", CultureInfo.CurrentUICulture));
            bool temp = Properties.Settings.Default.AutoEndEncounters;
            Properties.Settings.Default.AutoEndEncounters = false;
            UpdateForm(null, null);
            Properties.Settings.Default.AutoEndEncounters = temp;
            Console.WriteLine(MWR.GetString("CON_LOG_REINIT", CultureInfo.CurrentUICulture));
            lastStatus = "";
            encounterlog = new Log(Properties.Settings.Default.Path);
            UpdateForm(null, null);
        }

        private void EndEncounter_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine(MWR.GetString("CON_EndEncounter_Click_END", CultureInfo.CurrentUICulture));
            bool temp = Properties.Settings.Default.AutoEndEncounters;
            Properties.Settings.Default.AutoEndEncounters = false;
            UpdateForm(null, null); // I'M FUCKING STUPID
            Properties.Settings.Default.AutoEndEncounters = temp;
            encounterlog.backupCombatants = encounterlog.combatants;

            List<Combatant> workingListCopy = new List<Combatant>();
            foreach (Combatant c in workingList)
            {
                Combatant temp2 = new Combatant(c.ID, c.Name, c.isTemporary);
                foreach (Attack a in c.Attacks)
                    temp2.Attacks.Add(new Attack(a.ID, a.Damage, a.Timestamp));
                temp2.ActiveTime = c.ActiveTime;
                temp2.PercentReadDPS = c.PercentReadDPS;
                workingListCopy.Add(temp2);
            }
            Console.WriteLine(MWR.GetString("CON_EndEncounter_Click_SAVE", CultureInfo.CurrentUICulture));
            lastCombatants = encounterlog.combatants;
            encounterlog.combatants = workingListCopy;
            string filename = encounterlog.WriteLog();
            if (filename != null)
            {
                if ((SessionLogs.Items[0] as MenuItem).Name == "SessionLogPlaceholder")
                    SessionLogs.Items.Clear();
                int items = SessionLogs.Items.Count;

                string prettyName = filename.Split('/').LastOrDefault();

                sessionLogFilenames.Add(filename);

                var menuItem = new MenuItem() { Name = MWR.GetString("UI_EndEncounter_Click_MENUITEM", CultureInfo.CurrentUICulture) + items.ToString(CultureInfo.InvariantCulture), Header = prettyName };
                menuItem.Click += OpenRecentLog_Click;
                SessionLogs.Items.Add(menuItem);
            }
            if (Properties.Settings.Default.LogToClipboard)
            {
                encounterlog.WriteClipboard();
            }
            Console.WriteLine(MWR.GetString("CON_LOG_REINIT", CultureInfo.CurrentUICulture));
            encounterlog = new Log(Properties.Settings.Default.Path);
            UpdateForm(null, null);
        }

        private void OpenRecentLog_Click(object sender, RoutedEventArgs e)
        {
            string filename = sessionLogFilenames[SessionLogs.Items.IndexOf((e.OriginalSource as MenuItem))];
            Console.WriteLine(String.Format(CultureInfo.CurrentUICulture, MWR.GetString("CON_OpenRecentLog_Click", CultureInfo.CurrentUICulture), filename));
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
            UpdateForm(null, null);
        }

        private void SeparateAIS_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.SeparateAIS = SeparateAIS.IsChecked;
            HideAIS.IsEnabled = SeparateAIS.IsChecked;
            HidePlayers.IsEnabled = SeparateAIS.IsChecked;
            UpdateForm(null, null);
        }

        private void FilterPlayers_Click(object sender, RoutedEventArgs e)
        {
            UpdateForm(null, null);
        }

        private void HidePlayers_Click(object sender, RoutedEventArgs e)
        {
            if (HidePlayers.IsChecked)
                HideAIS.IsChecked = false;
            UpdateForm(null, null);
        }

        private void HideAIS_Click(object sender, RoutedEventArgs e)
        {
            if (HideAIS.IsChecked)
                HidePlayers.IsChecked = false;
            UpdateForm(null, null);
        }

        private void SetEncounterTimeout_Click(object sender, RoutedEventArgs e)
        {
            AlwaysOnTop.IsChecked = false;

            int x;
            string input = Microsoft.VisualBasic.Interaction.InputBox(MWR.GetString("UI_SetEncounterTimeout_Click", CultureInfo.CurrentUICulture), MWR.GetString("UI_SetEncounterTimeout_Click_Title", CultureInfo.CurrentUICulture), Properties.Settings.Default.EncounterTimeout.ToString(CultureInfo.InvariantCulture));
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
            var MsgText = String.Format(CultureInfo.InvariantCulture, MWR.GetString("UI_About_Click", CultureInfo.CurrentUICulture), version);
            MessageBox.Show(MsgText, MWR.GetString("UI_Title", CultureInfo.CurrentUICulture));
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
            var result = String.Format(CultureInfo.CurrentCulture, MWR.GetString("UI_WindowStats_menubar_wh", CultureInfo.CurrentUICulture), MenuBar.Width.ToString(CultureInfo.CurrentCulture), MenuBar.Height.ToString(CultureInfo.CurrentCulture));
            result += String.Format(CultureInfo.CurrentCulture, MWR.GetString("UI_WindowStats_menubar_pm", CultureInfo.CurrentUICulture), MenuBar.Padding, MenuBar.Margin);
            result += String.Format(CultureInfo.CurrentCulture, MWR.GetString("UI_WindowStats_menuitem_wh", CultureInfo.CurrentUICulture), MenuSystem.Width.ToString(CultureInfo.CurrentCulture), MenuSystem.Height.ToString(CultureInfo.CurrentCulture));
            result += String.Format(CultureInfo.CurrentCulture, MWR.GetString("UI_WindowStats_menuitem_pm", CultureInfo.CurrentUICulture), MenuSystem.Padding, MenuSystem.Margin);
            result += String.Format(CultureInfo.CurrentCulture, MWR.GetString("UI_WindowStats_menuitem_fb", CultureInfo.CurrentUICulture), AutoEndEncounters.Foreground, AutoEndEncounters.Background);
            result += String.Format(CultureInfo.CurrentCulture, MWR.GetString("UI_WindowStats_menuitem_fs", CultureInfo.CurrentUICulture), MenuSystem.FontFamily, MenuSystem.FontSize, MenuSystem.FontWeight, MenuSystem.FontStyle);
            result += String.Format(CultureInfo.CurrentCulture, MWR.GetString("UI_WindowStats_image", CultureInfo.CurrentUICulture), image.Width, image.Height, image.Margin);
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