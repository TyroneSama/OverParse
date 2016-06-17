using Ookii.Dialogs.Wpf;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Text.RegularExpressions;
using System.Windows;

namespace OverParse
{
    public class Log
    {
        private const int pluginVersion = 4;

        public bool notEmpty;
        public bool valid;
        public bool running;
        private int startTimestamp = 0;
        public int newTimestamp = 0;
        public string filename;
        private string encounterData;
        private List<string> instances = new List<string>();
        private StreamReader logReader;
        public List<Combatant> combatants = new List<Combatant>();
        private Random random = new Random();
        public DirectoryInfo logDirectory;
        public List<Combatant> backupCombatants = new List<Combatant>();
        ResourceManager LogR;

        public Log(string attemptDirectory)
        {
            valid = false;
            notEmpty = false;
            running = false;
            LogR = new ResourceManager("OverParse.LogResources", Assembly.GetExecutingAssembly());
            var PathPSO2Binary = String.Format(CultureInfo.InvariantCulture, @"{0}\pso2.exe", attemptDirectory);

            bool nagMe = false;

            while (!File.Exists(PathPSO2Binary))
            {
                Console.WriteLine(LogR.GetString("CON_InvaildPSO2_binPath", CultureInfo.CurrentUICulture));

                if (nagMe)
                {
                    MessageBox.Show("That doesn't appear to be a valid pso2_bin directory.\n\nIf you installed the game using default settings, it will probably be in C:\\PHANTASYSTARONLINE2\\pso2_bin\\. Otherwise, find the location you installed to.", "OverParse Setup", MessageBoxButton.OK, MessageBoxImage.Warning);
                } else
                {
                    MessageBox.Show("Please select your pso2_bin directory. OverParse uses this to read your damage logs.\n\nIf you picked a folder while setting up the Tweaker, choose that. Otherwise, it will be in your PSO2 installation folder.", "OverParse Setup", MessageBoxButton.OK, MessageBoxImage.Information);
                    nagMe = true;
                }
                
                //WINAPI FILE DIALOGS DON'T SHOW UP FOR PEOPLE SOMETIMES AND I HAVE NO IDEA WHY, *** S I C K  M E M E ***
                //VistaFolderBrowserDialog oDialog = new VistaFolderBrowserDialog();
                //oDialog.Description =  LogR.GetString("UI_SelectPSO2_Finder", CultureInfo.CurrentUICulture);
                //oDialog.UseDescriptionForTitle = true;

                System.Windows.Forms.FolderBrowserDialog dialog = new System.Windows.Forms.FolderBrowserDialog();
                dialog.Description =  LogR.GetString("UI_SelectPSO2_Finder", CultureInfo.CurrentUICulture);
                System.Windows.Forms.DialogResult picked = dialog.ShowDialog();
                if (picked == System.Windows.Forms.DialogResult.OK)
                {
                    attemptDirectory = dialog.SelectedPath;
                    Console.WriteLine(String.Format(CultureInfo.InvariantCulture, LogR.GetString("CON_TestPSO2_binPath", CultureInfo.CurrentUICulture), attemptDirectory));
                    Properties.Settings.Default.Path = attemptDirectory;
                } else
                {
                    Console.WriteLine(LogR.GetString("CON_CanceledDirPicker", CultureInfo.CurrentUICulture));
                    MessageBox.Show(LogR.GetString("UI_CanceledDirPicker", CultureInfo.CurrentUICulture), LogR.GetString("UI_SetupTitle", CultureInfo.CurrentUICulture), MessageBoxButton.OK, MessageBoxImage.Information);
                    Application.Current.Shutdown(); // ABORT ABORT ABORT
                    break;
                }
                PathPSO2Binary = String.Format(CultureInfo.InvariantCulture, @"{0}\pso2.exe", attemptDirectory)
            }

            if (!File.Exists(PathPSO2Binary)) { return; }

            valid = true;

            Console.WriteLine(LogR.GetString("CON_damagelog_exist", CultureInfo.CurrentUICulture));
            var Pdamagelog = String.Format(CultureInfo.InvariantCulture, @"{0}\damagelogs", attemptDirectory);
            logDirectory = new DirectoryInfo(Pdamagelog);

            Console.WriteLine(LogR.GetString("CON_check_damage_override", CultureInfo.CurrentUICulture));
            var Pdamagecfg = String.Format(CultureInfo.InvariantCulture, @"{0}\plugins\PSO2DamageDump.cfg", attemptDirectory);
            if (File.Exists(Pdamagecfg))
            {
                Console.WriteLine(LogR.GetString("CON_found_damagecfg", CultureInfo.CurrentUICulture));
                String[] lines = File.ReadAllLines(Pdamagecfg);
                var CON_split = LogR.GetString("CON_split", CultureInfo.CurrentUICulture);
                var CON_found_damage_override = LogR.GetString("CON_found_damage_override", CultureInfo.CurrentUICulture);
                foreach (String s in lines)
                {
                    String[] split = s.Split('=');
                    Console.WriteLine(String.Format(CultureInfo.InvariantCulture, CON_split, split[0], split[1]));
                    if (split.Length < 2)
                        continue;
                    if (split[0].Split('[')[0] == "directory")
                    {
                        logDirectory = new DirectoryInfo(split[1]);
                        
                        Console.WriteLine(String.Format(CultureInfo.CurrentUICulture, CON_found_damage_override, split[1]));
                    }
                }
            }
            else
            {
                Console.WriteLine(LogR.GetString("CON_missing_damagecfg", CultureInfo.CurrentUICulture));
            }

            if (Properties.Settings.Default.LaunchMethod == "Unknown")
            {
                Console.WriteLine(LogR.GetString("CON_LaunchMethod", CultureInfo.CurrentUICulture));
                MessageBoxResult tweakerResult = MessageBox.Show(LogR.GetString("UI_AskTweakerUsage", CultureInfo.CurrentUICulture), LogR.GetString("UI_SetupTitle", CultureInfo.CurrentUICulture), MessageBoxButton.YesNo, MessageBoxImage.Question);
                Properties.Settings.Default.LaunchMethod = (tweakerResult == MessageBoxResult.Yes) ? "Tweaker" : "Manual";
            }

            if (Properties.Settings.Default.LaunchMethod == "Tweaker")
            {
                bool warn = true;
                if (logDirectory.Exists)
                {
                    if (logDirectory.GetFiles().Count() > 0)
                    {
                        warn = false;
                    }
                }

                if (warn && Hacks.DontAsk)
                {
                    Console.WriteLine(LogR.GetString("CON_Hack", CultureInfo.CurrentUICulture));
                    MessageBox.Show(LogR.GetString("UI_Hack", CultureInfo.CurrentUICulture), LogR.GetString("UI_SetupTitle", CultureInfo.CurrentUICulture), MessageBoxButton.OK, MessageBoxImage.Information);
                    Hacks.DontAsk = true;
                    Properties.Settings.Default.FirstRun = false;
                    Properties.Settings.Default.Save();
                    return;
                }
            }
            else if (Properties.Settings.Default.LaunchMethod == "Manual")
            {
                bool pluginsExist = File.Exists(attemptDirectory + "\\pso2h.dll") && File.Exists(attemptDirectory + "\\ddraw.dll") && File.Exists(attemptDirectory + "\\plugins" + "\\PSO2DamageDump.dll");
                if (!pluginsExist)
                    Properties.Settings.Default.InstalledPluginVersion = -1;

                Console.WriteLine(String.Format(CultureInfo.CurrentUICulture, LogR.GetString("CON_Plugin_Status", CultureInfo.CurrentUICulture), Properties.Settings.Default.InstalledPluginVersion, pluginVersion));

                if (Properties.Settings.Default.InstalledPluginVersion < pluginVersion)
                {
                    MessageBoxResult selfdestructResult;

                    if (pluginsExist)
                    {
                        Console.WriteLine(LogR.GetString("CON_Plugin_Prompt_update", CultureInfo.CurrentUICulture));
                        selfdestructResult = MessageBox.Show(LogR.GetString("UI_Plugin_Prompt_update", CultureInfo.CurrentUICulture), LogR.GetString("UI_SetupTitle", CultureInfo.CurrentUICulture), MessageBoxButton.YesNo, MessageBoxImage.Question);
                    }
                    else
                    {
                        Console.WriteLine(LogR.GetString("CON_Plugin_Prompt_initial", CultureInfo.CurrentUICulture));
                        selfdestructResult = MessageBox.Show(LogR.GetString("UI_Plugin_Prompt_initial", CultureInfo.CurrentUICulture), LogR.GetString("UI_SetupTitle", CultureInfo.CurrentUICulture), MessageBoxButton.YesNo, MessageBoxImage.Question);
                    }

                    if (selfdestructResult == MessageBoxResult.No && !pluginsExist)
                    {
                        Console.WriteLine(LogR.GetString("CON_Plugin_Prompt_denied", CultureInfo.CurrentUICulture));
                        MessageBox.Show(LogR.GetString("UI_Plugin_Prompt_denied", CultureInfo.CurrentUICulture), LogR.GetString("UI_SetupTitle", CultureInfo.CurrentUICulture), MessageBoxButton.OK, MessageBoxImage.Information);
                        Environment.Exit(-1);
                        return;
                    }
                    else if (selfdestructResult == MessageBoxResult.Yes)
                    {
                        Console.WriteLine(LogR.GetString("CON_Plugin_Prompt_accepted", CultureInfo.CurrentUICulture));
                        bool success = UpdatePlugin(attemptDirectory);
                        if (!pluginsExist && !success)
                            Environment.Exit(-1);
                    }
                }
            }

            Properties.Settings.Default.FirstRun = false;

            if (!logDirectory.Exists)
                return;
            if (logDirectory.GetFiles().Count() == 0)
                return;

            notEmpty = true;

            FileInfo log = logDirectory.GetFiles().Where(f => Regex.IsMatch(f.Name, @"\d+\.csv")).OrderByDescending(f => f.Name).First();
            Console.WriteLine(String.Format(CultureInfo.CurrentUICulture, LogR.GetString("CON_ReadingLog", CultureInfo.CurrentUICulture), log.FullName));
            filename = log.Name;
            FileStream fileStream = File.Open(log.DirectoryName + "\\" + log.Name, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            fileStream.Seek(0, SeekOrigin.Begin);
            logReader = new StreamReader(fileStream);

            string existingLines = logReader.ReadToEnd(); // gotta get the dummy line for current player name
            string[] result = existingLines.Split('\n');
            foreach (string s in result)
            {
                if (s == "")
                    continue;
                string[] parts = s.Split(',');
                if (parts[0] == "0" && parts[3] == "YOU")
                {
                    Hacks.currentPlayerID = parts[2];
                    Console.WriteLine(String.Format(CultureInfo.CurrentUICulture, LogR.GetString("CON_FoundYou", CultureInfo.CurrentUICulture), parts[2]));
                }
            }
        }

        public bool UpdatePlugin(string attemptDirectory)
        {
            try
            {
                File.Copy(Directory.GetCurrentDirectory() + "\\resources\\pso2h.dll", attemptDirectory + "\\pso2h.dll", true);
                File.Copy(Directory.GetCurrentDirectory() + "\\resources\\ddraw.dll", attemptDirectory + "\\ddraw.dll", true);
                Directory.CreateDirectory(attemptDirectory + "\\plugins");
                File.Copy(Directory.GetCurrentDirectory() + "\\resources\\PSO2DamageDump.dll", attemptDirectory + "\\plugins" + "\\PSO2DamageDump.dll", true);
                Properties.Settings.Default.InstalledPluginVersion = pluginVersion;
                MessageBox.Show(LogR.GetString("UI_UpdatePlugin_Good", CultureInfo.CurrentUICulture), LogR.GetString("UI_SetupTitle", CultureInfo.CurrentUICulture), MessageBoxButton.OK, MessageBoxImage.Information);
                Console.WriteLine(LogR.GetString("CON_UpdatePlugin_Good", CultureInfo.CurrentUICulture));
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(LogR.GetString("UI_UpdatePlugin_Bad", CultureInfo.CurrentUICulture), LogR.GetString("UI_SetupTitle", CultureInfo.CurrentUICulture), MessageBoxButton.OK, MessageBoxImage.Error);
                Console.WriteLine(String.Format(CultureInfo.CurrentUICulture, LogR.GetString("CON_UpdatePlugin_Bad", CultureInfo.CurrentUICulture), ex.ToString()));
                return false;
            }
        }

        public void WriteClipboard()
        {
            string log = "";
            var ForStr = LogR.GetString("UI_Clip", CultureInfo.CurrentUICulture);
            foreach (Combatant c in combatants)
            {
                if (c.isAlly)
                {
                    string shortname = c.Name;
                    if (c.Name.Length > 4)
                    {
                        shortname = c.Name.Substring(0, 4);
                    }
                    log += String.Format(CultureInfo.CurrentUICulture, ForStr, shortname, FormatNumber(c.Damage));
                }
            }

            if (log == "") { return; }
            log = log.Substring(0, log.Length - 2);

            try
            {
                Clipboard.SetText(log);
            }
            catch
            {
                //LMAO
            }
        }

        public string WriteLog()
        {
            Console.WriteLine(LogR.GetString("CON_Log_Start", CultureInfo.CurrentUICulture));
            var ForStr = LogR.GetString("CON_Log_Ally", CultureInfo.CurrentUICulture); 

            foreach (Combatant c in combatants) // Debug for ID mapping
            {
                if (c.isAlly)
                {
                    foreach (Attack a in c.Attacks)
                    {
                        if (!MainWindow.skillDict.ContainsKey(a.ID))
                        {
                            TimeSpan t = TimeSpan.FromSeconds(a.Timestamp);
                            var ts = t.ToString(LogR.GetString("CON_Log_Ally_Time", CultureInfo.CurrentUICulture), CultureInfo.InvariantCulture);
                            Console.WriteLine(String.Format(CultureInfo.CurrentUICulture, ForStr, ts, a.ID, a.Damage, c.Name));
                        }
                    }
                }
            }

            if (combatants.Count != 0)
            {
                int elapsed = newTimestamp - startTimestamp;
                TimeSpan timespan = TimeSpan.FromSeconds(elapsed);
                var TimerF = LogR.GetString("LOG_TimeStamp", CultureInfo.CurrentUICulture);
                var headerF = LogR.GetString("LOG_Combatant_header", CultureInfo.CurrentUICulture);
                var Statl1 = LogR.GetString("LOG_Combatant_line1", CultureInfo.CurrentUICulture);
                var Statl2 = LogR.GetString("LOG_Combatant_line2", CultureInfo.CurrentUICulture);
                string timer = timespan.ToString(TimerF, CultureInfo.CurrentCulture);
                string log = DateTime.Now.ToString("F", CultureInfo.CurrentCulture) + " | " + timer + Environment.NewLine;

                log += Environment.NewLine;

                foreach (Combatant c in combatants)
                {
                    if (c.isAlly || c.isZanverse)
                        log += String.Format(CultureInfo.CurrentUICulture, LogR.GetString("LOG_Combatant_Stat", CultureInfo.CurrentUICulture), c.Name, c.ReadDamage.ToString("N0", CultureInfo.CurrentCulture), c.PercentReadDPSReadout, c.DPS, c.MaxHit) + Environment.NewLine;
                }

                log += Environment.NewLine + Environment.NewLine;

                foreach (Combatant c in combatants)
                {
                    if (c.isAlly || c.isZanverse)
                    {
                        string header = String.Format(CultureInfo.CurrentUICulture, headerF, c.Name, c.ReadDamage.ToString("N0", CultureInfo.CurrentCulture), c.PercentReadDPSReadout);
                        log += header + Environment.NewLine + Environment.NewLine;

                        List<string> attackNames = new List<string>();
                        List<Tuple<string, List<int>>> attackData = new List<Tuple<string, List<int>>>();

                        if (c.isZanverse && Properties.Settings.Default.SeparateZanverse)
                        {
                            foreach (Combatant c2 in backupCombatants)
                            {
                                if (c2.ZanverseDamage > 0)
                                    attackNames.Add(c2.ID);
                            }

                            foreach (string s in attackNames)
                            {
                                Combatant targetCombatant = backupCombatants.First(x => x.ID == s);
                                List<int> matchingAttacks = targetCombatant.Attacks.Where(a => a.ID == "2106601422").Select(a => a.Damage).ToList();
                                attackData.Add(new Tuple<string, List<int>>(targetCombatant.Name, matchingAttacks));
                            }

                        }
                        else
                        {
                            foreach (Attack a in c.Attacks)
                            {
                                if (a.ID == "2106601422" && Properties.Settings.Default.SeparateZanverse)
                                    continue;
                                if (MainWindow.skillDict.ContainsKey(a.ID))
                                    a.ID = MainWindow.skillDict[a.ID]; // these are getting disposed anyway, no 1 cur
                                if (!attackNames.Contains(a.ID))
                                    attackNames.Add(a.ID);
                            }

                            foreach (string s in attackNames)
                            {
                                List<int> matchingAttacks = c.Attacks.Where(a => a.ID == s).Select(a => a.Damage).ToList();
                                attackData.Add(new Tuple<string, List<int>>(s, matchingAttacks));
                            }
                        }

                        attackData = attackData.OrderByDescending(x => x.Item2.Sum()).ToList();

                        foreach (var i in attackData)
                        {
                            double percent = i.Item2.Sum() * 100d / c.ReadDamage;
                            string spacer = (percent >= 9) ? "" : " ";

                            string paddedPercent = percent.ToString("00.00", CultureInfo.CurrentCulture).Substring(0, 5);
                            string hits = i.Item2.Count().ToString("N0", CultureInfo.CurrentCulture);
                            string sum = i.Item2.Sum().ToString("N0", CultureInfo.CurrentCulture); 
                            string min = i.Item2.Min().ToString("N0", CultureInfo.CurrentCulture);
                            string max = i.Item2.Max().ToString("N0", CultureInfo.CurrentCulture); 
                            string avg = i.Item2.Average().ToString("N0", CultureInfo.CurrentCulture);

                            log += String.Format(CultureInfo.CurrentUICulture, Statl1, paddedPercent, i.Item1, sum);
                            log += Environment.NewLine;
                            log += String.Format(CultureInfo.CurrentUICulture, Statl2, hits, min, avg, max);
                            log += Environment.NewLine;
                        }

                        log += Environment.NewLine;
                    }
                }

                log += "Instance IDs: " + String.Join(", ", instances.ToArray());

                DateTime thisDate = DateTime.Now;
                string directory = string.Format(CultureInfo.InvariantCulture, "{0:yyyy-MM-dd}", DateTime.Now);
                Directory.CreateDirectory(String.Format(CultureInfo.InvariantCulture, "Logs/{0}", directory));
                string datetime = string.Format(CultureInfo.InvariantCulture, "{0:yyyy-MM-dd_HH-mm-ss}", DateTime.Now);
                string filename = String.Format(CultureInfo.InvariantCulture, "Logs/{0}/OverParse - {1}.txt", directory, datetime);
                File.WriteAllText(filename, log);

                return filename;
            }

            return null;
        }

        public string logStatus()
        {
            if (!valid)
            {
                return "USER SHOULD PROBABLY NEVER SEE THIS";
            }

            if (!notEmpty)
            {
                return "No logs: Enable plugin and check pso2_bin!";
            }

            if (!running)
            {
                return $"Waiting for combat data...";
            }

            return encounterData;
        }

        public void GenerateFakeEntries()
        {
            float totalDPS = 0;
            var StrFMTid = "1000000{0}";
            var StrFMTPname = "TestPlayer_{0}";
            var StrFMTEname = "TestEnemy_{0}";
            for (int i = 0; i <= 12; i++)
            {
                var Strid = String.Format(CultureInfo.InvariantCulture, StrFMTid, i.ToString(CultureInfo.InvariantCulture));
                var Strname = String.Format(CultureInfo.InvariantCulture, StrFMTPname, random.Next(0, 99).ToString(CultureInfo.InvariantCulture));
                Combatant temp = new Combatant(Strid, Strname);
                combatants.Add(temp);
            }

            foreach (Combatant c in combatants)
                c.PercentDPS = c.DPS / totalDPS * 100;

            for (int i = 0; i <= 9; i++)
            {
                var Strname = String.Format(CultureInfo.InvariantCulture, StrFMTEname, i.ToString(CultureInfo.InvariantCulture));
                Combatant temp = new Combatant(i.ToString(CultureInfo.InvariantCulture), Strname); 
                temp.PercentDPS = -1;
                combatants.Add(temp);
            }

            combatants.Sort((x, y) => y.DPS.CompareTo(x.DPS));

            valid = true;
            running = true;
        }

        public void UpdateLog(object sender, EventArgs e)
        {
            if (!valid || !notEmpty)
            {
                return;
            }

            string newLines = logReader.ReadToEnd();
            if (newLines != "")
            {
                string[] result = newLines.Split('\n');
                foreach (string str in result)
                {
                    if (str != "")
                    {
                        string[] parts = str.Split(',');
                        string lineTimestamp = parts[0];
                        string instanceID = parts[1];
                        string sourceID = parts[2];
                        string targetID = parts[4];
                        string targetName = parts[5];
                        string sourceName = parts[3];
                        int hitDamage = int.Parse(parts[7], CultureInfo.InvariantCulture);
                        string attackID = parts[6];
                        string isMultiHit = parts[10];
                        string isMisc = parts[11];
                        string isMisc2 = parts[12];
                        int index = -1;

                        if (lineTimestamp == "0" && sourceName == "YOU")
                        {
                            Hacks.currentPlayerID = parts[2];
                            Console.WriteLine(String.Format(CultureInfo.InvariantCulture, LogR.GetString("CON_Update_newplayer", CultureInfo.CurrentUICulture), parts[2]));
                            continue;
                        }

                        if (!instances.Contains(instanceID))
                            instances.Add(instanceID);

                        if (hitDamage < 1)
                            continue;
                        if (sourceID == "0" || attackID == "0")
                            continue;

                        foreach (Combatant x in combatants)
                        {
                            if (x.ID == sourceID && x.isTemporary == "no")
                            {
                                index = combatants.IndexOf(x);
                            }
                        }

                        if (index == -1)
                        {
                            combatants.Add(new Combatant(sourceID, sourceName));
                            index = combatants.Count - 1;
                        }

                        Combatant source = combatants[index];

                        newTimestamp = int.Parse(lineTimestamp, CultureInfo.InvariantCulture);
                        if (startTimestamp == 0)
                        {
                            var Strlog = String.Format(CultureInfo.CurrentUICulture, LogR.GetString("CON_Update_first", CultureInfo.CurrentUICulture), hitDamage, sourceID, sourceName, attackID, targetID, targetName);
                            Console.WriteLine(Strlog);
                            startTimestamp = newTimestamp;
                        }

                        source.Attacks.Add(new Attack(attackID, hitDamage, newTimestamp - startTimestamp));
                        running = true;
                    }
                }

                combatants.Sort((x, y) => y.ReadDamage.CompareTo(x.ReadDamage));

                if (startTimestamp != 0)
                {
                    encounterData = "00:00 - ∞ DPS";
                }

                if (startTimestamp != 0 && newTimestamp != startTimestamp)
                {
                    foreach (Combatant x in combatants)
                    {
                        if (x.isAlly || x.isZanverse)
                            x.ActiveTime = (newTimestamp - startTimestamp);
                    }
                }

            }
        }

        private String FormatNumber(int value)
        {
            if (value >= 100000000)
                return (value / 1000000).ToString("#,0", CultureInfo.InvariantCulture) + "M";
            if (value >= 1000000)
                return (value / 1000000D).ToString("0.#", CultureInfo.InvariantCulture) + "M";
            if (value >= 100000)
                return (value / 1000).ToString("#,0", CultureInfo.InvariantCulture) + "K";
            if (value >= 1000)
                return (value / 1000D).ToString("0.#", CultureInfo.InvariantCulture) + "K";
            return value.ToString("#,0", CultureInfo.InvariantCulture);
        }
    }
}
