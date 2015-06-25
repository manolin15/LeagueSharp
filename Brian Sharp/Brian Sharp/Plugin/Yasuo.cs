﻿using System;
using System.Collections.Generic;
using System.Linq;
using BrianSharp.Common;
using LeagueSharp;
using LeagueSharp.Common;
using LeagueSharp.Common.Data;
using SharpDX;
using Color = System.Drawing.Color;
using Orbwalk = BrianSharp.Common.Orbwalker;

namespace BrianSharp.Plugin
{
    internal class Yasuo : Helper
    {
        private const int QCirWidth = 300, RWidth = 400;

        public Yasuo()
        {
            Q = new Spell(SpellSlot.Q, 500);
            Q2 = new Spell(SpellSlot.Q, 1100);
            W = new Spell(SpellSlot.W, 400);
            E = new Spell(SpellSlot.E, 475, TargetSelector.DamageType.Magical);
            R = new Spell(SpellSlot.R, 1200);
            Q.SetSkillshot(GetQDelay, 20, float.MaxValue, false, SkillshotType.SkillshotLine);
            Q2.SetSkillshot(GetQ2Delay, 90, 1500, false, SkillshotType.SkillshotLine);

            var champMenu = new Menu("Plugin", Player.ChampionName + "_Plugin");
            {
                var comboMenu = new Menu("Combo", "Combo");
                {
                    AddBool(comboMenu, "Q", "Use Q");
                    AddBool(comboMenu, "E", "Use E");
                    AddBool(comboMenu, "EDmg", "-> Deal Damage (Q Must On)");
                    AddBool(comboMenu, "EGap", "-> Gap Closer");
                    AddSlider(comboMenu, "EGapRange", "-> If Enemy Not In", 300, 1, 475);
                    AddBool(comboMenu, "EGapTower", "-> Under Tower", false);
                    AddBool(comboMenu, "R", "Use R");
                    AddBool(comboMenu, "RDelay", "-> Delay");
                    AddSlider(comboMenu, "RDelayTime", "--> Time (ms)", 200, 200, 400);
                    AddSlider(comboMenu, "RHpU", "-> If Enemy Hp Under", 60);
                    AddSlider(comboMenu, "RCountA", "-> Or Enemy Above", 2, 1, 5);
                    champMenu.AddSubMenu(comboMenu);
                }
                var harassMenu = new Menu("Harass", "Harass");
                {
                    AddKeybind(harassMenu, "AutoQ", "Auto Q", "H", KeyBindType.Toggle, true);
                    AddBool(harassMenu, "AutoQ3", "-> Use Q3", false);
                    AddBool(harassMenu, "AutoQTower", "-> Under Tower", false);
                    AddBool(harassMenu, "Q", "Use Q");
                    AddBool(harassMenu, "Q3", "-> Use Q3");
                    AddBool(harassMenu, "QTower", "-> Under Tower");
                    AddBool(harassMenu, "QLastHit", "-> Last Hit (Q1/Q2)");
                    champMenu.AddSubMenu(harassMenu);
                }
                var clearMenu = new Menu("Clear", "Clear");
                {
                    AddBool(clearMenu, "Q", "Use Q");
                    AddBool(clearMenu, "Q3", "-> Use Q3");
                    AddBool(clearMenu, "E", "Use E");
                    AddBool(clearMenu, "ETower", "-> Under Tower", false);
                    AddBool(clearMenu, "Item", "Use Tiamat/Hydra Item");
                    champMenu.AddSubMenu(clearMenu);
                }
                var lastHitMenu = new Menu("Last Hit", "LastHit");
                {
                    AddBool(lastHitMenu, "Q", "Use Q");
                    AddBool(lastHitMenu, "Q3", "-> Use Q3");
                    AddBool(lastHitMenu, "E", "Use E");
                    AddBool(lastHitMenu, "ETower", "-> Under Tower", false);
                    champMenu.AddSubMenu(lastHitMenu);
                }
                var fleeMenu = new Menu("Flee", "Flee");
                {
                    AddBool(fleeMenu, "E", "Use E");
                    AddBool(fleeMenu, "EStackQ", "-> Stack Q While Dashing");
                    champMenu.AddSubMenu(fleeMenu);
                }
                var miscMenu = new Menu("Misc", "Misc");
                {
                    if (HeroManager.Enemies.Any())
                    {
                        new WindWall(miscMenu);
                    }
                    var killStealMenu = new Menu("Kill Steal", "KillSteal");
                    {
                        AddBool(killStealMenu, "Q", "Use Q");
                        AddBool(killStealMenu, "E", "Use E");
                        AddBool(killStealMenu, "R", "Use R");
                        AddBool(killStealMenu, "Ignite", "Use Ignite");
                        miscMenu.AddSubMenu(killStealMenu);
                    }
                    var interruptMenu = new Menu("Interrupt", "Interrupt");
                    {
                        AddBool(interruptMenu, "Q", "Use Q3");
                        foreach (var spell in
                            Interrupter.Spells.Where(
                                i => HeroManager.Enemies.Any(a => i.ChampionName == a.ChampionName)))
                        {
                            AddBool(
                                interruptMenu, spell.ChampionName + "_" + spell.Slot,
                                "-> Skill " + spell.Slot + " Of " + spell.ChampionName);
                        }
                        miscMenu.AddSubMenu(interruptMenu);
                    }
                    AddKeybind(miscMenu, "StackQ", "Auto Stack Q", "Z", KeyBindType.Toggle);
                    AddBool(miscMenu, "StackQDraw", "-> Draw Text");
                    champMenu.AddSubMenu(miscMenu);
                }
                var drawMenu = new Menu("Draw", "Draw");
                {
                    AddBool(drawMenu, "Q", "Q Range", false);
                    AddBool(drawMenu, "E", "E Range", false);
                    AddBool(drawMenu, "R", "R Range", false);
                    champMenu.AddSubMenu(drawMenu);
                }
                MainMenu.AddSubMenu(champMenu);
            }
            Game.OnUpdate += OnUpdate;
            Drawing.OnDraw += OnDraw;
            Interrupter.OnPossibleToInterrupt += OnPossibleToInterrupt;
        }

        private float GetQDelay
        {
            get { return 0.4f * (1 - Math.Min((Player.AttackSpeedMod - 1) * 0.58f, 0.66f)); }
        }

        private float GetQ2Delay
        {
            get { return 0.5f * (1 - Math.Min((Player.AttackSpeedMod - 1) * 0.58f, 0.66f)); }
        }

        private bool HaveQ3
        {
            get { return Player.HasBuff("YasuoQ3W"); }
        }

        private Obj_AI_Hero QCirTarget
        {
            get
            {
                var target = TargetSelector.GetTarget(
                    QCirWidth, TargetSelector.DamageType.Physical, true, null, Player.GetDashInfo().EndPos.To3D());
                return target != null && Player.Distance(target) < QCirWidth ? target : null;
            }
        }

        private void OnUpdate(EventArgs args)
        {
            if (!Equals(Q.Delay, GetQDelay))
            {
                Q.Delay = GetQDelay;
                Q2.Delay = GetQ2Delay;
            }
            if (Player.IsDead || MenuGUI.IsChatOpen || Player.IsRecalling())
            {
                return;
            }
            switch (Orbwalk.CurrentMode)
            {
                case Orbwalker.Mode.Combo:
                    Fight("Combo");
                    break;
                case Orbwalker.Mode.Harass:
                    Fight("Harass");
                    break;
                case Orbwalker.Mode.Clear:
                    Clear();
                    break;
                case Orbwalker.Mode.LastHit:
                    LastHit();
                    break;
                case Orbwalker.Mode.Flee:
                    Flee();
                    break;
            }
            AutoQ();
            KillSteal();
            StackQ();
        }

