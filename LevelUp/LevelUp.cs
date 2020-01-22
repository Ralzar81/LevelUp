// Project:         LevelUp Adjuster  mod for Daggerfall Unity (http://www.dfworkshop.net)
// Copyright:       Copyright (C) 2019 Ralzar
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Author:          Ralzar

using UnityEngine;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Formulas;
using System;

namespace LevelUp
{
    public class LevelUp : MonoBehaviour
    {
        static Mod mod;

        static bool editAP = false;
        static bool curvedAP = false;
        static bool editMaxAP = false;
        static bool editHP = false;
        static bool editLvlSpeed = false;
        static bool retroEnd = false;

        static int apMax = 6;
        static int apMin = 4;
        static int maxAttribute = 100;
        static int hpMax = 3;
        static int hpMin = 2;
        static int lvlSpeed = 2;

        static PlayerEntity playerEntity = GameManager.Instance.PlayerEntity;

        void Awake()
        {
            ModSettings settings = mod.GetSettings();

            editAP = settings.GetValue<bool>("AttributePoints", "ChangeAttributePoints");
            apMax = settings.GetValue<int>("AttributePoints", "MaximumAttributePoints");
            apMin = settings.GetValue<int>("AttributePoints", "MinimumAttributePoints");
            curvedAP = settings.GetValue<bool>("AttributePoints", "ActivateCurvedLeveling");

            editMaxAP = settings.GetValue<bool>("AttributeMaximum", "ChangeAttributeMaximum");
            maxAttribute = settings.GetValue<int>("AttributeMaximum", "MaximumAttributePoints");

            editHP = settings.GetValue<bool>("HitPoints", "ChangeHitPoints");
            hpMax = settings.GetValue<int>("HitPoints", "MaximumHitPoints");
            hpMin = settings.GetValue<int>("HitPoints", "MinimumHitPoints");
            retroEnd = settings.GetValue<bool>("HitPoints", "RetroactiveCalculation");

            editLvlSpeed = settings.GetValue<bool>("LevelingSpeed", "ChangeLevelingSpeed");
            lvlSpeed = settings.GetValue<int>("LevelingSpeed", "LevelSpeed");
        }
        
        void Start()
        {
            if (editAP)
            {
                FormulaHelper.RegisterOverride<Func<int>>(mod, "BonusPool", () =>
                {
                    if (apMin > apMax)
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
            if (editLvlSpeed)
            {
                FormulaHelper.RegisterOverride<Func<int, int, int>>(mod, "CalculatePlayerLevel", (int startingLevelUpSkillsSum, int currentLevelUpSkillsSum) =>
                {                    
                    lvlSpeed = (lvlSpeed * 3) + 24;
                    int calcLvl = (int)Mathf.Floor((currentLevelUpSkillsSum - startingLevelUpSkillsSum + lvlSpeed) / 15);
                    return calcLvl;
                });
            }
            if (editHP)
            {
                FormulaHelper.RegisterOverride<Func<PlayerEntity, int>>(mod, "CalculateHitPointsPerLevelUp", (player) =>
                {

                    int addHitPoints = 0;

                    if (hpMax == 0) { hpMax = player.Career.HitPointsPerLevel / 2; }
                    else if (hpMax == 1) { hpMax = player.Career.HitPointsPerLevel; }
                    else if (hpMax == 2) { hpMax = player.Career.HitPointsPerLevel * 2; };

                    if (hpMin == 0) { hpMin = 0; }
                    else if (hpMin == 1) { hpMin = hpMax / 2; }
                    else if (hpMin == 2) { hpMin = hpMax; };

                    int minRoll = hpMin;
                    int maxRoll = hpMax;

                    if (retroEnd)
                    {
                        int endBonus = FormulaHelper.HitPointsModifier(player.Stats.PermanentEndurance) * player.Level;
                        int pureMaxHP = ((maxRoll * (player.Level-1)) + 25);
                        int newHP = pureMaxHP + endBonus;
                        GameManager.Instance.PlayerEntity.MaxHealth = newHP;
                        Debug.Log("retroEnd endBonus=" + endBonus.ToString() + " pureMaxHP=" + pureMaxHP.ToString());
                        Debug.Log("retroEnd newHP=" + newHP.ToString());
                    }

                    addHitPoints = UnityEngine.Random.Range(minRoll, maxRoll + 1);
                        addHitPoints += FormulaHelper.HitPointsModifier(player.Stats.PermanentEndurance);
                        if (addHitPoints < 1)
                            addHitPoints = 1;
                    Debug.Log("minRoll=" + minRoll.ToString() + " maxRoll=" + maxRoll.ToString());
                    Debug.Log("addHitPoints=" + addHitPoints.ToString());
                    return addHitPoints;
                    
                });
            }           
        }



        [Invoke(StateManager.StateTypes.Game, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            GameObject GO = new GameObject("NoDiceStats");
            GO.AddComponent<LevelUp>();
            mod.IsReady = true;
        }
    }
}
