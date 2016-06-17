using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Windows.Media;
using System.Windows.Shapes;

namespace OverParse
{
    public class Combatant
    {
        private const float maxBGopacity = 0.6f;
        public string ID;
        public string Name { get; set; }
        public float PercentDPS;
        public float PercentReadDPS;
        public int ActiveTime;
        public string isTemporary;
        public List<Attack> Attacks;
        Color green;
        public static string[] AISAttackIDs = new string[] { "119505187", "79965782", "79965783", "79965784", "80047171", "434705298", "79964675", "1460054769", "4081218683", "3298256598", "2826401717" };
        //public static string[] AISAttackIDs = new string[] { "1756866220", "2398664728" };
	ResourceManager CombatantR;

        public int Damage
        {
            get
            {
                return Attacks.Sum(x => x.Damage);
            }
        }

        public int ZanverseDamage
        {
            get
            {
                return Attacks.Where(a => a.ID == "2106601422").Sum(x => x.Damage);
            }
        }

        public int AISDamage
        {
            get
            {
                return Attacks.Where(a => AISAttackIDs.Contains(a.ID)).Sum(x => x.Damage);
            }
        }

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
                Console.WriteLine(String.Format(CultureInfo.CurrentUICulture, CombatantR.GetString("CON_ReadDPS", CultureInfo.CurrentUICulture), Name, ReadDamage, ActiveTime));
                return ReadDamage / (float)ActiveTime;
            }
        }

        public bool isAIS
        {
            get
            {
                return (isTemporary == "AIS");
            }
        }

        public bool isZanverse
        {
            get
            {
                return (isTemporary == "Zanverse");
            }
        }

        public bool isYou
        {
            get
            {
                return (ID == Hacks.currentPlayerID);
            }
        }

        public int MaxHitNum
        {
            get
            {
                return MaxHitAttack.Damage;
            }
        }

        public string MaxHitID
        {
            get
            {
                return MaxHitAttack.ID;
            }
        }

        public int ReadDamage
        {
            get
            {
                if (isZanverse || isAIS)
                    return Damage;

                int temp = Damage;
                if (Properties.Settings.Default.SeparateZanverse)
                    temp -= ZanverseDamage;
                if (Properties.Settings.Default.SeparateAIS)
                    temp -= AISDamage;
                return temp;
            }
        }

        public string AnonymousName()
        {
            if (isYou)
                return Name;
            else
                return CombatantR.GetString("UI_Blank", CultureInfo.CurrentUICulture);
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
                if (int.Parse(ID, CultureInfo.InvariantCulture) >= 10000000 && !isZanverse)
                    return true;
                return false;
            }
        }

        public Attack MaxHitAttack
        {
            get
            {
                Attacks.Sort((x, y) => y.Damage.CompareTo(x.Damage));
                return Attacks.FirstOrDefault();
            }
        }

        public string MaxHit
        {
            get
            {
                if (MaxHitAttack == null)
                    return CombatantR.GetString("UI_Blank", CultureInfo.CurrentUICulture);

                string UIattack = CombatantR.GetString("UI_Unknown", CultureInfo.CurrentUICulture);
                string CONattack = CombatantR.GetString("CON_Unknown", CultureInfo.CurrentUICulture);
                if (MainWindow.skillDict.ContainsKey(MaxHitID))
                {
                    UIattack = MainWindow.skillDict[MaxHitID];
                    CONattack = MainWindow.skillDict[MaxHitID];
                }

                Console.WriteLine(String.Format(CultureInfo.InvariantCulture, CombatantR.GetString("CON_MaxHit", CultureInfo.CurrentUICulture), Name, MaxHitID, MaxHitAttack.Damage, CONattack));
                return String.Format(CultureInfo.CurrentUICulture, CombatantR.GetString("UI_MaxHit", CultureInfo.CurrentUICulture), MaxHitAttack.Damage.ToString("N0", CultureInfo.CurrentCulture), UIattack);
            }
        }

        public string DPSReadout
        {
            get
            {
                if (Properties.Settings.Default.ShowRawDPS)
                {
                    return FormatNumberUI(ReadDPS);
                }
                else
                {
                    return PercentReadDPSReadout;
                }

            }
        }

        public string PercentReadDPSReadout
        {
            get
            {
                if (PercentReadDPS < -.5)
                {
                    return CombatantR.GetString("Blank", CultureInfo.CurrentUICulture);
                }
                else
                {
                    return string.Format(CultureInfo.CurrentUICulture, CombatantR.GetString("UI_PercentReadDPSReadout", CultureInfo.CurrentUICulture), PercentReadDPS);
                }
            }
        }

        private String FormatNumber(float value)
        {
            int num = (int)Math.Round(value);

            if (value >= 100000000)
                return (value / 1000000).ToString("#,0", CultureInfo.InvariantCulture) + "M";
            if (value >= 1000000)
                return (value / 1000000D).ToString("0.0", CultureInfo.InvariantCulture) + "M";
            if (value >= 100000)
                return (value / 1000).ToString("#,0", CultureInfo.InvariantCulture) + "K";
            if (value >= 1000)
                return (value / 1000D).ToString("0.0", CultureInfo.InvariantCulture) + "K";
            return value.ToString("#,0", CultureInfo.InvariantCulture);
        }

        private String FormatNumberUI(float value)
        {
            int num = (int)Math.Round(value);

            if (value >= 100000000)
                return (value / 1000000).ToString("#,0", CultureInfo.CurrentCulture) + "M";
            if (value >= 1000000)
                return (value / 1000000D).ToString("0.0", CultureInfo.CurrentCulture) + "M";
            if (value >= 100000)
                return (value / 1000).ToString("#,0", CultureInfo.CurrentCulture) + "K";
            if (value >= 1000)
                return (value / 1000D).ToString("0.0", CultureInfo.CurrentCulture) + "K";
            return value.ToString("#,0", CultureInfo.CurrentCulture);
        }
        public string DamageReadout
        {
            get
            {
                return ReadDamage.ToString("N0", CultureInfo.CurrentCulture);
            }
        }

        public Combatant(string id, string name)
        {
            ID = id;
            Name = name;
            PercentDPS = -1;
            Attacks = new List<Attack>();
            isTemporary = "no";
            PercentReadDPS = 0;
            ActiveTime = 0;
            green = Color.FromArgb(160, 32, 130, 32);
            CombatantR = new ResourceManager("OverParse.CombatantResources", Assembly.GetExecutingAssembly());
        }

        public Combatant(string id, string name, string temp)
        {
            ID = id;
            Name = name;
            PercentDPS = -1;
            Attacks = new List<Attack>();
            isTemporary = temp;
            PercentReadDPS = 0;
            ActiveTime = 0;
            green = Color.FromArgb(160, 32, 130, 32);
        }

    }
}