        private void OnDraw(EventArgs args)
        {
            if (Player.IsDead)
            {
                return;
            }
            if (GetValue<KeyBind>("Misc", "StackQ").Active && GetValue<bool>("Misc", "StackQDraw"))
            {
                var pos = Drawing.WorldToScreen(Player.Position);
                Drawing.DrawText(pos.X, pos.Y, Color.Orange, "Auto Stack Q");
            }
            if (GetValue<bool>("Draw", "Q") && Q.Level > 0)
            {
                Render.Circle.DrawCircle(
                    Player.Position, Player.IsDashing() ? QCirWidth : (!HaveQ3 ? Q : Q2).Range,
                    Q.IsReady() ? Color.Green : Color.Red);
            }
            if (GetValue<bool>("Draw", "E") && E.Level > 0)
            {
                Render.Circle.DrawCircle(Player.Position, E.Range, E.IsReady() ? Color.Green : Color.Red);
            }
            if (GetValue<bool>("Draw", "R") && R.Level > 0)
            {
                Render.Circle.DrawCircle(Player.Position, R.Range, R.IsReady() ? Color.Green : Color.Red);
            }
        }

        private void OnPossibleToInterrupt(Obj_AI_Hero unit, InterruptableSpell spell)
        {
            if (Player.IsDead || !GetValue<bool>("Interrupt", "Q") ||
                !GetValue<bool>("Interrupt", unit.ChampionName + "_" + spell.Slot) || !HaveQ3)
            {
                return;
            }
            if (E.IsReady() && Q.IsReady(50))
            {
                if (E.IsInRange(unit) && CanCastE(unit) && unit.Distance(PosAfterE(unit)) < QCirWidth &&
                    E.CastOnUnit(unit, PacketCast))
                {
                    return;
                }
                if (E.IsInRange(unit, E.Range + QCirWidth))
                {
                    var obj = GetNearObj(unit, true);
                    if (obj != null && E.CastOnUnit(obj, PacketCast))
                    {
                        return;
                    }
                }
            }
            if (!Q.IsReady())
            {
                return;
            }
            if (Player.IsDashing())
            {
                var pos = Player.GetDashInfo().EndPos;
                if (Player.Distance(pos) < 40 && unit.Distance(pos) <= QCirWidth)
                {
                    CastQCir(unit);
                }
            }
            else
            {
                Q2.CastIfHitchanceEquals(unit, HitChance.High, PacketCast);
            }
        }

        private void Fight(string mode)
        {
            if (mode == "Combo")
            {
                if (GetValue<bool>(mode, "R") && R.IsReady())
                {
                    var obj = (from enemy in HeroManager.Enemies.Where(i => R.IsInRange(i) && CanCastR(i))
                        let sub = enemy.GetEnemiesInRange(RWidth).Where(CanCastR).ToList()
                        where
                            (sub.Count > 1 && R.IsKillable(enemy)) ||
                            sub.Any(i => i.HealthPercent < GetValue<Slider>(mode, "RHpU").Value) ||
                            sub.Count >= GetValue<Slider>(mode, "RCountA").Value
                        select enemy).ToList();
                    if (obj.Any())
                    {
                        var target =
                            obj.Where(
                                i =>
                                    !GetValue<bool>(mode, "RDelay") ||
                                    TimeLeftR(i) <= (float) GetValue<Slider>(mode, "RDelayTime").Value / 1000)
                                .MaxOrDefault(i => i.GetEnemiesInRange(RWidth).Count(CanCastR));
                        if (target != null && R.CastOnUnit(target, PacketCast))
                        {
                            return;
                        }
                    }
                }
                if (GetValue<bool>(mode, "E") && E.IsReady())
                {
                    if (GetValue<bool>(mode, "EDmg") && GetValue<bool>(mode, "Q") && Q.IsReady(50))
                    {
                        var target = Q.GetTarget();
                        if (target != null)
                        {
                            var obj = GetNearObj(target, true);
                            if (obj != null && E.CastOnUnit(obj, PacketCast))
                            {
                                return;
                            }
                        }
                    }
                    if (GetValue<bool>(mode, "EGap"))
                    {
                        var target = Q.GetTarget() ?? Q2.GetTarget();
                        if (target != null)
                        {
                            var obj = GetNearObj(target);
                            if (obj != null &&
                                (obj.NetworkId != target.NetworkId
                                    ? Player.Distance(target) > GetValue<Slider>(mode, "EGapRange").Value
                                    : !Orbwalk.InAutoAttackRange(target)) &&
                                (!UnderTower(PosAfterE(obj)) || GetValue<bool>(mode, "EGapTower")) &&
                                E.CastOnUnit(obj, PacketCast))
                            {
                                return;
                            }
                        }
                    }
                }
            }
            if (GetValue<bool>(mode, "Q") && Q.IsReady())
            {
                if (mode == "Combo" ||
                    ((!HaveQ3 || GetValue<bool>(mode, "Q3")) &&
                     (!UnderTower(Player.ServerPosition) || GetValue<bool>(mode, "QTower"))))
                {
                    if (Player.IsDashing())
                    {
                        if (QCirTarget != null && CastQCir(QCirTarget))
                        {
                            return;
                        }
                    }
                    else
                    {
                        var target = (!HaveQ3 ? Q : Q2).GetTarget();
                        if (target != null)
                        {
                            if (HaveQ3 && mode == "Combo" && GetValue<bool>(mode, "E") && GetValue<bool>(mode, "EDmg") &&
                                E.IsReady() && GetNearObj(target, true) != null)
                            {
                                return;
                            }
                            if ((!HaveQ3 ? Q : Q2).Cast(target, PacketCast).IsCasted())
                            {
                                return;
                            }
                        }
                    }
                }
                if (mode == "Harass" && GetValue<bool>(mode, "QLastHit") && Q.GetTarget(100) == null && !HaveQ3 &&
                    !Player.IsDashing())
                {
                    var obj =
                        MinionManager.GetMinions(
                            E.Range, MinionTypes.All, MinionTeam.NotAlly, MinionOrderTypes.MaxHealth)
                            .Cast<Obj_AI_Minion>()
                            .FirstOrDefault(i => CanKill(i, GetQDmg(i)));
                    if (obj != null)
                    {
                        Q.CastIfHitchanceEquals(obj, HitChance.High, PacketCast);
                    }
                }
            }
        }

        private void Clear()
        {
            if (GetValue<bool>("Clear", "E") && E.IsReady())
            {
                var minionObj =
                    MinionManager.GetMinions(E.Range, MinionTypes.All, MinionTeam.NotAlly, MinionOrderTypes.MaxHealth)
                        .Cast<Obj_AI_Minion>()
                        .Where(i => CanCastE(i) && (!UnderTower(PosAfterE(i)) || GetValue<bool>("Clear", "ETower")))
                        .ToList();
                if (minionObj.Any())
                {
                    var obj = minionObj.FirstOrDefault(i => CanKill(i, GetEDmg(i)));
                    if (obj == null && GetValue<bool>("Clear", "Q") && Q.IsReady(50) &&
                        (!HaveQ3 || GetValue<bool>("Clear", "Q3")))
                    {
                        obj = (from i in minionObj
                            let sub =
                                MinionManager.GetMinions(PosAfterE(i), QCirWidth, MinionTypes.All, MinionTeam.NotAlly)
                            where
                                i.Team == GameObjectTeam.Neutral ||
                                (i.Distance(PosAfterE(i)) < QCirWidth && CanKill(i, GetEDmg(i) + GetQDmg(i))) ||
                                sub.Cast<Obj_AI_Minion>().Any(a => CanKill(a, GetQDmg(a))) || sub.Count > 1
                            select i).MaxOrDefault(
                                i =>
                                    MinionManager.GetMinions(
                                        PosAfterE(i), QCirWidth, MinionTypes.All, MinionTeam.NotAlly).Count);
                    }
                    if (obj != null && E.CastOnUnit(obj, PacketCast))
                    {
                        return;
                    }
                }
            }
            if (GetValue<bool>("Clear", "Q") && Q.IsReady() && (!HaveQ3 || GetValue<bool>("Clear", "Q3")))
            {
                if (Player.IsDashing())
                {
                    if (Player.Distance(Player.GetDashInfo().EndPos) < 100)
                    {
                        var minionObj = MinionManager.GetMinions(
                            Player.GetDashInfo().EndPos.To3D(), QCirWidth, MinionTypes.All, MinionTeam.NotAlly);
                        if (
                            (minionObj.Cast<Obj_AI_Minion>()
                                .Any(i => i.Team == GameObjectTeam.Neutral || CanKill(i, GetQDmg(i))) ||
                             minionObj.Count > 1) && CastQCir(minionObj.First()))
                        {
                            return;
                        }
                    }
                }
                else
                {
                    var minionObj = MinionManager.GetMinions(
                        (!HaveQ3 ? E : Q2).Range, MinionTypes.All, MinionTeam.NotAlly, MinionOrderTypes.MaxHealth);
                    if (minionObj.Any())
                    {
                        if (!HaveQ3)
                        {
                            var obj = minionObj.Cast<Obj_AI_Minion>().FirstOrDefault(i => CanKill(i, GetQDmg(i)));
                            if (obj != null && Q.CastIfHitchanceEquals(obj, HitChance.High, PacketCast))
                            {
                                return;
                            }
                        }
                        var pos =
                            (!HaveQ3 ? Q : Q2).GetLineFarmLocation(
                                minionObj.Select(i => (!HaveQ3 ? Q : Q2).GetPrediction(i))
                                    .Where(i => i.Hitchance >= HitChance.High)
                                    .Select(i => i.CastPosition.To2D())
                                    .ToList());
                        if (pos.MinionsHit > 0 && (!HaveQ3 ? Q : Q2).Cast(pos.Position, PacketCast))
                        {
                            return;
                        }
                    }
                }
            }
            if (GetValue<bool>("Clear", "Item") && (Hydra.IsReady() || Tiamat.IsReady()))
            {
                var minionObj = MinionManager.GetMinions(
                    (Hydra.IsReady() ? Hydra : Tiamat).Range, MinionTypes.All, MinionTeam.NotAlly);
                if (minionObj.Count > 2 ||
                    minionObj.Any(
                        i => i.MaxHealth >= 1200 && i.Distance(Player) < (Hydra.IsReady() ? Hydra : Tiamat).Range - 80))
                {
                    if (Tiamat.IsReady())
                    {
                        Tiamat.Cast();
                    }
                    if (Hydra.IsReady())
                    {
                        Hydra.Cast();
                    }
                }
            }
        }

