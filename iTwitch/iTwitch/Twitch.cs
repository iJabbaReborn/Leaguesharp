namespace iTwitch
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Linq;

    using DZLib.MenuExtensions;

    using LeagueSharp;
    using LeagueSharp.Common;

    using SharpDX;

    using Color = System.Drawing.Color;
    using ItemData = LeagueSharp.Common.Data.ItemData;

    public class Twitch
    {
        #region Static Fields

        /// <summary>
        ///     The dictionary to call the Spell slot and the Spell Class
        /// </summary>
        public static readonly Dictionary<SpellSlot, Spell> Spells = new Dictionary<SpellSlot, Spell>
                                                                         {
                                                                             { SpellSlot.Q, new Spell(SpellSlot.Q) }, 
                                                                             { SpellSlot.W, new Spell(SpellSlot.W, 950f) }, 
                                                                             { SpellSlot.E, new Spell(SpellSlot.E, 1100) }, 
                                                                             { SpellSlot.R, new Spell(SpellSlot.R) }, 
                                                                         };

        #endregion

        #region Fields

        private Menu menu;

        private Orbwalking.Orbwalker orbwalker;

        #endregion

        #region Public Methods and Operators

        public void LoadMenu()
        {
            menu = new Menu("iTwitch 2.0 - Hawk Mode", "com.itwitch", true).SetFontStyle(
                FontStyle.Bold, 
                SharpDX.Color.AliceBlue);

            var owMenu = new Menu(":: Orbwalker", "com.itwitch.orbwalker");
            {
                orbwalker = new Orbwalking.Orbwalker(owMenu);
                menu.AddSubMenu(owMenu);
            }

            var comboMenu = new Menu(":: iTwitch 2.0 - Combo Options", "com.itwitch.combo");
            {
                comboMenu.AddBool("com.itwitch.combo.useW", "Use W", true);
                comboMenu.AddBool("com.itwitch.combo.useEKillable", "Use E Killable", true);
                menu.AddSubMenu(comboMenu);
            }

            var harassMenu = new Menu(":: iTwitch 2.0 - Harass Options", "com.itwitch.harass");
            {
                harassMenu.AddBool("com.itwitch.harass.useW", "Use W");
                harassMenu.AddBool("com.itwitch.harass.useEKillable", "Use E", true);
                menu.AddSubMenu(harassMenu);
            }

            var miscMenu = new Menu(":: iTwitch 2.0 - Misc Options", "com.itwitch.misc");
            {
                miscMenu.AddBool("com.itwitch.misc.autoYo", "Youmuus with R", true);
                miscMenu.AddBool("com.itwitch.misc.noWTurret", "Don't W Under Tower", true);
                miscMenu.AddSlider("com.itwitch.misc.noWAA", "No W if x aa can kill", 2, 0, 10);
                miscMenu.AddBool("com.itwitch.misc.saveManaE", "Save Mana for E", true);
                miscMenu.AddBool("com.itwitch.misc.Exploit", "Use Exploit").SetTooltip("Will Instant Q After Kill");
                miscMenu.AddBool("com.itwitch.misc.EAAQ", "E AA Q")
                    .SetTooltip("Will cast E if killable by E + AA then Q");
                miscMenu.AddKeybind(
                    "com.itwitch.misc.recall", 
                    "Stealth Recall", 
                    new Tuple<uint, KeyBindType>("T".ToCharArray()[0], KeyBindType.Press));
                menu.AddSubMenu(miscMenu);
            }

            var drawingMenu = new Menu(":: iTwitch 2.0 - Drawing Options", "com.itwitch.drawing");
            {
                drawingMenu.AddBool("com.itwitch.drawing.drawERange", "Draw E Range", true);
                drawingMenu.AddBool("com.itwitch.drawing.drawRRange", "Draw R Range", true);
                drawingMenu.AddBool("com.itwitch.drawing.drawQTime", "Draw Q Time", true);
                drawingMenu.AddBool("com.itwitch.drawing.drawEStacks", "Draw E Stacks", true);
                drawingMenu.AddBool("com.itwitch.drawing.drawEStackT", "Draw E Stack Time", true);
                drawingMenu.AddBool("com.itwitch.drawing.drawRTime", "Draw R Time", true);
                drawingMenu.AddItem(
                    new MenuItem("com.itwitch.drawing.eDamage", "Draw E Damage on Enemies").SetValue(
                        new Circle(true, Color.DarkOliveGreen)));
                menu.AddSubMenu(drawingMenu);
            }

            menu.AddToMainMenu();
        }

        public void LoadSpells()
        {
            Spells[SpellSlot.W].SetSkillshot(0.25f, 120f, 1400f, false, SkillshotType.SkillshotCircle);
        }

        public void OnCombo()
        {
            if (menu.Item("com.itwitch.combo.useW").GetValue<bool>() && Spells[SpellSlot.W].IsReady())
            {
                if (menu.Item("com.itwitch.misc.saveManaE").GetValue<bool>()
                    && ObjectManager.Player.Mana <= Spells[SpellSlot.E].ManaCost + Spells[SpellSlot.W].ManaCost)
                {
                    return;
                }

                if (menu.Item("com.itwitch.misc.noWTurret").GetValue<bool>() && ObjectManager.Player.UnderTurret(true))
                {
                    return;
                }

                var wTarget = TargetSelector.GetTarget(Spells[SpellSlot.W].Range, TargetSelector.DamageType.Physical);

                if (wTarget != null
                    && wTarget.Health
                    < ObjectManager.Player.GetAutoAttackDamage(wTarget, true)
                    * menu.Item("com.itwitch.misc.noWAA").GetValue<Slider>().Value) return;

                if (wTarget.IsValidTarget(Spells[SpellSlot.W].Range)
                    && !ObjectManager.Player.HasBuff("TwitchHideInShadows"))
                {
                    var prediction = Spells[SpellSlot.W].GetPrediction(wTarget);
                    if (prediction.Hitchance >= HitChance.High)
                    {
                        Spells[SpellSlot.W].Cast(prediction.CastPosition);
                    }
                }
            }
        }

        public void OnGameLoad(EventArgs args)
        {
            if (ObjectManager.Player.ChampionName != "Twitch") return;

            CustomDamageIndicator.Initialize(Extensions.GetPoisonDamage);

            LoadSpells();
            LoadMenu();

            Spellbook.OnCastSpell += (sender, eventArgs) =>
                {
                    if (eventArgs.Slot == SpellSlot.Recall && Spells[SpellSlot.Q].IsReady()
                        && menu.Item("com.itwitch.misc.recall").GetValue<KeyBind>().Active)
                    {
                        Spells[SpellSlot.Q].Cast();
                        Utility.DelayAction.Add(
                            (int)(Spells[SpellSlot.Q].Delay + 300), 
                            () => ObjectManager.Player.Spellbook.CastSpell(SpellSlot.Recall));
                        eventArgs.Process = false;
                        return;
                    }

                    if (eventArgs.Slot == SpellSlot.R && menu.Item("com.itwitch.misc.autoYo").GetValue<bool>())
                    {
                        if (!HeroManager.Enemies.Any(x => ObjectManager.Player.Distance(x) <= Spells[SpellSlot.R].Range)) return;

                        if (Items.HasItem(ItemData.Youmuus_Ghostblade.Id))
                        {
                            Items.UseItem(ItemData.Youmuus_Ghostblade.Id);
                        }
                    }

                    if (menu.Item("com.itwitch.misc.saveManaE").GetValue<bool>() && eventArgs.Slot == SpellSlot.W)
                    {
                        if (ObjectManager.Player.Mana <= Spells[SpellSlot.E].ManaCost + 10)
                        {
                            eventArgs.Process = false;
                        }
                    }
                };

            Game.OnUpdate += OnUpdate;
            Drawing.OnDraw += OnDraw;
        }

        public void OnHarass()
        {
            if (menu.Item("com.itwitch.harass.useW").GetValue<bool>() && Spells[SpellSlot.W].IsReady())
            {
                var wTarget = TargetSelector.GetTarget(Spells[SpellSlot.W].Range, TargetSelector.DamageType.Physical);
                if (wTarget.IsValidTarget(Spells[SpellSlot.W].Range))
                {
                    var prediction = Spells[SpellSlot.W].GetPrediction(wTarget);
                    if (prediction.Hitchance >= HitChance.High)
                    {
                        Spells[SpellSlot.W].Cast(prediction.CastPosition);
                    }
                }
            }
        }

        private void Exploit()
        {
            var target = TargetSelector.GetTarget(ObjectManager.Player.HasBuff("TwitchFullAutomatic") ? 850 : 550, TargetSelector.DamageType.Physical);
            if (target == null || !target.IsValidTarget() || target.IsInvulnerable || !Spells[SpellSlot.Q].IsReady()) return;

            if (Spells[SpellSlot.E].IsReady() && menu.Item("com.itwitch.misc.EAAQ").GetValue<bool>())
            {
                var realRange = ObjectManager.Player.HasBuff("TwitchFullAutomatic") ? 850 : 550;
                if (target.Distance(ObjectManager.Player) > realRange)
                {
                    return;
                }

                if (target.Health <= ObjectManager.Player.GetAutoAttackDamage(target) * (1.15) + Spells[SpellSlot.E].GetDamage(target) * (1.35))
                {
                    Spells[SpellSlot.E].Cast();
                }
            }

            if (target.Health <= ObjectManager.Player.GetAutoAttackDamage(target, true)
                && ObjectManager.Player.IsWindingUp)
            {
                Spells[SpellSlot.Q].Cast();
            }
        }

        #endregion

        #region Methods

        private void OnDraw(EventArgs args)
        {
            CustomDamageIndicator.DrawingColor = menu.Item("com.itwitch.drawing.eDamage").GetValue<Circle>().Color;
            CustomDamageIndicator.Enabled = menu.Item("com.itwitch.drawing.eDamage").GetValue<Circle>().Active;

            if (menu.Item("com.itwitch.drawing.drawRRange").GetValue<bool>())
            {
                Render.Circle.DrawCircle(ObjectManager.Player.Position, Spells[SpellSlot.R].Range, Color.BlueViolet);
            }

            if (menu.Item("com.itwitch.drawing.drawERange").GetValue<bool>())
            {
                Render.Circle.DrawCircle(ObjectManager.Player.Position, Spells[SpellSlot.E].Range, Color.BlueViolet);
            }

            if (menu.Item("com.itwitch.drawing.drawQTime").GetValue<bool>()
                && ObjectManager.Player.HasBuff("TwitchHideInShadows"))
            {
                var position = new Vector3(
                    ObjectManager.Player.Position.X, 
                    ObjectManager.Player.Position.Y - 30, 
                    ObjectManager.Player.Position.Z);
                position.DrawTextOnScreen(
                    "Stealth:  " + $"{ObjectManager.Player.GetRemainingBuffTime("TwitchHideInShadows"):0.0}", 
                    Color.AntiqueWhite);
            }

            if (menu.Item("com.itwitch.drawing.drawRTime").GetValue<bool>()
                && ObjectManager.Player.HasBuff("TwitchFullAutomatic"))
            {
                ObjectManager.Player.Position.DrawTextOnScreen(
                    "Ultimate:  " + $"{ObjectManager.Player.GetRemainingBuffTime("TwitchFullAutomatic"):0.0}", 
                    Color.AntiqueWhite);
            }

            if (menu.Item("com.itwitch.drawing.drawEStacks").GetValue<bool>())
            {
                foreach (var source in
                    HeroManager.Enemies.Where(x => x.HasBuff("TwitchDeadlyVenom") && !x.IsDead && x.IsVisible))
                {
                    var position = new Vector3(source.Position.X, source.Position.Y + 10, source.Position.Z);
                    position.DrawTextOnScreen($"{"Stacks: " + source.GetPoisonStacks()}", Color.AntiqueWhite);
                }
            }

            if (menu.Item("com.itwitch.drawing.drawEStackT").GetValue<bool>())
            {
                foreach (var source in
                    HeroManager.Enemies.Where(x => x.HasBuff("TwitchDeadlyVenom") && !x.IsDead && x.IsVisible))
                {
                    var position = new Vector3(source.Position.X, source.Position.Y - 30, source.Position.Z);
                    position.DrawTextOnScreen(
                        "Stack Timer:  " + $"{source.GetRemainingBuffTime("TwitchDeadlyVenom"):0.0}", 
                        Color.AntiqueWhite);
                }
            }
        }

        private void OnUpdate(EventArgs args)
        {
            if (menu.Item("com.itwitch.misc.recall").GetValue<KeyBind>().Active)
            {
                ObjectManager.Player.Spellbook.CastSpell(SpellSlot.Recall);
            }

            if (menu.Item("com.itwitch.misc.Exploit").GetValue<bool>()) Exploit();

            if (menu.Item("com.itwitch.combo.useEKillable").GetValue<bool>() && Spells[SpellSlot.E].IsReady())
            {
                if (menu.Item("com.itwitch.misc.EAAQ").GetValue<bool>() && Spells[SpellSlot.Q].IsReady()) return;

                if (HeroManager.Enemies.Any(x => x.IsPoisonKillable() && x.IsValidTarget(Spells[SpellSlot.E].Range)))
                {
                    Spells[SpellSlot.E].Cast();
                }
            }

            switch (orbwalker.ActiveMode)
            {
                case Orbwalking.OrbwalkingMode.Combo:
                    OnCombo();
                    break;

                case Orbwalking.OrbwalkingMode.Mixed:
                    OnHarass();
                    break;
            }
        }

        #endregion
    }
}