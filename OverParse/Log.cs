using Ookii.Dialogs.Wpf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public Log(string attemptDirectory)
        {
            valid = false;
            notEmpty = false;
            running = false;

            while (!File.Exists($"{attemptDirectory}\\pso2.exe"))  ///TODO: GetText
            {
                Console.WriteLine("Invalid pso2_bin directory, prompting for new one...");  ///TODO: GetText
                MessageBox.Show("Please select your pso2_bin directory.\n\nThis folder will be inside your PSO2 install folder, which is usually at C:\\PHANTASYSTARONLINE2\\.\n\nIf you installed the game multiple times (e.g. through the torrent), please make sure you pick the right one, or OverParse won't be able to read your logs!", "OverParse Setup", MessageBoxButton.OK, MessageBoxImage.Information);  ///TODO: GetText

                VistaFolderBrowserDialog oDialog = new VistaFolderBrowserDialog();
                oDialog.Description = "Select your pso2_bin folder...";  ///TODO: GetText
                oDialog.UseDescriptionForTitle = true;

                if ((bool)oDialog.ShowDialog(Application.Current.MainWindow))
                {
                    attemptDirectory = oDialog.SelectedPath;
                    Console.WriteLine($"Testing {attemptDirectory} as pso2_bin directory...");  ///TODO: GetText
                    Properties.Settings.Default.Path = attemptDirectory;
                }
                else
                {
                    Console.WriteLine("Canceled out of directory picker");  ///TODO: GetText
                    MessageBox.Show("OverParse needs a valid PSO2 installation to function.\nThe application will now close.", "OverParse Setup", MessageBoxButton.OK, MessageBoxImage.Information);  ///TODO: GetText
                    Application.Current.Shutdown(); // ABORT ABORT ABORT
                    break;
                }
            }

            if (!File.Exists($"{attemptDirectory}\\pso2.exe")) { return; }  ///TODO: GetText

            valid = true;

            Console.WriteLine("Making sure pso2_bin\\damagelogs exists");  ///TODO: GetText
            logDirectory = new DirectoryInfo($"{attemptDirectory}\\damagelogs");  ///TODO: GetText

            Console.WriteLine("Checking for damagelog directory override");  ///TODO: GetText
            if (File.Exists($"{attemptDirectory}\\plugins\\PSO2DamageDump.cfg"))  ///TODO: GetText
            {
                Console.WriteLine("Found a config file for damage dump plugin, parsing");  ///TODO: GetText
                String[] lines = File.ReadAllLines($"{attemptDirectory}\\plugins\\PSO2DamageDump.cfg");  ///TODO: GetText
                foreach (String s in lines)
                {
                    String[] split = s.Split('=');
                    Console.WriteLine(split[0] + "|" + split[1]);  ///TODO: GetText
                    if (split.Length < 2)
                        continue;
                    if (split[0].Split('[')[0] == "directory")
                    {
                        logDirectory = new DirectoryInfo(split[1]);
                        Console.WriteLine($"Log directory override: {split[1]}");  ///TODO: GetText
                    }
                }
            }
            else
            {
                Console.WriteLine("No PSO2DamageDump.cfg");  ///TODO: GetText
            }

            if (Properties.Settings.Default.LaunchMethod == "Unknown")
            {
                Console.WriteLine("LaunchMethod prompt");  ///TODO: GetText
                MessageBoxResult tweakerResult = MessageBox.Show("Do you use the PSO2 Tweaker?", "OverParse Setup", MessageBoxButton.YesNo, MessageBoxImage.Question);  ///TODO: GetText
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
                    Console.WriteLine("No damagelog warning");  ///TODO: GetText
                    MessageBox.Show("Your PSO2 folder doesn't contain any damagelogs. This is not an error, just a reminder!\n\nPlease turn on the Damage Parser plugin in PSO2 Tweaker (orb menu > Plugins). OverParse needs this to function. You may also want to update the plugins while you're there.", "OverParse Setup", MessageBoxButton.OK, MessageBoxImage.Information);  ///TODO: GetText
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

                Console.WriteLine($"Installed: {Properties.Settings.Default.InstalledPluginVersion} / Current: {pluginVersion}");  ///TODO: GetText

                if (Properties.Settings.Default.InstalledPluginVersion < pluginVersion)
                {
                    MessageBoxResult selfdestructResult;

                    if (pluginsExist)
                    {
                        Console.WriteLine("Prompting for plugin update");  ///TODO: GetText
                        selfdestructResult = MessageBox.Show("This release of OverParse includes a new version of the parsing plugin. Would you like to update now?\n\nOverParse may behave unpredictably if you use a different version than it expects.", "OverParse Setup", MessageBoxButton.YesNo, MessageBoxImage.Question);  ///TODO: GetText
                    }
                    else
                    {
                        Console.WriteLine("Prompting for initial plugin install");  ///TODO: GetText
                        selfdestructResult = MessageBox.Show("OverParse needs a Tweaker plugin to recieve its damage information.\n\nThe plugin can be installed without the Tweaker, but it won't be automatically updated, and I can't provide support for this method.\n\nDo you want to try to manually install the Damage Parser plugin?", "OverParse Setup", MessageBoxButton.YesNo, MessageBoxImage.Question);  ///TODO: GetText
                    }

                    if (selfdestructResult == MessageBoxResult.No && !pluginsExist)
                    {
                        Console.WriteLine("Denied plugin install");  ///TODO: GetText
                        MessageBox.Show("OverParse needs the Damage Parser plugin to function.\n\nThe application will now close.", "OverParse Setup", MessageBoxButton.OK, MessageBoxImage.Information);  ///TODO: GetText
                        Environment.Exit(-1);
                        return;
                    }
                    else if (selfdestructResult == MessageBoxResult.Yes)
                    {
                        Console.WriteLine("Accepted plugin install");  ///TODO: GetText
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
            Console.WriteLine($"Reading from {log.DirectoryName}\\{log.Name}");  ///TODO: GetText
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
                    Console.WriteLine("Found existing active player ID: " + parts[2]);  ///TODO: GetText
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
                MessageBox.Show("Setup complete! A few files have been copied to your pso2_bin folder.\n\nIf PSO2 is running right now, you'll need to close it before the changes can take effect.", "OverParse Setup", MessageBoxButton.OK, MessageBoxImage.Information);  ///TODO: GetText
                Console.WriteLine("Plugin install successful");  ///TODO: GetText
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Something went wrong with manual installation. This usually means that the files are already in use: try again with PSO2 closed.\n\nIf you've recieved this message even after closing PSO2, you may need to run OverParse as administrator.", "OverParse Setup", MessageBoxButton.OK, MessageBoxImage.Error);  ///TODO: GetText
                Console.WriteLine($"PLUGIN INSTALL FAILED: {ex.ToString()}");  ///TODO: GetText

                return false;
            }
        }

        public void WriteClipboard()
        {
            string log = "";
            foreach (Combatant c in combatants)
            {
                if (c.isAlly)
                {
                    string shortname = c.Name;
                    if (c.Name.Length > 4)
                    {
                        shortname = c.Name.Substring(0, 4);
                    }

                    log += $"{shortname} {FormatNumber(c.Damage)} | ";  ///TODO: GetText
                }
            }

            if (log == "") { return; }
            log = log.Substring(0, log.Length - 2);

            try
            {
                Clipboard.SetText(log);  ///TODO: GetText
            }
            catch
            {
                //LMAO
            }
        }

        public string WriteLog()
        {
            Console.WriteLine("Logging encounter information to file");  ///TODO: GetText

            foreach (Combatant c in combatants) // Debug for ID mapping
            {
                if (c.isAlly)
                {
                    foreach (Attack a in c.Attacks)
                    {
                        if (!MainWindow.skillDict.ContainsKey(a.ID))
                        {
                            TimeSpan t = TimeSpan.FromSeconds(a.Timestamp);
                            Console.WriteLine($"{t.ToString(@"dd\.hh\:mm\:ss")} unmapped: {a.ID} ({a.Damage} dmg from {c.Name})");  ///TODO: GetText
                        }
                    }
                }
            }

            if (combatants.Count != 0)
            {
                int elapsed = newTimestamp - startTimestamp;
                TimeSpan timespan = TimeSpan.FromSeconds(elapsed);
                string timer = timespan.ToString(@"mm\:ss");  ///TODO: GetText
                string log = DateTime.Now.ToString("F") + " | " + timer + Environment.NewLine;

                log += Environment.NewLine;

                foreach (Combatant c in combatants)
                {
                    if (c.isAlly || c.isZanverse)
                        log += $"{c.Name} | {c.ReadDamage.ToString("N0")} dmg | {c.PercentReadDPSReadout} contrib | {c.DPS} DPS | Max: {c.MaxHit}" + Environment.NewLine;  ///TODO: GetText
                }

                log += Environment.NewLine + Environment.NewLine;

                foreach (Combatant c in combatants)
                {
                    if (c.isAlly || c.isZanverse)
                    {
                        string header = $"###### {c.Name} - {c.ReadDamage.ToString("N0")} dmg ({c.PercentReadDPSReadout}) ######";  ///TODO: GetText
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

                            string paddedPercent = percent.ToString("00.00").Substring(0, 5); ///TODO: GetText
                            string hits = i.Item2.Count().ToString("N0"); ///TODO: GetText
                            string sum = i.Item2.Sum().ToString("N0"); ///TODO: GetText
                            string min = i.Item2.Min().ToString("N0"); ///TODO: GetText
                            string max = i.Item2.Max().ToString("N0"); ///TODO: GetText
                            string avg = i.Item2.Average().ToString("N0"); ///TODO: GetText

                            log += $"{paddedPercent}% | {i.Item1} ({sum} dmg)" + Environment.NewLine; ///TODO: GetText
                            log += $"       |   {hits} hits - {min} min, {avg} avg, {max} max" + Environment.NewLine; ///TODO: GetText
                        }

                        log += Environment.NewLine;
                    }
                }

                log += "Instance IDs: " + String.Join(", ", instances.ToArray());

                DateTime thisDate = DateTime.Now;
                string directory = string.Format("{0:yyyy-MM-dd}", DateTime.Now); ///TODO: GetText
                Directory.CreateDirectory($"Logs/{directory}"); ///TODO: GetText
                string datetime = string.Format("{0:yyyy-MM-dd_HH-mm-ss}", DateTime.Now); ///TODO: GetText
                string filename = $"Logs/{directory}/OverParse - {datetime}.txt"; ///TODO: GetText
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
            for (int i = 0; i <= 12; i++)
            {
                Combatant temp = new Combatant("1000000" + i.ToString(), "TestPlayer_" + random.Next(0, 99).ToString()); ///TODO: GetText
                combatants.Add(temp);
            }

            foreach (Combatant c in combatants)
                c.PercentDPS = c.DPS / totalDPS * 100;

            for (int i = 0; i <= 9; i++)
            {
                Combatant temp = new Combatant(i.ToString(), "TestEnemy_" + i.ToString()); ///TODO: GetText
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
                        int hitDamage = int.Parse(parts[7]); ///TODO: GetText
                        string attackID = parts[6];
                        string isMultiHit = parts[10];
                        string isMisc = parts[11];
                        string isMisc2 = parts[12];
                        int index = -1;

                        if (lineTimestamp == "0" && sourceName == "YOU")
                        {
                            Hacks.currentPlayerID = parts[2];
                            Console.WriteLine("Found new active player ID: " + parts[2]); ///TODO: GetText
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

                        newTimestamp = int.Parse(lineTimestamp); ///TODO: GetText
                        if (startTimestamp == 0)
                        {
                            Console.WriteLine($"FIRST ATTACK RECORDED: {hitDamage} dmg from {sourceID} ({sourceName}) with {attackID}, to {targetID} ({targetName})"); ///TODO: GetText
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
                return (value / 1000000).ToString("#,0") + "M"; ///TODO: GetText
            if (value >= 1000000)
                return (value / 1000000D).ToString("0.#") + "M"; ///TODO: GetText
            if (value >= 100000)
                return (value / 1000).ToString("#,0") + "K"; ///TODO: GetText
            if (value >= 1000)
                return (value / 1000D).ToString("0.#") + "K"; ///TODO: GetText
            return value.ToString("#,0"); ///TODO: GetText
        }
    }
}
