using BepInEx.Configuration;
using R2API;
using RoR2;
using RoR2.Projectile;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using static MinigunnerMod.Main;
using MinigunnerMod.Utils;
using EntityStates.BrotherMonster;

namespace MinigunnerMod.Items
{
    public class SteelBootcaps : ItemBase<SteelBootcaps>
    {
        public ConfigEntry<float> DamageOfProjectile;

        public ConfigEntry<float> DamageBonus;

        public ConfigEntry<int> ActivationsToTrigger;

        public ConfigEntry<float> BootcapsActiveDuration;

        public override string ItemName => "Steel Bootcaps";

        public override string ItemLangTokenName => "STEEL_BOOTCAPS";

        public override string ItemPickupDesc => "Occasionally gain a buff when using your utility skill.";

        public override string ItemFullDescription => $"Every <style=cIsUtility>{ActivationsToTrigger.Value}</style> uses of your <style=cIsUtility>Utility Skill</style> become invincible and gain <style=cIsDamage>+{DamageBonus.Value * 100f}%</style> damage <style=cStack>(+{DamageBonus.Value * 100f}% damage per stack)</style>.";

        public override string ItemLore => "Some reinforced steel bootcaps that are always cold to the touch. An incscription on the back reads 'If found return to the Cryon Mining Company'. ";

        public override ItemTier Tier => ItemTier.Tier2;

        public override GameObject ItemModel => MainAssets.LoadAsset<GameObject>("PracticeOrbDisplay.prefab");

        public override Sprite ItemIcon => MainAssets.LoadAsset<Sprite>("BootcapIcon.png");

        public static GameObject Projectile;

        public static GameObject ItemBodyModelPrefab;

        public static BuffDef BootcapsChargingBuff;
        public static BuffDef BootcapsActiveBuff;

        public override void Init(ConfigFile config)
        {
            CreateConfig(config);
            CreateLang();
            CreateProjectile();
            CreateBuff();
            CreateItem();
            Hooks();
        }

        private void CreateBuff()
        {
            BootcapsChargingBuff = ScriptableObject.CreateInstance<BuffDef>();
            BootcapsChargingBuff.name = "Minigunner: Bootcaps Charging";
            BootcapsChargingBuff.buffColor = Color.red;
            BootcapsChargingBuff.canStack = true;
            BootcapsChargingBuff.iconSprite = MainAssets.LoadAsset<Sprite>("BootcapIcon.png");

            ContentAddition.AddBuffDef(BootcapsChargingBuff);

            BootcapsActiveBuff = ScriptableObject.CreateInstance<BuffDef>();
            BootcapsActiveBuff.name = "Minigunner: Bootcaps Active";
            BootcapsActiveBuff.buffColor = Color.green;
            BootcapsActiveBuff.canStack = false;
            BootcapsActiveBuff.iconSprite = MainAssets.LoadAsset<Sprite>("BootcapIcon.png"); ;

            ContentAddition.AddBuffDef(BootcapsActiveBuff);
        }

        private void CreateProjectile()
        {
            Projectile = PrefabAPI.InstantiateClone(Resources.Load<GameObject>("prefabs/projectiles/FMJ"), "OrbProjectile", true);


            var model = MainAssets.LoadAsset<GameObject>("PracticeOrbDisplay.prefab");
            model.AddComponent<NetworkIdentity>();
            model.AddComponent<ProjectileGhostController>();

            var projectileController = Projectile.GetComponent<ProjectileController>();
            projectileController.ghostPrefab = model;

            PrefabAPI.RegisterNetworkPrefab(Projectile);
            ContentAddition.AddProjectile(Projectile);
        }
         
        public override void CreateConfig(ConfigFile config)
        {
            DamageOfProjectile = config.Bind<float>("Item: " + ItemName, "Damage of Projectile", 100, "what damage should our projectile have?");
            ActivationsToTrigger = config.Bind<int>("Item: " + ItemName, "Activations to Trigger", 4, "how many utility activations does it take to activate the item?");
            BootcapsActiveDuration = config.Bind<float>("Item: " + ItemName, "Duration of Buff", 6f, "how long will the damage bonus last?");
            DamageBonus = config.Bind<float>("Item: " + ItemName, "Damage Bonus", 1f, "how much bonus damage do we get?"); 
        }
        public override ItemDisplayRuleDict CreateItemDisplayRules()
        {
            ItemBodyModelPrefab = ItemModel;
            var ItemDisplay = ItemBodyModelPrefab.AddComponent<ItemDisplay>();
            ItemDisplay.rendererInfos = ItemHelpers.ItemDisplaySetup(ItemBodyModelPrefab);

            ItemDisplayRuleDict rules = new ItemDisplayRuleDict();
            rules.Add("mdlCommandoDualies", new ItemDisplayRule[]
            {
                new ItemDisplayRule
                {
                    ruleType = ItemDisplayRuleType.ParentedPrefab,
                    followerPrefab = ItemBodyModelPrefab,
                    childName = "pelvis",
                    localPos = new Vector3(0,0,0),
                    localAngles = new Vector3(0,0,0),
                    localScale = new Vector3(1,1,1)
                }
            });

            return rules;
        }
        public override void Hooks()
        {
            //On.RoR2.CharacterBody.OnSkillActivated += FireProjectile;
            On.RoR2.CharacterBody.OnSkillActivated += TriggerBootcaps;
            RecalculateStatsAPI.GetStatCoefficients += RecalculateStatsAPI_GetStatCoefficients;
        }

        private void RecalculateStatsAPI_GetStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if(sender.GetBuffCount(BootcapsActiveBuff) > 0)
            {
                var inventoryCount = GetCount(sender);
                args.damageMultAdd += inventoryCount * DamageBonus.Value;
            }
        }

        private void TriggerBootcaps(On.RoR2.CharacterBody.orig_OnSkillActivated orig, CharacterBody self, GenericSkill skill)
        {

            var inventoryCount = GetCount(self);
            if(inventoryCount > 0 && skill == self.skillLocator.utility)
            {
                int currChargingBuffs = self.GetBuffCount(BootcapsChargingBuff);
                if (currChargingBuffs < ActivationsToTrigger.Value)
                {
                    self.AddBuff(BootcapsChargingBuff);
                }
                if (self.GetBuffCount(BootcapsChargingBuff) >= ActivationsToTrigger.Value)
                {
                    while(self.GetBuffCount(BootcapsChargingBuff) > 0)
                        self.RemoveBuff(BootcapsChargingBuff);
                    self.AddTimedBuff(BootcapsActiveBuff, BootcapsActiveDuration.Value);
                    self.AddTimedBuff(RoR2Content.Buffs.HiddenInvincibility, BootcapsActiveDuration.Value);
                }
            }
            orig(self, skill);
        }

        //private void FireProjectile(On.RoR2.CharacterBody.orig_OnSkillActivated orig, RoR2.CharacterBody self, RoR2.GenericSkill skill)
        //{
        //    var inventoryCount = GetCount(self);
        //    if (inventoryCount > 0)
        //    {
        //        FireProjectileInfo fireProjectileInfo = new FireProjectileInfo()
        //        {
        //            owner = self.gameObject,
        //            damage = DamageOfProjectile.Value * inventoryCount,
        //            position = self.corePosition,
        //            rotation = Util.QuaternionSafeLookRotation(self.inputBank.aimDirection),
        //            projectilePrefab = Projectile
        //        };

        //        ProjectileManager.instance.FireProjectile(fireProjectileInfo);
        //    }

        //    orig(self, skill);
        //}
    }
}
