// Project:         LevelUp Adjuster  mod for Daggerfall Unity (http://www.dfworkshop.net)
// Copyright:       Copyright (C) 2020 Ralzar
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Author:          Ralzar

using UnityEngine;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Formulas;
using System;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallConnect;
using DaggerfallWorkshop.Game.UserInterfaceWindows;

namespace LevelUpAdjuster
{
    public class LevelUp : MonoBehaviour
    {
        static Mod mod;

        static PlayerEntity playerEntity = GameManager.Instance.PlayerEntity;

        static bool editAP = false;
        static bool curvedAP = false;
        //static bool editStartAP = false;
        static bool editMaxAP = false;
        static bool editHP = false;
        static bool retroEnd = false;
        static bool medianHP = false;

        static int apMax = 6;
        static int apMin = 4;
        static int startAttribute = 0;
        static int maxAttribute = 100;
        static int hpMaxSetting = 1;
        static int hpMinSetting = 1;
        static int minRoll = 0;
        static int maxRoll = 0;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            var go = new GameObject(mod.Title);
            go.AddComponent<LevelUp>();
            mod.IsReady = true;
            StartGameBehaviour.OnStartGame += StartAttributes_OnStartGame;

            ModSettings settings = mod.GetSettings();

            apMax = settings.GetValue<int>("AttributePoints", "MaximumAttributePoints");
            apMin = settings.GetValue<int>("AttributePoints", "MinimumAttributePoints");
            curvedAP = settings.GetValue<bool>("AttributePoints", "ActivateCurvedLeveling");
            startAttribute = settings.GetValue<int>("StartingAttributes", "AdjustStartingAttributesBy");
            maxAttribute = settings.GetValue<int>("AttributeMaximum", "MaximumAttributePoints");
            hpMaxSetting = settings.GetValue<int>("HitPoints", "MaximumHitPoints");
            hpMinSetting = settings.GetValue<int>("HitPoints", "MinimumHitPoints");
            medianHP = settings.GetValue<bool>("HitPoints", "MedianHitPoints");
            retroEnd = settings.GetValue<bool>("HitPoints", "RetroactiveEnduranceBonus");

            if (apMax != 6 || apMin != 4)
                editAP = true;
            //if (startAttribute != 0)
            //    editStartAP = true;
            if (maxAttribute != 100)
                editMaxAP = true;
            if (hpMaxSetting != 1 || hpMinSetting != 1 || medianHP || retroEnd)
                editHP = true;
        }
        
        void Start()
        {
            Debug.Log("[LevelUp Adjuster] Loading Start().");
            if (editAP || curvedAP)
            {
                FormulaHelper.RegisterOverride<Func<int>>(mod, "BonusPool", () =>
                {
                    if (!editAP)
                    {
                        apMax = 6;
                        apMin = 4;
                    }
                    else if (apMin > apMax)
                    { apMin = apMax; }

                    if (curvedAP)
                    {
                        int apAdjust = playerEntity.Level / 2;
                        apMin = Mathf.Clamp(apMin + 4 - apAdjust, 1, 9);
                        apMax = Mathf.Clamp(apMax + 4 - apAdjust, 1, 9);
                    }
                    int minBonusPool = apMin;        // The minimum number of free points to allocate on level up
                    int maxBonusPool = apMax;        // The maximum number of free points to allocate on level up

                    // Roll bonus pool for player to distribute
                    // Using maxBonusPool + 1 for inclusive range
                    UnityEngine.Random.InitState(Time.frameCount);
                    return UnityEngine.Random.Range(minBonusPool, maxBonusPool + 1);
                });
            }
            if (editMaxAP)
            {
                FormulaHelper.RegisterOverride<Func<int>>(mod, "MaxStatValue", () =>
                {
                    return maxAttribute;
                });
            }
            if (editHP)
            {
                FormulaHelper.RegisterOverride<Func<PlayerEntity, int>>(mod, "CalculateHitPointsPerLevelUp", (player) =>
                {

                    int addHitPoints = 0;
                    int hpMax = player.Career.HitPointsPerLevel;
                    int hpMin = hpMax / 2;

                    if (hpMaxSetting == 0) { hpMax /= 2; }
                    else if (hpMaxSetting == 2) { hpMax *= 2; };

                    if (hpMinSetting == 0) { hpMin = 0; }
                    else if (hpMinSetting == 2) { hpMin = hpMax; };

                    minRoll = hpMin;
                    maxRoll = hpMax;

                    if (medianHP)
                    {
                        minRoll = (hpMax + hpMin) / 2;
                        maxRoll = (hpMax + hpMin) / 2;
                    }

                    if (retroEnd)
                    {
                        EntityEffectBroker.OnNewMagicRound += RetroEndBonus_OnNewMagicRound;
                        addHitPoints = maxRoll;
                        addHitPoints += FormulaHelper.HitPointsModifier(player.Stats.PermanentEndurance);
                    }
                    else
                    {
                        addHitPoints = UnityEngine.Random.Range(minRoll, maxRoll + 1);
                        addHitPoints += FormulaHelper.HitPointsModifier(player.Stats.PermanentEndurance);
                        if (addHitPoints < 1) { addHitPoints = 1; }
                    }
                    Debug.Log("minRoll=" + minRoll.ToString() + " maxRoll=" + maxRoll.ToString());
                    Debug.Log("addHitPoints=" + addHitPoints.ToString());
                    return addHitPoints;
                    
                });
            }           
        }



