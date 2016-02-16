using Ookii.Dialogs.Wpf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace OverParse
{
    public class Log
    {
        public bool notEmpty;
        public bool valid;
        public bool running;
        int startTimestamp = 0;
        public int newTimestamp = 0;
        public string filename;
        string encounterData;
        StreamReader logReader;
        public List<Combatant> combatants = new List<Combatant>();
        Random random = new Random();

        public Log(string attemptDirectory)
        {
            valid = false;
            notEmpty = false;
            running = false;

            while (!File.Exists($"{attemptDirectory}\\pso2.exe"))
            {
                Console.WriteLine("Invalid pso2_bin directory, prompting for new one...");
                MessageBox.Show("Please select your pso2_bin directory.\nThis is the same folder you selected while setting up the Tweaker.", "OverParse Setup", MessageBoxButton.OK, MessageBoxImage.Information);

                VistaFolderBrowserDialog oDialog = new VistaFolderBrowserDialog();
                oDialog.Description = "Select your pso2_bin folder...";
                oDialog.UseDescriptionForTitle = true;

                if ((bool)oDialog.ShowDialog(Application.Current.MainWindow))
                {
                    attemptDirectory = oDialog.SelectedPath;
                    Console.WriteLine($"Testing {attemptDirectory} as pso2_bin directory...");
                    Properties.Settings.Default.Path = attemptDirectory;
                }
                else
                {
                    Console.WriteLine("Canceled out of directory picker");
                    MessageBox.Show("OverParse needs a valid PSO2 installation to function.\nThe application will now close.", "OverParse Setup", MessageBoxButton.OK, MessageBoxImage.Information);
                    Application.Current.Shutdown(); // ABORT ABORT ABORT
                    break;
                }
            }

            if (!File.Exists($"{attemptDirectory}\\pso2.exe")) { return; }

            valid = true;

            Console.WriteLine("Making sure pso2_bin\\damagelogs exists");
            DirectoryInfo directory =  new DirectoryInfo($"{attemptDirectory}\\damagelogs");

            if (Properties.Settings.Default.FirstRun)
            {
                Console.WriteLine("First run");
                bool unsetFirstRun = true;
                MessageBoxResult tweakerResult = MessageBox.Show("Do you use the PSO2 Tweaker?", "OverParse Setup", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (tweakerResult == MessageBoxResult.Yes)
                {
                    bool warn = true;
                    if (directory.Exists)
                    {
                        if (directory.GetFiles().Count() > 0)
                        {
                            warn = false;
                        }
                    }

                    if (warn)
                    {
                        Console.WriteLine("No damagelog warning");
                        MessageBox.Show("Your PSO2 folder doesn't contain any damagelogs. This is not an error, just a reminder!\n\nPlease turn on the Damage Parser plugin in PSO2 Tweaker (orb menu > Plugins). OverParse needs this to function. You may also want to update the plugins while you're there.", "Notice", MessageBoxButton.OK, MessageBoxImage.Information);
                        Properties.Settings.Default.FirstRun = false;
                        Properties.Settings.Default.Save();
                        return;
                    }
                }
                else if (tweakerResult == MessageBoxResult.No)
                {
                    bool pluginsExist = File.Exists(attemptDirectory + "\\pso2h.dll") && File.Exists(attemptDirectory + "\\ddraw.dll") && File.Exists(attemptDirectory + "\\plugins" + "\\PSO2DamageDump.dll");

                    MessageBoxResult selfdestructResult;
                    if (pluginsExist)
                    {
                        Console.WriteLine("Prompting for plugin update");
                        selfdestructResult = MessageBox.Show("Would you like to update your plugins to the version included with OverParse?\n\nOverParse may behave unpredictably if you use a different version than it expects.", "OverParse Setup", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    } else
                    {
                        Console.WriteLine("Prompting for plugin install");
                        selfdestructResult = MessageBox.Show("OverParse needs a Tweaker plugin to recieve its damage information.\n\nThe plugin can be installed without the Tweaker, but it won't be automatically updated, and I can't provide support for this method.\n\nDo you want to try to manually install the Damage Parser plugin?", "OverParse Setup", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    }

                    if (selfdestructResult == MessageBoxResult.No && pluginsExist)
                        unsetFirstRun = false;
                    
                    if (selfdestructResult == MessageBoxResult.No && !pluginsExist)
                    {
                        Console.WriteLine("Denied plugin install");
                        MessageBox.Show("OverParse needs the Damage Parser plugin to function.\n\nThe application will now close.", "OverParse Setup", MessageBoxButton.OK, MessageBoxImage.Information);
                        Application.Current.Shutdown();
                        return;
                    }
                    else if (selfdestructResult == MessageBoxResult.Yes)
                    {
                        Console.WriteLine("Accepted plugin install");
                        try
                        {
                            File.Copy(Directory.GetCurrentDirectory() + "\\resources\\pso2h.dll", attemptDirectory + "\\pso2h.dll", true);
                            File.Copy(Directory.GetCurrentDirectory() + "\\resources\\ddraw.dll", attemptDirectory + "\\ddraw.dll", true);
                            Directory.CreateDirectory(attemptDirectory + "\\plugins");
                            File.Copy(Directory.GetCurrentDirectory() + "\\resources\\PSO2DamageDump.dll", attemptDirectory + "\\plugins" + "\\PSO2DamageDump.dll", true);
                            MessageBox.Show("Setup complete! A few files have been copied to your pso2_bin folder.\n\nIf PSO2 is running right now, you'll need to close it before the changes can take effect.", "OverParse Setup", MessageBoxButton.OK, MessageBoxImage.Information);
                            Console.WriteLine("Plugin install successful");
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Something went wrong with manual installation. (This usually means that some of the files were in use: try again with PSO2 closed.)\n\nIf that's not the problem, then when you complain to TyroneSama, please include the following text:\n\n" + ex.ToString(), "OverParse Setup", MessageBoxButton.OK, MessageBoxImage.Error);
                            Console.WriteLine($"PLUGIN INSTALL FAILED: {ex.ToString()}");
                            Application.Current.Shutdown();
                            return;
                        }


                        
                    }
                }

                if (unsetFirstRun)
                {
                    Properties.Settings.Default.FirstRun = false;
                    Properties.Settings.Default.Save();
                }

            }

            if (!directory.Exists)
                return;
            if (directory.GetFiles().Count() == 0)
                return;

            notEmpty = true;

            FileInfo log = directory.GetFiles().OrderByDescending(f => f.LastWriteTime).First();
            Console.WriteLine($"Reading from {log.DirectoryName}\\{log.Name}");
            filename = log.Name;
            FileStream fileStream = File.Open(log.DirectoryName + "\\" + log.Name, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            fileStream.Seek(0, SeekOrigin.End);
            logReader = new StreamReader(fileStream);
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

                    log += $"{shortname} {FormatNumber(c.Damage)} | ";
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

        public void WriteLog()
        {
            Console.WriteLine("Logging encounter information to file");
            if (combatants.Count != 0)
            {
                int elapsed = newTimestamp - startTimestamp;
                TimeSpan timespan = TimeSpan.FromSeconds(elapsed);
                string timer = timespan.ToString(@"mm\:ss");
                string log = string.Format("{0:yyyy-MM-dd_hh-mm-ss-tt}", DateTime.Now) + " | " + timer + Environment.NewLine;

                log += Environment.NewLine;

                foreach (Combatant c in combatants)
                {
                    if (c.isAlly)
                        log += $"{c.Name} | {c.Damage} dmg | {c.DPSReadout} contrib | {c.DPS} DPS | Max: {c.MaxHit}" + Environment.NewLine;
                }

                log += Environment.NewLine;

                foreach (Combatant c in combatants)
                {
                    if (c.isAlly)
                    {
                        log += $"### {c.Name} - {c.Damage} Dmg ({c.DPSReadout}) ### " + Environment.NewLine;
                        List<string> attackTypes = new List<string>();
                        List<int> damageTotals = new List<int>();
                        foreach (Attack a in c.Attacks)
                        {
                            string name = a.ID;
                            if (MainWindow.skillDict.ContainsKey(a.ID))
                            {
                                name = MainWindow.skillDict[a.ID];
                            }

                            if (attackTypes.Contains(name))
                            {
                                int index = attackTypes.IndexOf(name);
                                damageTotals[index] += a.Damage;
                            }
                            else
                            {
                                attackTypes.Add(name);
                                damageTotals.Add(a.Damage);
                            }
                        }

                        int total = damageTotals.Sum();
                        List<Tuple<string, int>> finalAttacks = new List<Tuple<string, int>>();
                        foreach (string str in attackTypes)
                        {
                            finalAttacks.Add(new Tuple<string, int>(str, damageTotals[attackTypes.IndexOf(str)]));
                        }

                        finalAttacks = finalAttacks.OrderBy(x => x.Item2).Reverse().ToList();
                        foreach (Tuple<string, int> t in finalAttacks)
                        {
                            log += $"{t.Item2 * 100 / total}% | {t.Item1} ({t.Item2} dmg)" + Environment.NewLine;
                        }

                        log += Environment.NewLine;
                    }
                }

                File.WriteAllText("Logs/OverParse Log - " + string.Format("{0:yyyy-MM-dd_hh-mm-ss-tt}", DateTime.Now) + ".txt", log);

                foreach (Combatant c in combatants)
                {
                    if (c.Name == "YOU")
                    {
                        foreach (Attack a in c.Attacks)
                        {
                            if (!MainWindow.skillDict.ContainsKey(a.ID))
                            {
                                TimeSpan t = TimeSpan.FromSeconds(a.Timestamp);
                                Console.WriteLine($"UNMAPPED ATTACK - {t.ToString(@"hh\h\:mm\m\:ss\s\:fff\m\s")} -- {a.ID} dealing {a.Damage} dmg" + Environment.NewLine);
                            }
                        }

                    }
                }
            }
        }

        public string logStatus()
        {
            if (!valid)
            {
                return "USER SHOULD PROBABLY NEVER SEE THIS";
            }

            if (!notEmpty)
            {
                return "No logs: check Damage Parser plugin!";
            }

            if (!running)
            {
                return $"Waiting for combat data...";
            }

            return encounterData;
        }

        public void GenerateFakeEntries()
        {
            for (int i = 0; i <= 9; i++)
            {
                Combatant temp = new Combatant("1000000" + i.ToString(), "TestPlayer_" + i.ToString());
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
                        string sourceID = parts[2];
                        string sourceName = parts[3];
                        int hitDamage = int.Parse(parts[7]);
                        string attackID = parts[6];
                        string isMultiHit = parts[10];
                        string isMisc = parts[11];
                        string isMisc2 = parts[12];
                        int index = -1;

                        if (hitDamage < 1)
                            continue;

                        foreach (Combatant x in combatants)
                        {
                            if (x.ID == sourceID)
                            {
                                index = combatants.IndexOf(x);
                            }
                        }

                        if (attackID == "2106601422" && Properties.Settings.Default.SeparateZanverse)
                        {
                            index = -1;
                            foreach (Combatant x in combatants)
                            {
                                if (x.ID == "94857493" && x.Name == "Zanverse")
                                {
                                    index = combatants.IndexOf(x);
                                }
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

                        source.Damage += hitDamage;
                        newTimestamp = int.Parse(lineTimestamp);
                        if (startTimestamp == 0 && sourceID != "0")
                        {
                            Console.WriteLine($"FIRST ATTACK RECORDED: {hitDamage} from {sourceID} with {attackID}");
                            startTimestamp = newTimestamp;
                        }

                        source.Attacks.Add(new Attack(attackID, hitDamage, newTimestamp - startTimestamp));
                        running = true;

                        if (source.MaxHitNum < hitDamage)
                        {
                            source.MaxHitNum = hitDamage;
                            source.MaxHitID = attackID;
                        }

                    }
                }

                combatants.Sort((x, y) => y.Damage.CompareTo(x.Damage));

                if (startTimestamp != 0)
                {
                    encounterData = "00:00 - ∞ DPS";
                }

                if (startTimestamp != 0 && newTimestamp != startTimestamp)
                {
                    int elapsed = newTimestamp - startTimestamp;
                    float partyDPS = 0;
                    int filtered = 0;
                    TimeSpan timespan = TimeSpan.FromSeconds(elapsed);
                    string timer = timespan.ToString(@"mm\:ss");
                    encounterData = $"{timer}";

                    if (Properties.Settings.Default.CompactMode) {
                        foreach (Combatant c in combatants)
                        {
                            if (c.Name == "YOU")
                                encounterData += $" - MAX: {c.MaxHit}";
                        }

                    }


                    foreach (Combatant x in combatants)
                    {
                        if (x.isAlly)
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
                        if (x.isAlly)
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

        string FormatNumber(int num)
        {
            if (num >= 100000000)
            {
                return (num / 1000000D).ToString("0.#M");
            }

            if (num >= 1000000)
            {
                return (num / 1000000D).ToString("0.##M");
            }

            if (num >= 100000)
            {
                return (num / 1000D).ToString("0.#K");
            }

            if (num >= 10000)
            {
                return (num / 1000D).ToString("0.##K");
            }

            return num.ToString("#,0");
        }
    }
}