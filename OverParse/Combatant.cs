using System;
using System.Collections.Generic;
using System.Linq;

namespace OverParse
{
    public class Combatant
    {
        public int Damage;
        public int Healing;
        public string ID;
        public string Name {
            get; set; }
        public int MaxHitNum;
        public string MaxHitID;
        public float DPS;
        public float PercentDPS;
        public List<Attack> Attacks;

        public bool isAlly
        {
            get
            {
                string[] SuAttacks = {"487482498", "2785585589", "639929291"};
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