        private void LastHit()
        {
            if (GetValue<bool>("LastHit", "Q") && Q.IsReady() && !Player.IsDashing() &&
                (!HaveQ3 || GetValue<bool>("LastHit", "Q3")))
            {
                var obj =
                    MinionManager.GetMinions(
                        (!HaveQ3 ? E : Q2).Range, MinionTypes.All, MinionTeam.NotAlly, MinionOrderTypes.MaxHealth)
                        .Cast<Obj_AI_Minion>()
                        .FirstOrDefault(i => CanKill(i, GetQDmg(i)));
                if (obj != null && (!HaveQ3 ? Q : Q2).CastIfHitchanceEquals(obj, HitChance.High, PacketCast))
                {
                    return;
                }
            }
            if (GetValue<bool>("LastHit", "E") && E.IsReady())
            {
                var obj =
                    MinionManager.GetMinions(E.Range, MinionTypes.All, MinionTeam.NotAlly, MinionOrderTypes.MaxHealth)
                        .Cast<Obj_AI_Minion>()
                        .Where(
                            i =>
                                CanCastE(i) &&
                                (!Orbwalk.InAutoAttackRange(i) || i.Health > Player.GetAutoAttackDamage(i, true)) &&
                                (!UnderTower(PosAfterE(i)) || GetValue<bool>("LastHit", "ETower")))
                        .FirstOrDefault(i => CanKill(i, GetEDmg(i)));
                if (obj != null)
                {
                    E.CastOnUnit(obj, PacketCast);
                }
            }
        }

        private void Flee()
        {
            if (!GetValue<bool>("Flee", "E"))
            {
                return;
            }
            if (GetValue<bool>("Flee", "EStackQ") && Q.IsReady() && !HaveQ3 && Player.IsDashing())
            {
                if (QCirTarget != null && CastQCir(QCirTarget))
                {
                    return;
                }
                if (Player.Distance(Player.GetDashInfo().EndPos) < 100)
                {
                    var minionObj = MinionManager.GetMinions(
                        Player.GetDashInfo().EndPos.To3D(), QCirWidth, MinionTypes.All, MinionTeam.NotAlly);
                    if (minionObj.Any() && CastQCir(minionObj.First()))
                    {
                        return;
                    }
                }
            }
            var obj = GetNearObj();
            if (obj == null || !E.IsReady())
            {
                return;
            }
            E.CastOnUnit(obj, PacketCast);
        }

        private void AutoQ()
        {
            if (!GetValue<KeyBind>("Harass", "AutoQ").Active || Player.IsDashing() ||
                (HaveQ3 && !GetValue<bool>("Harass", "AutoQ3")) ||
                (UnderTower(Player.ServerPosition) && !GetValue<bool>("Harass", "AutoQTower")))
            {
                return;
            }
            (!HaveQ3 ? Q : Q2).CastOnBestTarget(0, PacketCast);
        }

        private void KillSteal()
        {
            if (GetValue<bool>("KillSteal", "Ignite") && Ignite.IsReady())
            {
                var target = TargetSelector.GetTarget(600, TargetSelector.DamageType.True);
                if (target != null && CastIgnite(target))
                {
                    return;
                }
            }
            if (GetValue<bool>("KillSteal", "Q") && Q.IsReady())
            {
                if (Player.IsDashing())
                {
                    var target = QCirTarget;
                    if (target != null && CanKill(target, GetQDmg(target)) && CastQCir(target))
                    {
                        return;
                    }
                }
                else
                {
                    var target = (!HaveQ3 ? Q : Q2).GetTarget();
                    if (target != null && CanKill(target, GetQDmg(target)) &&
                        (!HaveQ3 ? Q : Q2).CastIfHitchanceEquals(target, HitChance.High, PacketCast))
                    {
                        return;
                    }
                }
            }
            if (GetValue<bool>("KillSteal", "E") && E.IsReady())
            {
                var target = E.GetTarget(0, HeroManager.Enemies.Where(i => !CanCastE(i)));
                if (target != null &&
                    (CanKill(target, GetEDmg(target)) ||
                     (GetValue<bool>("KillSteal", "Q") && Q.IsReady(50) &&
                      CanKill(target, GetEDmg(target) + GetQDmg(target)))) && E.CastOnUnit(target, PacketCast))
                {
                    return;
                }
            }
            if (GetValue<bool>("KillSteal", "R") && R.IsReady())
            {
                var target = R.GetTarget(0, HeroManager.Enemies.Where(i => !CanCastR(i)));
                if (target != null && R.IsKillable(target))
                {
                    R.CastOnUnit(target, PacketCast);
                }
            }
        }

        private void StackQ()
        {
            if (!GetValue<KeyBind>("Misc", "StackQ").Active || !Q.IsReady() || Player.IsDashing() || HaveQ3)
            {
                return;
            }
            var target = Q.GetTarget();
            if (target != null && !UnderTower(Player.ServerPosition))
            {
                Q.CastIfHitchanceEquals(target, HitChance.High, PacketCast);
            }
            else
            {
                var minionObj = MinionManager.GetMinions(
                    E.Range, MinionTypes.All, MinionTeam.NotAlly, MinionOrderTypes.MaxHealth);
                if (!minionObj.Any())
                {
                    return;
                }
                var obj = minionObj.Cast<Obj_AI_Minion>().FirstOrDefault(i => CanKill(i, GetQDmg(i))) ??
                          minionObj.MinOrDefault(i => i.Distance(Player));
                if (obj != null)
                {
                    Q.CastIfHitchanceEquals(obj, HitChance.High, PacketCast);
                }
            }
        }

        private bool CastQCir(Obj_AI_Base target)
        {
            return Q.Cast((!HaveQ3 ? Q : Q2).GetPrediction(target).CastPosition, PacketCast);
        }

        private bool CanCastE(Obj_AI_Base target)
        {
            return !target.HasBuff("YasuoDashWrapper");
        }

        private bool CanCastR(Obj_AI_Hero target)
        {
            return target.HasBuffOfType(BuffType.Knockup) || target.HasBuffOfType(BuffType.Knockback);
        }

