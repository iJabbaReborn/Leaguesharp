﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DZLib.Modules;
using LeagueSharp;
using LeagueSharp.Common;

namespace iLucian
{
    internal class Variables
    {
        public static readonly Dictionary<Spells, Spell> Spell = new Dictionary<Spells, Spell>
        {
            {Spells.Q, new Spell(SpellSlot.Q, 675)},
            {Spells.Q2, new Spell(SpellSlot.Q, 1200)},
            {Spells.W, new Spell(SpellSlot.W, 1000)},
            {Spells.E, new Spell(SpellSlot.E, 475)},
            {Spells.R, new Spell(SpellSlot.R, 3000f)}
        };

        public static Menu Menu { get; set; }
        public static Orbwalking.Orbwalker Orbwalker { get; set; }

        public static bool HasPassive()
        {
            return ObjectManager.Player.HasBuff("LucianPassiveBuff");
        }

        public static float GetComboDamage(Obj_AI_Base target)
        {
            var damage = 0f;

            if (Spell[Spells.Q].IsReady())
            {
                damage += Spell[Spells.Q].GetDamage(target);
            }
            if (Spell[Spells.W].IsReady())
            {
                damage += Spell[Spells.W].GetDamage(target);
            }
            if (Spell[Spells.E].IsReady())
            {
                damage += (float) ObjectManager.Player.GetAutoAttackDamage(target, true) * 2;
            }

            return damage;
        }

        internal enum Spells
        {
            Q,
            Q2,
            W,
            E,
            R
        }
    }
}