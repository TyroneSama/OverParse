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
        public bool isAux;
        public float DPS;
        public float PercentDPS;
        public List<Attack> Attacks;
        Color green = Color.FromArgb(160, 32, 130, 32);

        bool isYou()
        {
            return (Name == "YOU" || Name == "YOU (Aux)");
        }

        public Brush Brush
        {
            get
            {
                if (Hacks.ShowDamageGraph && PercentDPS != -1)
                {
                    return generateBarBrush(Color.FromArgb(200, 65, 112, 166), new Color());
                }
                else
                {
                    if (isYou())
                        return new SolidColorBrush(green);
                    return new SolidColorBrush(new Color());
                }

            }
        }

        public Brush Brush2
        {
            get
            {
                if (Hacks.ShowDamageGraph && PercentDPS != -1)
                {
                    return generateBarBrush(Color.FromArgb(140, 65, 112, 166), Color.FromArgb(64, 16, 16, 16));
                }
                else
                {
                    if (isYou())
                        return new SolidColorBrush(green);
                    return new SolidColorBrush(Color.FromArgb(64, 16, 16, 16));
                }
            }
        }

        LinearGradientBrush generateBarBrush(Color c, Color c2)
        {
            if (!Hacks.ShowDamageGraph)
                c = new Color();

            if (isYou())
                c = green;

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
                if (isAux)
                    return true;

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

        private String FormatNumber(float value)
        {
            int num = (int)Math.Round(value);

            if (value >= 100000000)
                return (value / 1000000).ToString("#,0") + "M";
            if (value >= 1000000)
                return (value / 1000000D).ToString("0.0") + "M";
            if (value >= 100000)
                return (value / 1000).ToString("#,0") + "K";
            if (value >= 1000)
                return (value / 1000D).ToString("0.0") + "K";
            return value.ToString("#,0");
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
            isAux = false;
            Attacks = new List<Attack>();
        }
    }
}