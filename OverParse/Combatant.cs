using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using System.Windows.Shapes;

namespace OverParse
{
    public class Combatant
    {
        private const float maxBGopacity = 0.6f;
        public int Damage;
        public int Healing;
        public string ID;
        public string Name { get; set; }
        public int MaxHitNum;
        public string MaxHitID;
        public float DPS;
        public float PercentDPS;
        public List<Attack> Attacks;

        public Brush Brush
        {
            get
            {
                if (Hacks.ShowDamageGraph)
                {
                    return generateBarBrush(Color.FromArgb(200, 55, 95, 141), new Color());
                }
                else
                {
                    if (Name == "YOU")
                        return new SolidColorBrush(Color.FromArgb(160, 32, 80, 32));
                    return new SolidColorBrush(new Color());
                }

            }
        }

        public Brush Brush2
        {
            get
            {
                if (Hacks.ShowDamageGraph)
                {
                    return generateBarBrush(Color.FromArgb(140, 55, 95, 141), Color.FromArgb(64, 16, 16, 16));
                }
                else
                {
                    if (Name == "YOU")
                        return new SolidColorBrush(Color.FromArgb(160, 32, 80, 32));
                    return new SolidColorBrush(Color.FromArgb(64, 16, 16, 16));
                }
            }
        }

        LinearGradientBrush generateBarBrush(Color c, Color c2)
        {
            if (!Hacks.ShowDamageGraph)
                c = new Color();

            if (Name == "YOU")
                c = Color.FromArgb(160, 32, 80, 32);

            LinearGradientBrush lgb = new LinearGradientBrush();
            lgb.StartPoint = new System.Windows.Point(0, 0);
            lgb.EndPoint = new System.Windows.Point(1, 0);
            lgb.GradientStops.Add(new GradientStop(c, 0));
            lgb.GradientStops.Add(new GradientStop(c, PercentDPS / maxShare));
            lgb.GradientStops.Add(new GradientStop(c2, PercentDPS / maxShare));
            lgb.GradientStops.Add(new GradientStop(c2, 1));
            lgb.SpreadMethod = GradientSpreadMethod.Repeat;
            return lgb;
        }

        public static float maxShare = 0;

        public bool isAlly
        {
            get
            {
                string[] SuAttacks = { "487482498", "2785585589", "639929291" };
                if (int.Parse(ID) >= 10000000)
                {
                    return true;
                }

                bool allied = false;
                foreach (Attack a in Attacks)
                {
                    if (SuAttacks.Contains(a.ID))
                    {
                        allied = true;
                        return allied;
                    }
                }

                return allied;
            }
        }

        public string MaxHit
        {
            get
            {
                string attack = "Unknown";
                if (MainWindow.skillDict.ContainsKey(MaxHitID))
                {
                    attack = MainWindow.skillDict[MaxHitID];
                }

                return MaxHitNum.ToString("N0") + $" ({attack})";
            }
        }

        public string DPSReadout
        {
            get
            {
                if (Hacks.ShowRawDPS)
                {
                    return FormatNumber(DPS);
                }
                else
                {
                    if (PercentDPS < -.5)
                    {
                        return "--";
                    }
                    else
                    {
                        return string.Format("{0:0.0}", PercentDPS) + "%";
                    }
                }

            }
        }

        string FormatNumber(float input)
        {
            int num = (int)Math.Round(input);

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

        public string DamageReadout
        {
            get
            {
                return Damage.ToString("N0");
            }
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
            Attacks = new List<Attack>();
        }
    }
}