        private float TimeLeftR(Obj_AI_Hero target)
        {
            var buff = target.Buffs.FirstOrDefault(i => i.Type == BuffType.Knockup || i.Type == BuffType.Knockback);
            return buff != null ? buff.EndTime - Game.Time : -1;
        }

        public double GetQDmg(Obj_AI_Base target)
        {
            var dmgItem = 0d;
            if (Sheen.IsOwned() && (Sheen.IsReady() || Player.HasBuff("Sheen")))
            {
                dmgItem = Player.BaseAttackDamage;
            }
            if (Trinity.IsOwned() && (Trinity.IsReady() || Player.HasBuff("Sheen")))
            {
                dmgItem = Player.BaseAttackDamage * 2;
            }
            var haveInfinity = LeagueSharp.Common.Data.ItemData.Infinity_Edge.GetItem().IsOwned();
            var maxCrit = Player.Crit >= 0.85f;
            var dmg = 20 * Q.Level + Player.TotalAttackDamage * (maxCrit ? (haveInfinity ? 1.875 : 1.5) : 1) + dmgItem;
            if (LeagueSharp.Common.Data.ItemData.Blade_of_the_Ruined_King.GetItem().IsOwned())
            {
                var dmgBotrk = Math.Max(0.08 * target.Health, 10);
                if (target.IsValid<Obj_AI_Minion>())
                {
                    dmgBotrk = Math.Min(dmgBotrk, 60);
                }
                dmg += dmgBotrk;
            }
            return Player.CalcDamage(target, Damage.DamageType.Physical, dmg) +
                   (Player.GetBuffCount("ItemStatikShankCharge") == 100
                       ? Player.CalcDamage(
                           target, Damage.DamageType.Magical, 100 * (maxCrit ? (haveInfinity ? 2.25 : 1.8) : 1))
                       : 0);
        }

        private double GetEDmg(Obj_AI_Base target)
        {
            return Player.CalcDamage(
                target, Damage.DamageType.Magical,
                (50 + 20 * E.Level) * (1 + Math.Max(0, Player.GetBuffCount("YasuoDashScalar") * 0.25)) +
                0.6 * Player.FlatMagicDamageMod);
        }

        private Obj_AI_Base GetNearObj(Obj_AI_Base target = null, bool inQCir = false)
        {
            var pos = target != null ? Prediction.GetPrediction(target, 0.25f).UnitPosition : Game.CursorPos;
            var obj = new List<Obj_AI_Base>();
            obj.AddRange(MinionManager.GetMinions(E.Range, MinionTypes.All, MinionTeam.NotAlly));
            obj.AddRange(HeroManager.Enemies.Where(i => i.IsValidTarget(E.Range)));
            return
                obj.Where(i => CanCastE(i) && pos.Distance(PosAfterE(i)) < (inQCir ? QCirWidth : Player.Distance(pos)))
                    .MinOrDefault(i => pos.Distance(PosAfterE(i)));
        }

        private Vector3 PosAfterE(Obj_AI_Base target)
        {
            return Player.ServerPosition.Extend(
                target.ServerPosition, Player.Distance(target) < 410 ? E.Range : Player.Distance(target) + 65);
        }

        private bool UnderTower(Vector3 pos)
        {
            return
                ObjectManager.Get<Obj_AI_Turret>()
                    .Any(i => i.IsEnemy && !i.IsDead && i.Distance(pos) < 850 + Player.BoundingRadius);
        }

        private class WindWall
        {
            private readonly List<Spells> _spells = new List<Spells>();
            private readonly List<Skills> _spellsDetected = new List<Skills>();

            public WindWall(Menu menu)
            {
                LoadWindWallData();
                var windMenu = new Menu("Wind Wall", "WindWall");
                {
                    AddBool(windMenu, "W", "Use W");
                    AddBool(windMenu, "BAttack", "-> Basic Attack");
                    AddSlider(windMenu, "BAttackHpU", "--> If Hp Under", 20);
                    AddBool(windMenu, "CAttack", "-> Crit Attack");
                    AddSlider(windMenu, "CAttackHpU", "--> If Hp Under", 30);
                    foreach (var obj in
                        HeroManager.Enemies.Where(i => _spells.Any(a => a.ChampName == i.ChampionName)))
                    {
                        windMenu.AddSubMenu(new Menu("-> " + obj.ChampionName, "WW_" + obj.ChampionName));
                    }
                    foreach (
                        var wwData in _spells.Where(i => HeroManager.Enemies.Any(a => a.ChampionName == i.ChampName)))
                    {
                        var name = wwData.SpellName == ""
                            ? HeroManager.Enemies.First(i => i.ChampionName == wwData.ChampName)
                                .GetSpell(wwData.Slot)
                                .SData.Name
                            : wwData.SpellName;
                        AddBool(
                            windMenu.SubMenu("WW_" + wwData.ChampName), name,
                            string.Format("{0} ({1})", name, wwData.Slot), false);
                    }
                }
                menu.AddSubMenu(windMenu);
                Game.OnUpdate += OnUpdateDetect;
                GameObject.OnCreate += OnCreateWwDetectSkillShot;
                GameObject.OnCreate += OnCreateWwDetectTarget;
                GameObject.OnDelete += OnDeleteWwDetect;
                Obj_AI_Base.OnProcessSpellCast += OnCastWwDetect;
            }

            private void OnUpdateDetect(EventArgs args)
            {
                _spellsDetected.RemoveAll(i => i.Deactivate);
                if (!W.IsReady(5000) && _spellsDetected.Any())
                {
                    _spellsDetected.Clear();
                }
                if (Player.IsDead || !W.IsReady())
                {
                    return;
                }
                foreach (var spell in
                    _spellsDetected.Where(
                        i => i.Spell.Type == SpellType.Target || (i.Obj.IsVisible && W.IsInRange(i.Obj))))
                {
                    var isHit = false;
                    switch (spell.Spell.Type)
                    {
                        case SpellType.Target:
                        case SpellType.TargetGlobal:
                            if (spell.Spell.Type == SpellType.TargetGlobal)
                            {
                                spell.Start = spell.Obj.Position.To2D();
                            }
                            isHit = true;
                            break;
                        case SpellType.AoE:
                        case SpellType.LineAoE:
                            isHit = WillHit(spell.Start, spell.End, spell.Spell) ||
                                    (spell.Spell.Type == SpellType.LineAoE &&
                                     WillHit(spell.Start, spell.Start * 2 - spell.End, spell.Spell));
                            break;
                        case SpellType.Line:
                        case SpellType.LinePoint:
                        case SpellType.Global:
                            var posCur = spell.Obj.Position.To2D();
                            var posEnd = spell.End;
                            isHit = WillHit(posCur, posEnd, spell.Spell);
                            if (spell.Spell.SpellName == "DravenR" && !isHit)
                            {
                                posEnd = posCur +
                                         spell.Direction * spell.Spell.Speed *
                                         (0.5f + spell.Spell.Radius * 2 / Player.MoveSpeed);
                                posEnd = posCur.Extend(posEnd, spell.Spell.Range);
                                if (WillHit(posCur, posEnd, spell.Spell))
                                {
                                    W.Cast(posCur, PacketCast);
                                }
                            }
                            break;
                    }
                    if (isHit)
                    {
                        W.Cast(spell.Start, PacketCast);
                    }
                }
            }

