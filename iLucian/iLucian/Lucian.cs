﻿namespace iLucian
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using DZLib.Core;
    using DZLib.Positioning;

    using iLucian.MenuHelper;
    using iLucian.Utils;

    using LeagueSharp;
    using LeagueSharp.Common;

    using SharpDX;

    using ActiveGapcloser = DZLib.Core.ActiveGapcloser;
    using Color = System.Drawing.Color;

    class Lucian
    {
        #region Public Methods and Operators

        public static float GetComboDamage(Obj_AI_Base target)
        {
            float damage = 0;
            if (Variables.Spell[Variables.Spells.Q].IsReady())
                damage = damage + Variables.Spell[Variables.Spells.Q].GetDamage(target)
                         + (float)ObjectManager.Player.GetAutoAttackDamage(target);
            if (Variables.Spell[Variables.Spells.W].IsReady())
                damage = damage + Variables.Spell[Variables.Spells.W].GetDamage(target)
                         + (float)ObjectManager.Player.GetAutoAttackDamage(target);
            if (Variables.Spell[Variables.Spells.E].IsReady()) damage = damage + (float)ObjectManager.Player.GetAutoAttackDamage(target) * 2;

            damage = (float)(damage + ObjectManager.Player.GetAutoAttackDamage(target));

            return damage;
        }

        public void AutoHarass()
        {
            if (!Variables.Menu.Item("com.ilucian.harass.auto.autoharass").GetValue<KeyBind>().Active) return;

            var target = TargetSelector.GetTarget(
                Variables.Spell[Variables.Spells.Q2].Range, 
                TargetSelector.DamageType.Physical);

            if (Variables.Menu.IsEnabled("com.ilucian.harass.auto.q") && Variables.Spell[Variables.Spells.Q].IsReady())
            {
                if (Variables.Spell[Variables.Spells.Q].IsEnabledAndReady(ManaMeneger.Mode.Harass)
                    && Variables.Spell[Variables.Spells.Q].IsInRange(target) && target.IsValidTarget())
                {
                    Variables.Spell[Variables.Spells.Q].Cast(target);
                }
            }

            if (Variables.Menu.IsEnabled("com.ilucian.harass.auto.qExtended")
                && Variables.Spell[Variables.Spells.Q].IsReady())
            {
                CastExtendedQ();
            }
        }

        /// <summary>
        ///     Credits to Myo, stolen from him, ily :^)
        /// </summary>
        /// <param name="point1"></param>
        /// <param name="point2"></param>
        /// <param name="angle"></param>
        /// <returns></returns>
        public Vector2 Deviation(Vector2 point1, Vector2 point2, double angle)
        {
            angle *= Math.PI / 180.0;
            var temp = Vector2.Subtract(point2, point1);
            var result = new Vector2(0)
                             {
                                 X = (float)(temp.X * Math.Cos(angle) - temp.Y * Math.Sin(angle)) / 4, 
                                 Y = (float)(temp.X * Math.Sin(angle) + temp.Y * Math.Cos(angle)) / 4
                             };
            result = Vector2.Add(result, point1);
            return result;
        }

        public List<Obj_AI_Base> GetHittableTargets()
        {
            var unitList = new List<Obj_AI_Base>();
            var minions = MinionManager.GetMinions(
                ObjectManager.Player.Position, 
                Variables.Spell[Variables.Spells.Q].Range);
            var champions =
                HeroManager.Enemies.Where(
                    x =>
                    ObjectManager.Player.Distance(x) <= Variables.Spell[Variables.Spells.Q].Range
                    && !x.HasBuffOfType(BuffType.SpellShield)
                    && !Variables.Menu.IsEnabled("com.ilucian.harass.whitelist." + x.ChampionName.ToLower()));

            unitList.AddRange(minions);

            /*if (Variables.Menu.IsEnabled("com.ilucian.misc.extendChamps"))
            {
                unitList.AddRange(champions);
            }*/
            return unitList;
        }

        public void Killsteal()
        {
            var target =
                TargetSelector.GetTarget(
                    Variables.Spell[Variables.Spells.E].Range + Variables.Spell[Variables.Spells.Q2].Range, 
                    TargetSelector.DamageType.Physical);

            if (!Variables.Menu.IsEnabled("com.ilucian.misc.eqKs") || !Variables.Spell[Variables.Spells.Q].IsReady()
                || !target.IsValidTarget(
                    Variables.Spell[Variables.Spells.E].Range + Variables.Spell[Variables.Spells.Q2].Range))
            {
                return;
            }

            if (Variables.Spell[Variables.Spells.Q].GetDamage(target) - 20 >= target.Health)
            {
                if (target.IsValidTarget(Variables.Spell[Variables.Spells.Q].Range))
                {
                    Variables.Spell[Variables.Spells.Q].Cast(target);
                }

                if (target.IsValidTarget(Variables.Spell[Variables.Spells.Q2].Range)
                    && !target.IsValidTarget(Variables.Spell[Variables.Spells.Q].Range))
                {
                    CastExtendedQ();
                }
                else if (Variables.Spell[Variables.Spells.E].IsReady() && Variables.Spell[Variables.Spells.Q].IsReady())
                {
                    CastEqKillsteal();
                }
            }
        }

        public void OnLoad()
        {
            Console.WriteLine("Loaded Lucian");
            MenuGenerator.Generate();
            AutoCleanse.OnLoad();
            LoadSpells();
            LoadEvents();

            Game.PrintChat("[iLucian] -> Don't forget to upvote on assembly database.");
        }

        public void SemiUlt()
        {
            var target = TargetSelector.SelectedTarget != null
                             ? TargetSelector.GetSelectedTarget()
                             : TargetSelector.GetTarget(
                                 Variables.Spell[Variables.Spells.R].Range, 
                                 TargetSelector.DamageType.Physical);
            if (target.IsValid && Variables.Spell[Variables.Spells.R].IsReady()
                && !ObjectManager.Player.HasBuff("LucianR"))
            {
                Variables.Spell[Variables.Spells.R].Cast(target.Position);
            }
        }

        public void UltimateLock()
        {
            var currentTarget = TargetSelector.GetSelectedTarget();
            if (currentTarget.IsValidTarget())
            {
                var predictedPosition = Variables.Spell[Variables.Spells.R].GetPrediction(currentTarget).UnitPosition;
                var directionVector = (currentTarget.ServerPosition - ObjectManager.Player.ServerPosition).Normalized();
                const float RRangeCoefficient = 0.95f;
                var rRangeAdjusted = Variables.Spell[Variables.Spells.R].Range * RRangeCoefficient;
                var rEndPointXCoordinate = predictedPosition.X + directionVector.X * rRangeAdjusted;
                var rEndPointYCoordinate = predictedPosition.Y + directionVector.Y * rRangeAdjusted;
                var rEndPoint = new Vector2(rEndPointXCoordinate, rEndPointYCoordinate).To3D();

                if (rEndPoint.Distance(ObjectManager.Player.ServerPosition) < Variables.Spell[Variables.Spells.R].Range)
                {
                    ObjectManager.Player.IssueOrder(GameObjectOrder.MoveTo, rEndPoint);
                }
            }
        }

        #endregion

        #region Methods

        private static void OnEnemyGapcloser(LeagueSharp.Common.ActiveGapcloser gapcloser)
        {
            if (!Variables.Menu.IsEnabled("com.ilucian.misc.gapcloser"))
            {
                return;
            }

            if (!gapcloser.Sender.IsEnemy || !(gapcloser.End.Distance(ObjectManager.Player.ServerPosition) < 300)) return;

            var extendedPosition = ObjectManager.Player.ServerPosition.Extend(
                Game.CursorPos, 
                Variables.Spell[Variables.Spells.E].Range);
            if (extendedPosition.IsSafe(Variables.Spell[Variables.Spells.E].Range))
            {
                Variables.Spell[Variables.Spells.E].Cast(extendedPosition);
            }
        }

        private void CastE(Obj_AI_Base target)
        {
            // TODO check possible wall dashes :^)
            if (!Variables.Spell[Variables.Spells.E].IsReady() || !Variables.Menu.IsEnabled("com.ilucian.combo.e")
                || target == null || ObjectManager.Player.HasBuff("LucianR") || ObjectManager.Player.IsWindingUp)
            {
                return;
            }

            var dashRange = Variables.Menu.Item("com.ilucian.combo.eRange").GetValue<Slider>().Value;

            switch (Variables.Menu.Item("com.ilucian.combo.eMode").GetValue<StringList>().SelectedIndex)
            {
                case 0: // kite
                    var hypotheticalPosition = ObjectManager.Player.ServerPosition.Extend(
                        Game.CursorPos, 
                        Variables.Spell[Variables.Spells.E].Range);
                    if (ObjectManager.Player.HealthPercent <= 70
                        && target.HealthPercent >= ObjectManager.Player.HealthPercent)
                    {
                        if (ObjectManager.Player.Position.Distance(ObjectManager.Player.ServerPosition) >= 35
                            && target.Distance(ObjectManager.Player.ServerPosition)
                            < target.Distance(ObjectManager.Player.Position)
                            && hypotheticalPosition.IsSafe(Variables.Spell[Variables.Spells.E].Range))
                        {
                            Variables.Spell[Variables.Spells.E].Cast(hypotheticalPosition);
                        }
                    }

                    if (hypotheticalPosition.IsSafe(Variables.Spell[Variables.Spells.E].Range)
                        && hypotheticalPosition.Distance(target.ServerPosition)
                        <= Orbwalking.GetRealAutoAttackRange(null)
                        && (hypotheticalPosition.Distance(target.ServerPosition) > 400) && !Variables.HasPassive())
                    {
                        Variables.Spell[Variables.Spells.E].Cast(hypotheticalPosition);
                    }

                    break;

                case 1: // side
                    Variables.Spell[Variables.Spells.E].Cast(
                        Deviation(ObjectManager.Player.Position.To2D(), target.Position.To2D(), dashRange).To3D());
                    break;

                case 2: // Cursor
                    if (Game.CursorPos.IsSafe(475))
                    {
                        Variables.Spell[Variables.Spells.E].Cast(
                            ObjectManager.Player.Position.Extend(Game.CursorPos, dashRange));
                    }

                    break;

                case 3: // Enemy
                    Variables.Spell[Variables.Spells.E].Cast(
                        ObjectManager.Player.Position.Extend(target.Position, dashRange));
                    break;
                case 4:
                    Variables.Spell[Variables.Spells.E].Cast(
                        Deviation(ObjectManager.Player.Position.To2D(), target.Position.To2D(), 65f).To3D());
                    break;
                case 5: // Smart E Credits to ASUNOOO
                    var ePosition = new EPosition();
                    var bestPosition = ePosition.GetEPosition();
                    if (bestPosition != Vector3.Zero
                        && bestPosition.Distance(target.ServerPosition) < Orbwalking.GetRealAutoAttackRange(target))
                    {
                        Variables.Spell[Variables.Spells.E].Cast(bestPosition);
                    }

                    break;
            }
        }

        private void CastEqKillsteal()
        {
            var target =
                TargetSelector.GetTarget(
                    Variables.Spell[Variables.Spells.E].Range + Variables.Spell[Variables.Spells.Q2].Range, 
                    TargetSelector.DamageType.Physical);

            if (
                !target.IsValidTarget(
                    Variables.Spell[Variables.Spells.E].Range + Variables.Spell[Variables.Spells.Q2].Range)) return;

            var dashSpeed = (int)(Variables.Spell[Variables.Spells.E].Range / (700 + ObjectManager.Player.MoveSpeed));
            var extendedPrediction = GetExtendedPrediction(target, dashSpeed);

            var minions =
                ObjectManager.Get<Obj_AI_Minion>()
                    .Where(x => x.IsEnemy && x.IsValid && x.Distance(extendedPrediction, true) < 900 * 900)
                    .OrderByDescending(x => x.Distance(extendedPrediction));

            foreach (var minion in
                minions.Select(x => Prediction.GetPrediction(x, dashSpeed))
                    .Select(
                        pred =>
                        MathHelper.GetCicleLineInteraction(
                            pred.UnitPosition.To2D(), 
                            extendedPrediction.To2D(), 
                            ObjectManager.Player.ServerPosition.To2D(), 
                            Variables.Spell[Variables.Spells.E].Range))
                    .Select(inter => inter.GetBestInter(target)))
            {
                if (Math.Abs(minion.X) < 1) return;

                if (!NavMesh.GetCollisionFlags(minion.To3D()).HasFlag(CollisionFlags.Wall)
                    && !NavMesh.GetCollisionFlags(minion.To3D()).HasFlag(CollisionFlags.Building)
                    && minion.To3D().IsSafe(Variables.Spell[Variables.Spells.E].Range))
                {
                    Variables.Spell[Variables.Spells.E].Cast((Vector3)minion);
                }
            }

            var champions =
                ObjectManager.Get<Obj_AI_Hero>()
                    .Where(x => x.IsEnemy && x.IsValid && x.Distance(extendedPrediction, true) < 900 * 900)
                    .OrderByDescending(x => x.Distance(extendedPrediction));

            if (Variables.Menu.IsEnabled("com.ilucian.misc.useChampions"))
            {
                foreach (var position in
                    champions.Select(x => Prediction.GetPrediction(x, dashSpeed))
                        .Select(
                            pred =>
                            MathHelper.GetCicleLineInteraction(
                                pred.UnitPosition.To2D(), 
                                extendedPrediction.To2D(), 
                                ObjectManager.Player.ServerPosition.To2D(), 
                                Variables.Spell[Variables.Spells.E].Range))
                        .Select(inter => inter.GetBestInter(target)))
                {
                    if (Math.Abs(position.X) < 1) return;

                    if (!NavMesh.GetCollisionFlags(position.To3D()).HasFlag(CollisionFlags.Wall)
                        && !NavMesh.GetCollisionFlags(position.To3D()).HasFlag(CollisionFlags.Building)
                        && position.To3D().IsSafe(Variables.Spell[Variables.Spells.E].Range))
                    {
                        Variables.Spell[Variables.Spells.E].Cast((Vector3)position);
                    }
                }
            }
        }

        private void CastExtendedQ()
        {
            if (!Variables.Spell[Variables.Spells.Q].IsReady())
            {
                return;
            }

            var target = TargetSelector.SelectedTarget != null
                         && TargetSelector.SelectedTarget.Distance(ObjectManager.Player) < 1800
                             ? TargetSelector.SelectedTarget
                             : TargetSelector.GetTarget(
                                 Variables.Spell[Variables.Spells.Q2].Range, 
                                 TargetSelector.DamageType.Physical);

            var predictionPosition = Variables.Spell[Variables.Spells.Q2].GetPrediction(target);

            foreach (var unit in from unit in GetHittableTargets()
                                 let polygon =
                                     new Geometry.Polygon.Rectangle(
                                     ObjectManager.Player.ServerPosition, 
                                     ObjectManager.Player.ServerPosition.Extend(
                                         unit.ServerPosition, 
                                         Variables.Spell[Variables.Spells.Q2].Range), 
                                     65f)
                                 where polygon.IsInside(predictionPosition.CastPosition)
                                 select unit)
            {
                Variables.Spell[Variables.Spells.Q].Cast(unit);
            }
        }

        // Detuks ofc
        private Vector3 GetExtendedPrediction(Obj_AI_Hero target, int delay)
        {
            var res = Variables.Spell[Variables.Spells.Q2].GetPrediction(target);
            var del = Prediction.GetPrediction(target, delay);

            var dif = del.UnitPosition - target.ServerPosition;
            return res.CastPosition + dif;
        }

        private void LoadEvents()
        {
            Game.OnUpdate += OnUpdate;
            Obj_AI_Base.OnDoCast += OnDoCast;
            AntiGapcloser.OnEnemyGapcloser += OnEnemyGapcloser;
            Spellbook.OnCastSpell += (sender, args) =>
                {
                    if (sender.Owner.IsMe && args.Slot == SpellSlot.E)
                    {
                        Variables.LastECast = Environment.TickCount;
                    }
                };

            Obj_AI_Base.OnProcessSpellCast += OnSpellCast;
            Drawing.OnDraw += args =>
                {
                    if (Variables.Menu.IsEnabled("com.ilucian.misc.drawQ"))
                    {
                        Render.Circle.DrawCircle(
                            ObjectManager.Player.Position, 
                            Variables.Spell[Variables.Spells.Q2].Range, 
                            Color.BlueViolet);
                    }
                };
        }

        private void LoadSpells()
        {
            Variables.Spell[Variables.Spells.Q].SetTargetted(0.25f, 1400f);
            Variables.Spell[Variables.Spells.Q2].SetSkillshot(
                0.5f, 
                50, 
                float.MaxValue, 
                false, 
                SkillshotType.SkillshotLine);
            Variables.Spell[Variables.Spells.W].SetSkillshot(0.30f, 70f, 1600f, true, SkillshotType.SkillshotLine);
            Variables.Spell[Variables.Spells.R].SetSkillshot(0.2f, 110f, 2500, true, SkillshotType.SkillshotLine);
        }

        private void OnDoCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (!sender.IsMe) return;

            var target = TargetSelector.GetTarget(
                Variables.Spell[Variables.Spells.Q].Range, 
                TargetSelector.DamageType.Physical);

            if (target == null || Environment.TickCount - Variables.LastECast < 250) return;
            switch (Variables.Orbwalker.ActiveMode)
            {
                case Orbwalking.OrbwalkingMode.Combo:
                    if (Orbwalking.IsAutoAttack(args.SData.Name) && target.IsValid)
                    {
                        if (Variables.Menu.IsEnabled("com.ilucian.combo.startE")
                            && Variables.Spell[Variables.Spells.E].IsReady())
                        {
                            if (!sender.IsDead && !Variables.HasPassive())
                            {
                                CastE(target);
                            }

                            if (!Variables.Spell[Variables.Spells.E].IsReady()
                                && target.IsValidTarget(Variables.Spell[Variables.Spells.Q].Range)
                                && Variables.Menu.Item("com.ilucian.combo.q").GetValue<bool>()
                                && !Variables.HasPassive())
                            {
                                if (Variables.Spell[Variables.Spells.Q].IsReady()
                                    && Variables.Spell[Variables.Spells.Q].IsInRange(target)
                                    && !ObjectManager.Player.IsDashing())
                                {
                                    Variables.Spell[Variables.Spells.Q].Cast(target);
                                }
                            }

                            if (!Variables.Spell[Variables.Spells.E].IsReady() && !ObjectManager.Player.IsDashing()
                                && Variables.Menu.Item("com.ilucian.combo.w").GetValue<bool>())
                            {
                                if (Variables.Spell[Variables.Spells.W].IsReady() && !Variables.HasPassive())
                                {
                                    if (Variables.Menu.IsEnabled("com.ilucian.misc.usePrediction"))
                                    {
                                        var prediction = Variables.Spell[Variables.Spells.W].GetPrediction(target);
                                        if (prediction.Hitchance >= HitChance.High)
                                        {
                                            Variables.Spell[Variables.Spells.W].Cast(prediction.CastPosition);
                                        }
                                    }
                                    else
                                    {
                                        if (target.Distance(ObjectManager.Player) < 600)
                                        {
                                            Variables.Spell[Variables.Spells.W].Cast(target.Position);
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (target.IsValidTarget(Variables.Spell[Variables.Spells.Q].Range)
                                && Variables.Menu.Item("com.ilucian.combo.q").GetValue<bool>()
                                && !Variables.HasPassive())
                            {
                                if (Variables.Spell[Variables.Spells.Q].IsReady()
                                    && Variables.Spell[Variables.Spells.Q].IsInRange(target)
                                    && !ObjectManager.Player.IsDashing())
                                {
                                    Variables.Spell[Variables.Spells.Q].Cast(target);
                                }
                            }

                            if (!ObjectManager.Player.IsDashing()
                                && Variables.Menu.Item("com.ilucian.combo.w").GetValue<bool>())
                            {
                                if (Variables.Spell[Variables.Spells.W].IsReady() && !Variables.HasPassive())
                                {
                                    if (Variables.Menu.IsEnabled("com.ilucian.misc.usePrediction"))
                                    {
                                        var prediction = Variables.Spell[Variables.Spells.W].GetPrediction(target);
                                        if (prediction.Hitchance >= HitChance.High)
                                        {
                                            Variables.Spell[Variables.Spells.W].Cast(prediction.CastPosition);
                                        }
                                    }
                                    else
                                    {
                                        if (target.Distance(ObjectManager.Player) < 600)
                                        {
                                            Variables.Spell[Variables.Spells.W].Cast(target.Position);
                                        }
                                    }
                                }
                            }
                        }

                        if (!sender.IsDead && !Variables.HasPassive() && !Variables.Spell[Variables.Spells.Q].IsReady())
                        {
                            CastE(target);
                        }
                    }

                    break;
                case Orbwalking.OrbwalkingMode.Mixed:
                    if (Orbwalking.IsAutoAttack(args.SData.Name) && target.IsValid)
                    {
                        if (target.IsValidTarget(Variables.Spell[Variables.Spells.Q].Range)
                            && Variables.Menu.Item("com.ilucian.harass.q").GetValue<bool>())
                        {
                            if (Variables.Spell[Variables.Spells.Q].IsReady()
                                && Variables.Spell[Variables.Spells.Q].IsInRange(target))
                            {
                                Variables.Spell[Variables.Spells.Q].Cast(target);
                            }
                        }

                        if (!ObjectManager.Player.IsDashing()
                            && Variables.Menu.Item("com.ilucian.harass.w").GetValue<bool>())
                        {
                            if (Variables.Spell[Variables.Spells.W].IsReady())
                            {
                                if (Variables.Menu.IsEnabled("com.ilucian.misc.usePrediction"))
                                {
                                    var prediction = Variables.Spell[Variables.Spells.W].GetPrediction(target);
                                    if (prediction.Hitchance >= HitChance.High)
                                    {
                                        Variables.Spell[Variables.Spells.W].Cast(prediction.CastPosition);
                                    }
                                }
                                else
                                {
                                    if (target.Distance(ObjectManager.Player) < 600)
                                    {
                                        Variables.Spell[Variables.Spells.W].Cast(target.Position);
                                    }
                                }
                            }
                        }
                    }

                    break;
            }
        }

        private void OnHarass()
        {
            var target = TargetSelector.GetTarget(
                Variables.Spell[Variables.Spells.Q2].Range, 
                TargetSelector.DamageType.Physical);

            if (target == null || Variables.HasPassive()) return;
            if (Variables.Menu.IsEnabled("com.ilucian.harass.qExtended"))
            {
                CastExtendedQ();
            }

            if (target.IsValidTarget(Variables.Spell[Variables.Spells.Q].Range)
                && Variables.Menu.IsEnabled("com.ilucian.harass.q"))
            {
                if (Variables.Spell[Variables.Spells.Q].IsReady()
                    && Variables.Spell[Variables.Spells.Q].IsInRange(target))
                {
                    Variables.Spell[Variables.Spells.Q].Cast(target);
                }
            }

            if (!ObjectManager.Player.IsDashing() && Variables.Menu.IsEnabled("com.ilucian.harass.w"))
            {
                if (Variables.Spell[Variables.Spells.W].IsReady())
                {
                    if (Variables.Menu.IsEnabled("com.ilucian.misc.usePrediction"))
                    {
                        var prediction = Variables.Spell[Variables.Spells.W].GetPrediction(target);
                        if (prediction.Hitchance >= HitChance.High)
                        {
                            Variables.Spell[Variables.Spells.W].Cast(prediction.CastPosition);
                        }
                    }
                    else
                    {
                        if (target.Distance(ObjectManager.Player) < 600)
                        {
                            Variables.Spell[Variables.Spells.W].Cast(target.Position);
                        }
                    }
                }
            }
        }

        private void OnJungleclear()
        {
            var jungleMob =
                MinionManager.GetMinions(
                    Orbwalking.GetRealAutoAttackRange(ObjectManager.Player), 
                    MinionTypes.All, 
                    MinionTeam.Neutral, 
                    MinionOrderTypes.MaxHealth).FirstOrDefault(x => x.IsValid);

            if (jungleMob != null)
            {
                if (Variables.Spell[Variables.Spells.Q].IsReady()
                    && Variables.Menu.IsEnabled("com.ilucian.jungleclear.q") && !Variables.HasPassive())
                {
                    Variables.Spell[Variables.Spells.Q].Cast(jungleMob);
                }

                if (Variables.Spell[Variables.Spells.W].IsReady()
                    && Variables.Menu.IsEnabled("com.ilucian.jungleclear.w") && !Variables.HasPassive())
                {
                    Variables.Spell[Variables.Spells.W].Cast(jungleMob);
                }

                if (Variables.Spell[Variables.Spells.E].IsReady()
                    && Variables.Menu.IsEnabled("com.ilucian.jungleclear.e") && !Variables.HasPassive())
                {
                    Variables.Spell[Variables.Spells.E].Cast(ObjectManager.Player.Position.Extend(Game.CursorPos, 475));
                }
            }
        }

        private void OnLaneclear()
        {
            if (Variables.Menu.IsEnabled("com.ilucian.laneclear.q"))
            {
                var minions = MinionManager.GetMinions(Variables.Spell[Variables.Spells.Q].Range);
                var bestLocation = Variables.Spell[Variables.Spells.Q].GetCircularFarmLocation(minions, 60);

                if (bestLocation.MinionsHit
                    < Variables.Menu.Item("com.ilucian.laneclear.qMinions").GetValue<Slider>().Value) return;
                var adjacentMinions = minions.Where(m => m.Distance(bestLocation.Position) <= 45).ToList();
                if (!adjacentMinions.Any())
                {
                    return;
                }

                var firstMinion = adjacentMinions.OrderBy(m => m.Distance(bestLocation.Position)).First();

                if (!firstMinion.IsValidTarget(Variables.Spell[Variables.Spells.Q].Range)) return;
                if (!Variables.HasPassive() && Orbwalking.InAutoAttackRange(firstMinion))
                {
                    Variables.Spell[Variables.Spells.Q].Cast(firstMinion);
                }
            }
        }

        private void OnSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (Variables.Menu.IsEnabled("com.ilucian.misc.antiVayne") && sender.IsEnemy && sender.IsChampion("Vayne")
                && args.Slot == SpellSlot.E)
            {
                var predictedPosition = ObjectManager.Player.ServerPosition.Extend(sender.Position, -425);
                var dashPosition = ObjectManager.Player.Position.Extend(Game.CursorPos, 450);
                var dashCondemnCheck = dashPosition.Extend(sender.Position, -425);

                if (Variables.Spell[Variables.Spells.E].IsReady() && predictedPosition.IsWall()
                    && !dashCondemnCheck.IsWall())
                {
                    Variables.Spell[Variables.Spells.E].Cast(dashPosition);
                }
            }
        }

        private void OnUpdate(EventArgs args)
        {
            Variables.Spell[Variables.Spells.W].Collision =
                Variables.Menu.Item("com.ilucian.misc.usePrediction").GetValue<bool>();
            Killsteal();

            if (Variables.Menu.Item("com.ilucian.combo.forceR").GetValue<KeyBind>().Active)
            {
                SemiUlt();
                ObjectManager.Player.IssueOrder(GameObjectOrder.MoveTo, Game.CursorPos);
            }

            if (Variables.Menu.IsEnabled("com.ilucian.misc.forcePassive") && Variables.HasPassive())
            {
                var target = TargetSelector.GetTarget(
                    ObjectManager.Player.AttackRange, 
                    TargetSelector.DamageType.Physical);
                if (target != null && target.IsValid)
                {
                    Variables.Orbwalker.ForceTarget(target);
                }
            }

            AutoHarass();
            switch (Variables.Orbwalker.ActiveMode)
            {
                case Orbwalking.OrbwalkingMode.Combo:

                    // OnCombo();
                    if (Variables.Menu.IsEnabled("com.ilucian.combo.qExtended"))
                    {
                        CastExtendedQ();
                    }

                    break;
                case Orbwalking.OrbwalkingMode.Mixed:
                    OnHarass();
                    break;
                case Orbwalking.OrbwalkingMode.LaneClear:
                    OnLaneclear();
                    OnJungleclear();

                    // TODO OnTurretClear
                    break;
                case Orbwalking.OrbwalkingMode.LastHit:
                    break;
                case Orbwalking.OrbwalkingMode.Freeze:
                    break;
                case Orbwalking.OrbwalkingMode.CustomMode:
                    break;
                case Orbwalking.OrbwalkingMode.None:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        #endregion
    }
}