        private static void RetroEndBonus_OnNewMagicRound()
        {
            int HPmod = (int)Mathf.Floor(playerEntity.Stats.PermanentEndurance / 10) - 5;
            int endBonus = HPmod * playerEntity.Level;
            int pureMaxHP = ((maxRoll * playerEntity.Level) + 25);
            int newHP = pureMaxHP + endBonus;
            playerEntity.MaxHealth = newHP;
            playerEntity.CurrentHealth = newHP;
            Debug.Log("retroEnd OnNewMagicRound Endurance = " + playerEntity.Stats.PermanentEndurance.ToString() +  "HPmod = " + HPmod.ToString() + ", endBonus = " + endBonus.ToString() + ", pureMaxHP = " + pureMaxHP.ToString());
            Debug.Log("retroEnd OnNewMagicRound newHP=" + newHP.ToString());
            Debug.Log("RetroEndBonus_OnNewMagicRound de-registered from OnNewMagicRound");
            EntityEffectBroker.OnNewMagicRound -= RetroEndBonus_OnNewMagicRound;
        }

        private static void StartAttributes_OnStartGame(object sender, EventArgs e)
        {
            int maxStat = FormulaHelper.MaxStatValue();

            DaggerfallStats stats = playerEntity.Stats;

            int agility = playerEntity.Stats.PermanentAgility + startAttribute;
            int endurance = playerEntity.Stats.PermanentEndurance + startAttribute;
            int intelligence = playerEntity.Stats.PermanentIntelligence + startAttribute;
            int luck = playerEntity.Stats.PermanentLuck + startAttribute;
            int personality = playerEntity.Stats.PermanentPersonality + startAttribute;
            int speed = playerEntity.Stats.PermanentSpeed + startAttribute;
            int strength = playerEntity.Stats.PermanentStrength + startAttribute;
            int willpower = playerEntity.Stats.PermanentWillpower + startAttribute;

            if (agility - startAttribute < 2 || endurance + startAttribute < 2 || intelligence + startAttribute < 2 || luck + startAttribute < 2 || personality + startAttribute < 2 || speed + startAttribute < 2 || strength + startAttribute < 2 || willpower + startAttribute < 2)
            {
                string[] messages = new string[] { "LevelUp Adjuster has changed all starting attributes by " + startAttribute.ToString() + ".", "One or more of your attributes is reduced 1 or 0, which kills you." };
                StatusPopup(messages);
            }
            else if (agility + startAttribute > maxStat || endurance + startAttribute > maxStat || intelligence + startAttribute > maxStat || luck + startAttribute > maxStat || personality + startAttribute > maxStat || speed + startAttribute > maxStat || strength + startAttribute > maxStat || willpower + startAttribute > maxStat)
            {
                string[] messages = new string[] { "LevelUp Adjuster has changed all starting attributes by " + startAttribute.ToString() + ".", "One or more of your attributes is higher than the allowed maximum of " + maxStat.ToString() + " and will be adjusted down." };
                StatusPopup(messages);

                if (agility > maxStat) { agility = maxStat; }
                if (endurance > maxStat) { endurance = maxStat; }
                if (intelligence > maxStat) { intelligence = maxStat; }
                if (luck > maxStat) { luck = maxStat; }
                if (personality > maxStat) { personality = maxStat; }
                if (speed > maxStat) { speed = maxStat; }
                if (strength > maxStat) { strength = maxStat; }
                if (willpower > maxStat) { willpower = maxStat; }
            }
            else
            {
                string[] messages = new string[] { "LevelUp Adjuster has changed all starting attributes by " + startAttribute.ToString() + "." };
                StatusPopup(messages);
            }

            stats.SetPermanentStatValue(DFCareer.Stats.Agility, agility);
            stats.SetPermanentStatValue(DFCareer.Stats.Endurance, endurance);
            stats.SetPermanentStatValue(DFCareer.Stats.Intelligence, intelligence);
            stats.SetPermanentStatValue(DFCareer.Stats.Luck, luck);
            stats.SetPermanentStatValue(DFCareer.Stats.Personality, personality);
            stats.SetPermanentStatValue(DFCareer.Stats.Speed, speed);
            stats.SetPermanentStatValue(DFCareer.Stats.Strength, strength);
            stats.SetPermanentStatValue(DFCareer.Stats.Willpower, willpower);
        }

        static DaggerfallMessageBox tempInfoBox;

        public static void StatusPopup(string[] message)
        {
            if (tempInfoBox == null)
            {
                tempInfoBox = new DaggerfallMessageBox(DaggerfallUI.UIManager);
                tempInfoBox.AllowCancel = true;
                tempInfoBox.ClickAnywhereToClose = true;
                tempInfoBox.ParentPanel.BackgroundColor = Color.clear;
            }

            tempInfoBox.SetText(message);
            DaggerfallUI.UIManager.PushWindow(tempInfoBox);
        }
    }
}