            private void OnCreateWwDetectSkillShot(GameObject sender, EventArgs args)
            {
                if (Player.IsDead || !sender.IsValid<Obj_SpellMissile>() || !GetValue<bool>("WindWall", "W"))
                {
                    return;
                }
                var missile = (Obj_SpellMissile) sender;
                if (!missile.SpellCaster.IsValid<Obj_AI_Hero>() || missile.SpellCaster.IsAlly)
                {
                    return;
                }
                var caster = (Obj_AI_Hero) missile.SpellCaster;
                var spellData =
                    _spells.FirstOrDefault(
                        i =>
                            i.FoW && i.Type != SpellType.Target && i.Type != SpellType.TargetGlobal &&
                            (i.SpellName == ""
                                ? caster.GetSpellSlot(missile.SData.Name) == i.Slot
                                : i.SpellName == missile.SData.Name) &&
                            GetItem("WW_" + i.ChampName, missile.SData.Name) != null &&
                            GetValue<bool>("WW_" + i.ChampName, missile.SData.Name));
                if (spellData == null)
                {
                    return;
                }
                var posPlayer = Player.ServerPosition.To2D();
                var posCur = missile.Position.To2D();
                var posStart = missile.StartPosition.To2D();
                var posEnd = missile.EndPosition.To2D();
                if (spellData.Type == SpellType.AoE || spellData.Type == SpellType.LinePoint)
                {
                    if (spellData.Range > 0 && posStart.Distance(posEnd) > spellData.Range)
                    {
                        posEnd = posStart.Extend(posEnd, spellData.Range);
                    }
                }
                else if (spellData.Type != SpellType.Target && spellData.Type != SpellType.TargetGlobal)
                {
                    posEnd = posStart.Extend(posEnd, spellData.Range);
                }
                var castTime = Utils.TickCount - Game.Ping / 2 -
                               (int) (1000 * posCur.Distance(posStart) / spellData.Speed);
                if (posPlayer.Distance(posStart) > (spellData.Range + spellData.Radius + 1000) * 1.5)
                {
                    return;
                }
                _spellsDetected.Add(new Skills(spellData, castTime, posStart, posEnd, missile));
            }

            private void OnCreateWwDetectTarget(GameObject sender, EventArgs args)
            {
                if (Player.IsDead || !sender.IsValid<MissileClient>() || !GetValue<bool>("WindWall", "W"))
                {
                    return;
                }
                var missile = (MissileClient) sender;
                if (!missile.SpellCaster.IsValid<Obj_AI_Hero>() || missile.SpellCaster.IsAlly)
                {
                    return;
                }
                var caster = (Obj_AI_Hero) missile.SpellCaster;
                var spellData =
                    _spells.FirstOrDefault(
                        i =>
                            i.FoW && (i.Type == SpellType.Target || i.Type == SpellType.TargetGlobal) &&
                            (i.SpellName == ""
                                ? caster.GetSpellSlot(missile.SData.Name) == i.Slot
                                : i.SpellName == missile.SData.Name) &&
                            GetItem("WW_" + i.ChampName, missile.SData.Name) != null &&
                            GetValue<bool>("WW_" + i.ChampName, missile.SData.Name));
                if (spellData == null && missile.SData.IsAutoAttack() &&
                    (!missile.SData.Name.ToLower().Contains("critattack")
                        ? GetValue<bool>("WindWall", "BAttack") &&
                          Player.HealthPercent < GetValue<Slider>("WindWall", "BAttackHpU").Value
                        : GetValue<bool>("WindWall", "CAttack") &&
                          Player.HealthPercent < GetValue<Slider>("WindWall", "CAttackHpU").Value) && W.IsReady())
                {
                    spellData = new Spells(
                        caster.ChampionName, SpellSlot.Unknown, SpellType.Target, 0, 0, 0, missile.SData.Name);
                }
                if (spellData == null || !missile.Target.IsMe)
                {
                    return;
                }
                _spellsDetected.Add(
                    new Skills(spellData, 0, missile.StartPosition.To2D(), missile.EndPosition.To2D(), missile));
            }

            private void OnDeleteWwDetect(GameObject sender, EventArgs args)
            {
                if (!_spellsDetected.Any())
                {
                    return;
                }
                if (sender.IsValid<Obj_SpellMissile>())
                {
                    var missile = (Obj_SpellMissile) sender;
                    if (missile.SpellCaster.IsValid<Obj_AI_Hero>() && missile.SpellCaster.IsEnemy)
                    {
                        _spellsDetected.RemoveAll(
                            i =>
                                ((Obj_SpellMissile) i.Obj).SData.Name == missile.SData.Name &&
                                i.Obj.NetworkId == missile.NetworkId);
                    }
                }
                if (sender.IsValid<MissileClient>())
                {
                    var missile = (MissileClient) sender;
                    if (missile.SpellCaster.IsValid<Obj_AI_Hero>() && missile.SpellCaster.IsEnemy)
                    {
                        _spellsDetected.RemoveAll(
                            i =>
                                ((MissileClient) i.Obj).SData.Name == missile.SData.Name &&
                                i.Obj.NetworkId == missile.NetworkId);
                    }
                }
            }

