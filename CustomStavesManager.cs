using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace DvergrAllies
{
    public static class CustomStavesManager
    {
        public static void Setup()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= Setup;

            CreateEarthStaff();
            CreateSpiritStaff();
        }

        private static void CreateEarthStaff()
        {
            GameObject baseStaff = PrefabManager.Instance.GetPrefab("DvergerStaffFire");
            if (baseStaff == null) return;

            CustomItem earthStaff = new CustomItem("AllyDvergrStaffEarth", "DvergerStaffFire");
            ItemDrop earthDrop = earthStaff.ItemDrop;
            earthDrop.m_itemData.m_shared.m_name = "Earth Staff";
            
            // Custom Projectile
            GameObject baseProj = PrefabManager.Instance.GetPrefab("DvergerStaffIce_projectile");
            if (baseProj != null)
            {
                GameObject earthProj = PrefabManager.Instance.CreateClonedPrefab("AllyDvergrStaffEarth_projectile", "DvergerStaffIce_projectile");
                Projectile p = earthProj.GetComponent<Projectile>();
                if (p != null)
                {
                    p.m_damage.m_frost = 0;
                    p.m_damage.m_blunt = 20f;
                    p.m_damage.m_poison = 40f;
                    
                    // Tar effect
                    p.m_statusEffect = "Tar"; 

                    // Blob poison cloud VFX
                    GameObject blobHit = PrefabManager.Instance.GetPrefab("vfx_blob_attack");
                    if (blobHit != null)
                    {
                        p.m_hitEffects.m_effectPrefabs = new EffectList.EffectData[] {
                            new EffectList.EffectData { m_prefab = blobHit, m_enabled = true }
                        };
                    }
                }
                earthDrop.m_itemData.m_shared.m_attack.m_attackProjectile = earthProj;
            }

            ItemManager.Instance.AddItem(earthStaff);
        }

        private static void CreateSpiritStaff()
        {
            GameObject baseStaff = PrefabManager.Instance.GetPrefab("DvergerStaffFire");
            if (baseStaff == null) return;

            CustomItem spiritStaff = new CustomItem("AllyDvergrStaffSpirit", "DvergerStaffFire");
            ItemDrop spiritDrop = spiritStaff.ItemDrop;
            spiritDrop.m_itemData.m_shared.m_name = "Spirit Staff";
            
            GameObject baseProj = PrefabManager.Instance.GetPrefab("DvergerStaffIce_projectile");
            if (baseProj != null)
            {
                GameObject spiritProj = PrefabManager.Instance.CreateClonedPrefab("AllyDvergrStaffSpirit_projectile", "DvergerStaffIce_projectile");
                Projectile p = spiritProj.GetComponent<Projectile>();
                if (p != null)
                {
                    p.m_damage.m_frost = 0;
                    p.m_damage.m_blunt = 20f;
                    p.m_damage.m_spirit = 60f; // Unholy sizzle
                }
                spiritDrop.m_itemData.m_shared.m_attack.m_attackProjectile = spiritProj;
            }

            ItemManager.Instance.AddItem(spiritStaff);
        }
    }
}
