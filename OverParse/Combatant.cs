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
        public int ZanverseDamage;
        public bool isAux;
        public float PercentDPS;
        public float PercentReadDPS;
        public int ActiveTime;
        public List<Attack> Attacks;
        Color green = Color.FromArgb(160, 32, 130, 32);

        public float DPS
        {
            get
            {
                return Damage / (float)ActiveTime;
            }
        }

        public float ReadDPS
        {
            get
            {
                Console.WriteLine($"{this.Name} | read damage {ReadDamage} | ActiveTime {ActiveTime}");
                return ReadDamage / (float)ActiveTime;
            }
        }

        public bool isZanverse
        {
            get
            {
                if (ID == "99999999")
                    return true;
                return false;
            }
        }

        public bool isYou
        {
            get
            {
                return (ID == Hacks.currentPlayerID);
            }
        }

        public int ReadDamage
        {
            get
            {
                if (this.isZanverse)
                    return Damage;
                if (Properties.Settings.Default.SeparateZanverse)
                    return Damage - ZanverseDamage;
                return Damage;
            }
        }

        public string AnonymousName() {
            if (isYou)
                return Name;
            else
                return "--";
        }

        public string DisplayName
        {
            get
            {
                if (Properties.Settings.Default.AnonymizeNames && isAlly)
                    return AnonymousName();
                return Name;
            }

        }

        public Brush Brush
        {
            get
            {
                if (Properties.Settings.Default.ShowDamageGraph && (isAlly))
                {
                    return generateBarBrush(Color.FromArgb(200, 65, 112, 166), new Color());
                }
                else
                {
                    if (isYou && Properties.Settings.Default.HighlightYourDamage)
                        return new SolidColorBrush(green);
                    return new SolidColorBrush(new Color());
                }

            }
        }

        public Brush Brush2
        {
            get
            {
                if (Properties.Settings.Default.ShowDamageGraph && (isAlly && !isZanverse))
                {
                    return generateBarBrush(Color.FromArgb(140, 65, 112, 166), Color.FromArgb(64, 16, 16, 16));
                }
                else
                {
                    if (isYou && Properties.Settings.Default.HighlightYourDamage)
                        return new SolidColorBrush(green);
                    return new SolidColorBrush(Color.FromArgb(64, 16, 16, 16));
                }
            }
        }

        LinearGradientBrush generateBarBrush(Color c, Color c2)
        {
            if (!Properties.Settings.Default.ShowDamageGraph)
                c = new Color();

            if (isYou && Properties.Settings.Default.HighlightYourDamage)
                c = green;

            LinearGradientBrush lgb = new LinearGradientBrush();
            lgb.StartPoint = new System.Windows.Point(0, 0);
            lgb.EndPoint = new System.Windows.Point(1, 0);
            lgb.GradientStops.Add(new GradientStop(c, 0));
            lgb.GradientStops.Add(new GradientStop(c, ReadDamage / maxShare));
            lgb.GradientStops.Add(new GradientStop(c2, ReadDamage / maxShare));
            lgb.GradientStops.Add(new GradientStop(c2, 1));
            lgb.SpreadMethod = GradientSpreadMethod.Repeat;
            return lgb;
        }

        public static float maxShare = 0;

        public bool isAlly
        {
            get
            {
                if (int.Parse(ID) >= 10000000 && !isZanverse)
                    return true;
                return false;
            }
        }

        public string MaxHit
        {
            get
            {
                if (isZanverse)
                    return "--";

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
                if (Properties.Settings.Default.ShowRawDPS)
                {
                    return FormatNumber(ReadDPS);
                }
                else
                {
                    if (PercentDPS < -.5)
                    {
                        return "--";
                    }
                    else
                    {
                        return string.Format("{0:0.0}", PercentReadDPS) + "%";
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
                return ReadDamage.ToString("N0");
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
            PercentDPS = -1;
            isAux = false;
            Attacks = new List<Attack>();
        }
    }
}