            private void OnCastWwDetect(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
            {
                if (Player.IsDead || !sender.IsValid<Obj_AI_Hero>() || !sender.IsEnemy ||
                    !GetValue<bool>("WindWall", "W") || !W.IsReady())
                {
                    return;
                }
                var target = (Obj_AI_Hero) sender;
                var spellData =
                    _spells.FirstOrDefault(
                        i =>
                            !i.FoW &&
                            (i.SpellName == ""
                                ? target.GetSpellSlot(args.SData.Name) == i.Slot
                                : i.SpellName == args.SData.Name) &&
                            GetItem("WW_" + i.ChampName, args.SData.Name) != null &&
                            GetValue<bool>("WW_" + i.ChampName, args.SData.Name));
                if (spellData == null)
                {
                    return;
                }
                if (WillHit(args.Start.To2D(), args.End.To2D(), spellData))
                {
                    W.Cast(args.Start, PacketCast);
                }
            }

            private bool WillHit(Vector2 pos1, Vector2 pos2, Spells spellData)
            {
                if (spellData.Type == SpellType.AoE)
                {
                    return Player.Distance(pos2) < spellData.Radius + Player.BoundingRadius;
                }
                var point = Player.ServerPosition.To2D().ProjectOn(pos1, pos2).SegmentPoint;
                return Player.Distance(point) < spellData.Radius + Player.BoundingRadius &&
                       pos1.Distance(point) < pos1.Distance(pos2) && pos2.Distance(point) < pos1.Distance(pos2);
            }

            private void LoadWindWallData()
            {
                _spells.Add(new Spells("Aatrox", SpellSlot.E, SpellType.Line, 1075, 40, 1250, "aatroxeconemissile"));
                _spells.Add(new Spells("Ahri", SpellSlot.Q, SpellType.Line, 900, 100, 2500, "AhriOrbMissile"));
                _spells.Add(new Spells("Ahri", SpellSlot.Q, SpellType.LinePoint, 0, 100, 60, "AhriOrbReturn"));
                _spells.Add(new Spells("Ahri", SpellSlot.W, SpellType.Target, 0, 0, 0, "AhriFoxFireMissileTwo"));
                _spells.Add(new Spells("Ahri", SpellSlot.E, SpellType.Line, 1000, 60, 1550, "AhriSeduceMissile"));
                _spells.Add(new Spells("Ahri", SpellSlot.R, SpellType.Target, 0, 0, 0, "AhriTumbleMissile"));
                _spells.Add(new Spells("Akali", SpellSlot.Q));
                _spells.Add(new Spells("Amumu", SpellSlot.Q, SpellType.Line, 1100, 80, 2000, "SadMummyBandageToss"));
                _spells.Add(new Spells("Anivia", SpellSlot.Q, SpellType.Line, 1100, 110, 850, "FlashFrostSpell"));
                _spells.Add(new Spells("Anivia", SpellSlot.E));
                _spells.Add(new Spells("Annie", SpellSlot.Q));
                _spells.Add(new Spells("Ashe", SpellSlot.W, SpellType.Line, 1200, 20, 1500, "VolleyAttack"));
                _spells.Add(
                    new Spells("Ashe", SpellSlot.R, SpellType.Global, 25000, 130, 1600, "EnchantedCrystalArrow"));
                _spells.Add(new Spells("Bard", SpellSlot.Q, SpellType.Line, 950, 60, 1500, "bardqmissile"));
                _spells.Add(new Spells("Blitzcrank", SpellSlot.Q, SpellType.Line, 1050, 70, 1800, "RocketGrabMissile"));
                _spells.Add(new Spells("Brand", SpellSlot.Q, SpellType.Line, 1100, 60, 1600, "BrandBlazeMissile"));
                _spells.Add(new Spells("Brand", SpellSlot.E, SpellType.Target, 0, 0, 0, "BrandConflagrationMissile"));
                _spells.Add(new Spells("Brand", SpellSlot.R));
                _spells.Add(new Spells("Brand", SpellSlot.R, SpellType.Target, 0, 0, 0, "BrandWildfireMissile"));
                _spells.Add(new Spells("Braum", SpellSlot.Q, SpellType.Line, 1050, 60, 1700, "BraumQMissile"));
                _spells.Add(new Spells("Braum", SpellSlot.R, SpellType.Line, 1200, 115, 1400, "braumrmissile"));
                _spells.Add(
                    new Spells("Caitlyn", SpellSlot.Q, SpellType.Line, 1250, 90, 2200, "CaitlynPiltoverPeacemaker"));
                _spells.Add(
                    new Spells("Caitlyn", SpellSlot.E, SpellType.Line, 1000, 80, 2000, "CaitlynEntrapmentMissile"));
                _spells.Add(
                    new Spells("Caitlyn", SpellSlot.R, SpellType.TargetGlobal, 0, 0, 0, "CaitlynAceintheHoleMissile"));
                _spells.Add(new Spells("Cassiopeia", SpellSlot.W, SpellType.AoE, 850, 175, 2500, "CassiopeiaMiasma"));
                _spells.Add(new Spells("Cassiopeia", SpellSlot.E));
                _spells.Add(new Spells("Corki", SpellSlot.Q, SpellType.AoE, 825, 210, 1000, "PhosphorusBombMissile"));
                _spells.Add(new Spells("Corki", SpellSlot.R, SpellType.Line, 1300, 40, 2000, "MissileBarrageMissile"));
                _spells.Add(new Spells("Corki", SpellSlot.R, SpellType.Line, 1500, 40, 2000, "MissileBarrageMissile2"));
                _spells.Add(new Spells("Diana", SpellSlot.Q, SpellType.AoE, 900, 185, 0, "DianaArc", false));
                _spells.Add(
                    new Spells("DrMundo", SpellSlot.Q, SpellType.Line, 1050, 60, 2000, "InfectedCleaverMissile"));
                _spells.Add(
                    new Spells("Draven", SpellSlot.E, SpellType.Line, 1100, 130, 1400, "DravenDoubleShotMissile"));
                _spells.Add(new Spells("Draven", SpellSlot.R, SpellType.Global, 25000, 160, 2000, "DravenR"));
                _spells.Add(new Spells("Ekko", SpellSlot.Q, SpellType.Line, 950, 60, 1650, "ekkoqmis"));
                _spells.Add(new Spells("Elise", SpellSlot.Q, SpellType.Target, 0, 0, 0, "EliseHumanQ"));
                _spells.Add(new Spells("Elise", SpellSlot.E, SpellType.Line, 1100, 55, 1600, "EliseHumanE"));
                _spells.Add(new Spells("Evelynn", SpellSlot.Q, SpellType.Line, 650, 80, 2200, "HateSpikeLineMissile"));
                _spells.Add(
                    new Spells("Ezreal", SpellSlot.Q, SpellType.Line, 1200, 60, 2000, "EzrealMysticShotMissile"));
                _spells.Add(
                    new Spells("Ezreal", SpellSlot.W, SpellType.Line, 1050, 80, 1600, "EzrealEssenceFluxMissile"));
                _spells.Add(new Spells("Ezreal", SpellSlot.E, SpellType.Target, 0, 0, 0, "EzrealArcaneShiftMissile"));
                _spells.Add(
                    new Spells("Ezreal", SpellSlot.R, SpellType.Global, 25000, 160, 2000, "EzrealTrueshotBarrage"));
                _spells.Add(new Spells("FiddleSticks", SpellSlot.E));
                _spells.Add(
                    new Spells("FiddleSticks", SpellSlot.E, SpellType.Target, 0, 0, 0, "FiddleSticksDarkWindMissile"));
                _spells.Add(
                    new Spells("Fizz", SpellSlot.R, SpellType.LinePoint, 1175, 120, 1350, "FizzMarinerDoomMissile"));
                _spells.Add(new Spells("Galio", SpellSlot.Q, SpellType.AoE, 900, 200, 1300, "GalioResoluteSmite"));
                _spells.Add(
                    new Spells("Galio", SpellSlot.E, SpellType.Line, 1100, 160, 1300, "galiorighteousgustmissile"));
                _spells.Add(new Spells("Gangplank", SpellSlot.Q));
                _spells.Add(new Spells("Gnar", SpellSlot.Q, SpellType.Line, 1125, 55, 2500, "gnarqmissile"));
                _spells.Add(new Spells("Gnar", SpellSlot.Q, SpellType.Line, 2500, 75, 60, "GnarQMissileReturn"));
                _spells.Add(new Spells("Gnar", SpellSlot.Q, SpellType.Line, 1150, 90, 2100, "GnarBigQMissile"));
                _spells.Add(new Spells("Gragas", SpellSlot.Q, SpellType.AoE, 1100, 275, 1300, "GragasQMissile"));
                _spells.Add(new Spells("Gragas", SpellSlot.R, SpellType.AoE, 1050, 375, 1800, "GragasRBoom"));
                _spells.Add(
                    new Spells("Graves", SpellSlot.Q, SpellType.Line, 1000, 50, 2000, "GravesClusterShotAttack"));
                _spells.Add(new Spells("Graves", SpellSlot.W, SpellType.AoE, 900, 250, 1500, "gravessmokegrenadeboom"));
                _spells.Add(new Spells("Graves", SpellSlot.R, SpellType.Line, 1000, 100, 2100, "GravesChargeShotShot"));
                _spells.Add(
                    new Spells("Heimerdinger", SpellSlot.W, SpellType.Line, 1250, 40, 750, "HeimerdingerWAttack2"));
                _spells.Add(
                    new Spells("Heimerdinger", SpellSlot.W, SpellType.Line, 1250, 60, 750, "HeimerdingerWAttack2Ult"));
                _spells.Add(
                    new Spells("Heimerdinger", SpellSlot.E, SpellType.AoE, 925, 100, 1200, "heimerdingerespell"));
                _spells.Add(
                    new Spells("Heimerdinger", SpellSlot.E, SpellType.AoE, 925, 210, 1400, "heimerdingerespell_ult"));
                _spells.Add(
                    new Spells("Heimerdinger", SpellSlot.E, SpellType.AoE, 300, 210, 1400, "heimerdingerespell_ult2"));
                _spells.Add(
                    new Spells("Heimerdinger", SpellSlot.E, SpellType.AoE, 300, 210, 1200, "heimerdingerespell_ult3"));
                _spells.Add(
                    new Spells("Irelia", SpellSlot.R, SpellType.Line, 1200, 65, 1600, "IreliaTranscendentBlades"));
                _spells.Add(new Spells("Janna", SpellSlot.Q, SpellType.Line, 1700, 120, 900, "HowlingGaleSpell"));
                _spells.Add(new Spells("Janna", SpellSlot.W));
                _spells.Add(new Spells("Jayce", SpellSlot.Q, SpellType.Line, 1300, 70, 1450, "JayceShockBlastMis"));
                _spells.Add(new Spells("Jayce", SpellSlot.Q, SpellType.Line, 2000, 70, 2350, "JayceShockBlastWallMis"));
                _spells.Add(new Spells("Jinx", SpellSlot.W, SpellType.Line, 1450, 60, 3300, "JinxWMissile"));
                _spells.Add(new Spells("Jinx", SpellSlot.E, SpellType.AoE, 900, 120, 1100, "JinxEHit"));
                _spells.Add(new Spells("Jinx", SpellSlot.R, SpellType.Global, 25000, 140, 1700, "JinxR"));
                _spells.Add(
                    new Spells("Kalista", SpellSlot.Q, SpellType.Line, 1175, 40, 2400, "kalistamysticshotmistrue"));
                _spells.Add(new Spells("Karma", SpellSlot.Q, SpellType.Line, 1050, 60, 1700, "KarmaQMissile"));
                _spells.Add(new Spells("Karma", SpellSlot.Q, SpellType.Line, 950, 80, 1700, "KarmaQMissileMantra"));
                _spells.Add(new Spells("Kassadin", SpellSlot.Q));
                _spells.Add(new Spells("Katarina", SpellSlot.Q));
                _spells.Add(new Spells("Katarina", SpellSlot.Q, SpellType.Target, 0, 0, 0, "KatarinaQMis"));
                _spells.Add(new Spells("Katarina", SpellSlot.R, SpellType.AoE, 550, 550, 0, "KatarinaR", false));
                _spells.Add(new Spells("Kayle", SpellSlot.Q));
                _spells.Add(
                    new Spells("Kennen", SpellSlot.Q, SpellType.Line, 1050, 50, 1700, "KennenShurikenHurlMissile1"));
                _spells.Add(new Spells("Khazix", SpellSlot.W, SpellType.Line, 1025, 70, 1700, "KhazixWMissile"));
                _spells.Add(new Spells("KogMaw", SpellSlot.Q, SpellType.Line, 1000, 70, 1650, "KogMawQMis"));
                _spells.Add(new Spells("KogMaw", SpellSlot.E, SpellType.Line, 1250, 120, 1400, "KogMawVoidOozeMissile"));
                _spells.Add(new Spells("Leblanc", SpellSlot.Q));
                _spells.Add(new Spells("Leblanc", SpellSlot.E, SpellType.Line, 950, 70, 1600, "LeblancSoulShackle"));
                _spells.Add(new Spells("Leblanc", SpellSlot.R, SpellType.Target, 0, 0, 0, "LeblancChaosOrbM"));
                _spells.Add(new Spells("Leblanc", SpellSlot.R, SpellType.Line, 950, 70, 1600, "LeblancSoulShackleM"));
                _spells.Add(new Spells("LeeSin", SpellSlot.Q, SpellType.Line, 1100, 60, 1800, "BlindMonkQOne"));
                _spells.Add(new Spells("Leona", SpellSlot.E, SpellType.Line, 905, 70, 2000, "LeonaZenithBladeMissile"));
                _spells.Add(new Spells("Lissandra", SpellSlot.Q, SpellType.Line, 700, 75, 2200, "LissandraQMissile"));
                _spells.Add(
                    new Spells("Lissandra", SpellSlot.Q, SpellType.LinePoint, 1000, 90, 2200, "lissandraqshards"));
                _spells.Add(new Spells("Lissandra", SpellSlot.E, SpellType.Line, 1025, 125, 800, "LissandraEMissile"));
                _spells.Add(new Spells("Lucian", SpellSlot.W, SpellType.Line, 1000, 55, 1600, "lucianwmissile"));
                _spells.Add(new Spells("Lucian", SpellSlot.R, SpellType.Line, 1400, 110, 2800, "lucianrmissile"));
                //Test
                _spells.Add(new Spells("Lucian", SpellSlot.R, SpellType.Line, 1400, 110, 2800, "lucianrmissileoffhand"));
                //Test
                _spells.Add(new Spells("Lulu", SpellSlot.Q, SpellType.Line, 950, 60, 1450, "LuluQMissile"));
                _spells.Add(new Spells("Lulu", SpellSlot.Q, SpellType.Line, 950, 60, 1450, "LuluQMissileTwo"));
                _spells.Add(new Spells("Lulu", SpellSlot.W));
                _spells.Add(new Spells("Lux", SpellSlot.Q, SpellType.Line, 1300, 70, 1200, "LuxLightBindingMis"));
                _spells.Add(new Spells("Lux", SpellSlot.E, SpellType.AoE, 1100, 275, 1300, "LuxLightStrikeKugel"));
                _spells.Add(new Spells("Malphite", SpellSlot.Q));
                _spells.Add(new Spells("MissFortune", SpellSlot.Q));
                _spells.Add(new Spells("MissFortune", SpellSlot.Q, SpellType.Target, 0, 0, 0, "MissFortuneRShotExtra"));
                _spells.Add(
                    new Spells("MissFortune", SpellSlot.R, SpellType.Line, 1500, 20, 2000, "MissFortuneBullets"));
                //Test
                _spells.Add(
                    new Spells("MissFortune", SpellSlot.R, SpellType.Line, 1500, 20, 2000, "MissFortuneBulletsClone"));
                //Test
                _spells.Add(new Spells("Morgana", SpellSlot.Q, SpellType.Line, 1300, 70, 1200, "DarkBindingMissile"));
                _spells.Add(new Spells("Nami", SpellSlot.Q, SpellType.AoE, 850, 225, 2500, "namiqmissile"));
                _spells.Add(new Spells("Nami", SpellSlot.W, SpellType.Target, 0, 0, 0, "NamiWEnemy"));
                _spells.Add(new Spells("Nami", SpellSlot.W, SpellType.Target, 0, 0, 0, "NamiWMissileEnemy"));
                _spells.Add(new Spells("Nami", SpellSlot.R, SpellType.Line, 2750, 250, 850, "NamiRMissile"));
                _spells.Add(
                    new Spells("Nautilus", SpellSlot.Q, SpellType.Line, 1100, 90, 2000, "NautilusAnchorDragMissile"));
                _spells.Add(
                    new Spells("Nautilus", SpellSlot.E, SpellType.AoE, 600, 600, 450, "NautilusSplashZoneSplash"));
                _spells.Add(new Spells("Nidalee", SpellSlot.Q, SpellType.Line, 1500, 40, 1300, "JavelinToss"));
                _spells.Add(new Spells("Nocturne", SpellSlot.Q, SpellType.Line, 1200, 60, 1400, "NocturneDuskbringer"));
                _spells.Add(new Spells("Nunu", SpellSlot.E));
                _spells.Add(new Spells("Olaf", SpellSlot.Q, SpellType.LinePoint, 1000, 105, 1600, "olafaxethrow"));
                _spells.Add(new Spells("Orianna", SpellSlot.Q, SpellType.LinePoint, 0, 80, 1200, "orianaizuna")); //Test
                _spells.Add(new Spells("Orianna", SpellSlot.E, SpellType.LinePoint, 0, 80, 1850, "orianaredact"));
                //Test
                _spells.Add(new Spells("Pantheon", SpellSlot.Q));
                _spells.Add(new Spells("Quinn", SpellSlot.Q, SpellType.Line, 1050, 80, 1550, "QuinnQMissile"));
                _spells.Add(new Spells("RekSai", SpellSlot.Q, SpellType.Line, 1500, 65, 1950, "RekSaiQBurrowedMis"));
                _spells.Add(new Spells("Rengar", SpellSlot.E, SpellType.Line, 1000, 70, 1500, "RengarEFinal"));
                _spells.Add(new Spells("Riven", SpellSlot.R, SpellType.Line, 1075, 100, 1600, "RivenLightsaberMissile"));
                _spells.Add(
                    new Spells("Riven", SpellSlot.R, SpellType.Line, 1075, 100, 1600, "RivenLightsaberMissileSide"));
                _spells.Add(
                    new Spells("Rumble", SpellSlot.E, SpellType.Line, 950, 60, 2000, "rumblegrenademissilemechbase"));
                _spells.Add(new Spells("Ryze", SpellSlot.Q, SpellType.Line, 900, 50, 1700, "RyzeQ"));
                _spells.Add(new Spells("Ryze", SpellSlot.Q, SpellType.Line, 900, 50, 1700, "ryzerq"));
                _spells.Add(new Spells("Ryze", SpellSlot.E));
                _spells.Add(new Spells("Ryze", SpellSlot.E, SpellType.Target, 0, 0, 0, "spellfluxmissile"));
                _spells.Add(new Spells("Sejuani", SpellSlot.R, SpellType.Line, 1100, 110, 1600, "sejuaniglacialprison"));
                _spells.Add(new Spells("Shaco", SpellSlot.E));
                _spells.Add(new Spells("Shen", SpellSlot.Q));
                _spells.Add(new Spells("Shyvana", SpellSlot.E, SpellType.Line, 950, 60, 1700, "ShyvanaFireballMissile"));
                _spells.Add(
                    new Spells("Shyvana", SpellSlot.E, SpellType.Line, 750, 70, 2000, "ShyvanaFireballDragonFxMissile"));
                _spells.Add(new Spells("Sion", SpellSlot.E, SpellType.Line, 800, 80, 1800, "SionEMissile"));
                _spells.Add(new Spells("Sivir", SpellSlot.Q, SpellType.Line, 1250, 90, 1350, "SivirQMissile"));
                _spells.Add(new Spells("Sivir", SpellSlot.Q, SpellType.LinePoint, 0, 100, 1350, "SivirQMissileReturn"));
                _spells.Add(
                    new Spells("Skarner", SpellSlot.E, SpellType.Line, 1000, 70, 1500, "SkarnerFractureMissile"));
                _spells.Add(new Spells("Sona", SpellSlot.Q, SpellType.Target, 0, 0, 0, "sonaqmissile"));
                _spells.Add(new Spells("Sona", SpellSlot.R, SpellType.Line, 1000, 140, 2400, "SonaR"));
                _spells.Add(new Spells("Soraka", SpellSlot.Q, SpellType.AoE, 950, 210, 1100, "SorakaQMissile"));
                _spells.Add(new Spells("Swain", SpellSlot.E));
                _spells.Add(new Spells("Syndra", SpellSlot.R));
                _spells.Add(new Spells("Talon", SpellSlot.W, SpellType.Line, 700, 75, 2300, "talonrakemissileone"));
                _spells.Add(
                    new Spells("Talon", SpellSlot.R, SpellType.Line, 575, 125, 2300, "talonshadowassaultmisone"));
                _spells.Add(new Spells("Taric", SpellSlot.E));
                _spells.Add(new Spells("Teemo", SpellSlot.Q));
                _spells.Add(new Spells("Thresh", SpellSlot.Q, SpellType.Line, 1100, 70, 1900, "ThreshQMissile"));
                _spells.Add(new Spells("Thresh", SpellSlot.E, SpellType.LineAoE, 540, 110, 2000, "ThreshEMissile1"));
                _spells.Add(new Spells("Tristana", SpellSlot.E));
                _spells.Add(new Spells("Tristana", SpellSlot.R, SpellType.AoE, 650, 210, 2000, "TristanaR"));
                _spells.Add(new Spells("TwistedFate", SpellSlot.Q, SpellType.Line, 1450, 40, 1000, "SealFateMissile"));
                _spells.Add(new Spells("TwistedFate", SpellSlot.W, SpellType.Target, 0, 0, 0, "bluecardattack"));
                _spells.Add(new Spells("TwistedFate", SpellSlot.W, SpellType.Target, 0, 0, 0, "GoldCardAttack"));
                _spells.Add(new Spells("TwistedFate", SpellSlot.W, SpellType.Target, 0, 0, 0, "RedCardAttack"));
                _spells.Add(new Spells("Twitch", SpellSlot.W, SpellType.AoE, 950, 210, 1400, "TwitchVenomCaskMissile"));
                _spells.Add(
                    new Spells("Urgot", SpellSlot.Q, SpellType.Line, 1000, 60, 1600, "UrgotHeatseekingLineMissile"));
                _spells.Add(new Spells("Urgot", SpellSlot.Q, SpellType.Target, 0, 0, 0, "UrgotHeatseekingHomeMissile"));
                _spells.Add(new Spells("Urgot", SpellSlot.E, SpellType.AoE, 900, 210, 1500, "UrgotPlasmaGrenadeBoom"));
                _spells.Add(new Spells("Varus", SpellSlot.Q, SpellType.LinePoint, 1550, 70, 1900, "VarusQMissile"));
                _spells.Add(new Spells("Varus", SpellSlot.E, SpellType.AoE, 925, 210, 1500, "VarusEMissile"));
                _spells.Add(new Spells("Varus", SpellSlot.R, SpellType.Line, 1200, 120, 1950, "VarusRMissile"));
                _spells.Add(new Spells("Vayne", SpellSlot.E));
                _spells.Add(new Spells("Veigar", SpellSlot.Q, SpellType.Line, 950, 70, 2200, "VeigarBalefulStrikeMis"));
                _spells.Add(new Spells("Veigar", SpellSlot.R));
                _spells.Add(new Spells("Velkoz", SpellSlot.Q, SpellType.Line, 1100, 50, 1300, "VelkozQMissile"));
                _spells.Add(new Spells("Velkoz", SpellSlot.Q, SpellType.Line, 900, 45, 2100, "VelkozQMissileSplit"));
                _spells.Add(new Spells("Velkoz", SpellSlot.W, SpellType.Line, 1100, 87.5f, 1700, "VelkozWMissile"));
                _spells.Add(new Spells("Viktor", SpellSlot.Q));
                _spells.Add(new Spells("Viktor", SpellSlot.E, SpellType.Line, 700, 80, 780, "ViktorDeathRayMissile"));
                _spells.Add(new Spells("Viktor", SpellSlot.E, SpellType.Line, 700, 80, 780, "viktoreaugmissile"));
                _spells.Add(new Spells("Vladimir", SpellSlot.E, SpellType.Target, 0, 0, 0, "vladimirtidesofbloodnuke"));
                _spells.Add(new Spells("Xerath", SpellSlot.E, SpellType.Line, 1150, 60, 1400, "XerathMageSpearMissile"));
                _spells.Add(new Spells("Yasuo", SpellSlot.Q, SpellType.Line, 1100, 90, 1500, "yasuoq3mis"));
                _spells.Add(new Spells("Zed", SpellSlot.Q, SpellType.Line, 925, 50, 1700, "zedshurikenmisone"));
                _spells.Add(new Spells("Zed", SpellSlot.Q, SpellType.Line, 925, 50, 1700, "zedshurikenmistwo"));
                _spells.Add(new Spells("Ziggs", SpellSlot.Q, SpellType.AoE, 850, 225, 1700, "ZiggsQSpell"));
                _spells.Add(new Spells("Ziggs", SpellSlot.Q, SpellType.AoE, 350, 225, 1600, "ZiggsQSpell2"));
                _spells.Add(new Spells("Ziggs", SpellSlot.Q, SpellType.AoE, 200, 225, 1600, "ZiggsQSpell3"));
                _spells.Add(new Spells("Ziggs", SpellSlot.W, SpellType.AoE, 1000, 275, 1750, "ZiggsW"));
                _spells.Add(new Spells("Ziggs", SpellSlot.E, SpellType.AoE, 900, 225, 1550, "ziggse2"));
                _spells.Add(new Spells("Zilean", SpellSlot.Q, SpellType.AoE, 900, 210, 2000, "ZileanQMissile"));
                _spells.Add(new Spells("Zyra", SpellSlot.E, SpellType.Line, 1150, 70, 1150, "ZyraGraspingRoots"));
                _spells.Add(
                    new Spells("Zyra", SpellSlot.Unknown, SpellType.Line, 1474, 70, 1900, "zyrapassivedeathmanager"));
            }

            private enum SpellType
            {
                Target,
                TargetGlobal,
                Line,
                LinePoint,
                LineAoE,
                AoE,
                Global
            }

            private class Spells
            {
                public readonly string ChampName;
                public readonly bool FoW;
                public readonly float Radius;
                public readonly float Range;
                public readonly SpellSlot Slot;
                public readonly float Speed;
                public readonly string SpellName;
                public readonly SpellType Type;

                public Spells(string champ,
                    SpellSlot slot,
                    SpellType type = SpellType.Target,
                    float range = 0,
                    float radius = 0,
                    float speed = 0,
                    string spell = "",
                    bool fow = true)
                {
                    ChampName = champ;
                    SpellName = spell;
                    Slot = slot;
                    Type = type;
                    Range = range;
                    Radius = radius;
                    Speed = speed;
                    FoW = fow;
                }
            }

            private class Skills
            {
                private readonly int _startTick;
                public readonly Vector2 Direction;
                public readonly Vector2 End;
                public readonly GameObject Obj;
                public readonly Spells Spell;
                public Vector2 Start;

                public Skills(Spells spell, int startT, Vector2 start, Vector2 end, GameObject obj)
                {
                    Spell = spell;
                    _startTick = startT;
                    Start = start;
                    End = end;
                    Obj = obj;
                    Direction = (End - Start).Normalized();
                }

                public bool Deactivate
                {
                    get
                    {
                        return _startTick > 0 &&
                               Utils.TickCount > _startTick + 1000 * (Start.Distance(End) / Spell.Speed);
                    }
                }
            }
        }
    }
}
