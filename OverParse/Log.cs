using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace OverParse
{
    public class Log
    {
        public bool valid;
        public bool running;
        int startTimestamp = 0;
        public int newTimestamp = 0;
        public string filename;
        string encounterData;
        StreamReader logreader;
        public List<Combatant> combatants = new List<Combatant>();
        Random random = new Random();
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

        public Log(string attemptDirectory)
        {
            valid = false;
            DirectoryInfo directory = new DirectoryInfo($"{attemptDirectory}\\damagelogs");
            if (!directory.Exists)
            {
                Complain();
                return;
            }

            if (directory.GetFiles().Count() == 0)
            {
                Complain();
                return;
            }

            valid = true;
            running = false;
            FileInfo log = directory.GetFiles().OrderByDescending(f => f.LastWriteTime).First();
            Console.WriteLine($"Reading from {log.DirectoryName}\\{log.Name}");
            filename = log.Name;
            FileStream fileStream = File.Open(log.DirectoryName + "\\" + log.Name, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            fileStream.Seek(0, SeekOrigin.End);
            logreader = new StreamReader(fileStream);
        }

        public void Complain()
        {
            MessageBox.Show("No damage logs were found.\n\nPlease use \"System > Locate damagelogs folder...\" to select your installation directory, and make sure that the Damage Parser plugin is enabled in the Tweaker.", "Error");
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
            catch (Exception ex)
            {
            }
        }

        public void WriteLog()
        {
            if (combatants.Count != 0)
            {
                int elapsed = newTimestamp - startTimestamp;
                TimeSpan timespan = TimeSpan.FromSeconds(elapsed);
                string timer = timespan.ToString(@"mm\:ss");
                string log = string.Format("{0:yyyy-MM-dd_hh-mm-ss-tt}", DateTime.Now) + " | " + timer + Environment.NewLine + Environment.NewLine;
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

                foreach (Combatant c in combatants)
                {
                    log += $"{c.Name} | {c.Damage} dmg | {c.DPSReadout} contrib | {c.DPS} DPS | Max: {c.MaxHit}" + Environment.NewLine;
                }

                File.WriteAllText("logs/OverParse Log - " + string.Format("{0:yyyy-MM-dd_hh-mm-ss-tt}", DateTime.Now) + ".txt", log);
                string result = "";
                foreach (Combatant c in combatants)
                {
                    if (c.Name == Properties.Settings.Default.Username)
                    {
                        Console.WriteLine("found combatant matching name");
                        foreach (Attack a in c.Attacks)
                        {
                            if (!MainWindow.skillDict.ContainsKey(a.ID))
                            {
                                Console.WriteLine("found unmapped attack");
                                result += $"{a.Timestamp / 60}:{a.Timestamp % 60} -- {a.ID} dealing {a.Damage} dmg" + Environment.NewLine;
                            }
                        }

                        File.WriteAllText($"logs/Attack Mapping Debug - " + string.Format("{0:yyyy-MM-dd_hh-mm-ss-tt}", DateTime.Now) + ".txt", result);
                    }
                }
            }
        }

        public string logStatus()
        {
            if (!valid)
            {
                return "No logs, check \"System > Locate install folder\"!";
            }

            if (!running)
            {
                return $"Watching for combat data...";
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
            if (!valid)
            {
                return;
            }

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
                        string isMultiHit = parts[10];
                        string isMisc = parts[11];
                        string isMisc2 = parts[12];
                        int index = -1;
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
                        if (hitDamage > 0)
                        {
                            source.Damage += hitDamage;
                            newTimestamp = Int32.Parse(lineTimestamp);
                            if (startTimestamp == 0)
                            {
                                startTimestamp = newTimestamp;
                            }

                            source.Attacks.Add(new Attack(attackID, hitDamage, newTimestamp - startTimestamp));
                            running = true;
                        }
                        else
                        {
                            if (startTimestamp != 0)
                            {
                                source.Healing -= hitDamage;
                            }
                        }

                        if (source.MaxHitNum < hitDamage)
                        {
                            source.MaxHitNum = hitDamage;
                            source.MaxHitID = attackID;
                        }

                        string attack = attackID.ToString();
                        if (MainWindow.skillDict.ContainsKey(attackID))
                        {
                            attack = MainWindow.skillDict[attackID];
                        }

                        Console.WriteLine($" attack: {attack} {hitDamage} | misc: {isMultiHit} {isMisc} {isMisc2} | source: {source.Name} {source.ID}");
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
    }
}