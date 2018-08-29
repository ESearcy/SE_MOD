using System.Collections.Generic;
using MiningDrones;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI.Ingame;
using VRage.Game;
using VRage.Game.Entity;
using VRage.ObjectBuilders;
using IMyLargeTurretBase = Sandbox.ModAPI.IMyLargeTurretBase;
using IMySlimBlock = VRage.Game.ModAPI.IMySlimBlock;

namespace DroneConquest
{
    class ItemManager
    {

        private static string logPath = "ItemManager.txt";
        private static SerializableDefinitionId _gatlingAmmo;
        private static SerializableDefinitionId _launcherAmmo;
        private static SerializableDefinitionId _uraniumFuel;
        public ItemManager()
        {

            Util.GetInstance().Log("inside constructor", logPath);
            _gatlingAmmo = new SerializableDefinitionId(new MyObjectBuilderType(new MyObjectBuilder_AmmoMagazine().GetType()), "NATO_25x184mm");

            Util.GetInstance().Log(_gatlingAmmo + " ammo1", logPath);
            _launcherAmmo = new SerializableDefinitionId(new MyObjectBuilderType(new MyObjectBuilder_AmmoMagazine().GetType()), "Missile200mm");

            Util.GetInstance().Log(_launcherAmmo + " ammo1", logPath);
            _uraniumFuel = new SerializableDefinitionId(new MyObjectBuilderType(new MyObjectBuilder_Ingot().GetType()), "Uranium");

            Util.GetInstance().Log(_uraniumFuel + " ammo1", logPath);
        }


        public void Reload(List<IMySlimBlock> guns)
        {
            for (int i = 0; i < guns.Count; i++)
            {
                if (IsAGun((MyEntity)guns[i].FatBlock))
                    Reload((MyEntity)guns[i].FatBlock, _gatlingAmmo);
                else
                    Reload((MyEntity)guns[i].FatBlock, _launcherAmmo);
            }
        }

        private void Reload(MyEntity gun, SerializableDefinitionId ammo, bool reactor = false)
        {
            var cGun = gun;
            MyInventory inv = cGun.GetInventory(0);
            VRage.MyFixedPoint point = inv.GetItemAmount(ammo, MyItemFlags.None | MyItemFlags.Damaged);
            
            if (point.RawValue > 1000000)
                return;
            //inv.Clear();
            VRage.MyFixedPoint amount = new VRage.MyFixedPoint();
            amount.RawValue = 2000000;
            Util.GetInstance().Log(ammo.SubtypeName + " [ReloadGuns] Amount " + amount, logPath);
            MyObjectBuilder_InventoryItem ii;
            if (reactor)
            {

                Util.GetInstance().Log(ammo.SubtypeName + " [ReloadGuns] loading reactor " + point.RawValue, "ItemManager.txt");
                ii = new MyObjectBuilder_InventoryItem()
                {
                    Amount = 10,
                    Content = new MyObjectBuilder_Ingot() { SubtypeName = ammo.SubtypeName }
                };
                Util.GetInstance().Log(ammo.SubtypeName + " [ReloadGuns] loading reactor 2 " + point.RawValue, "ItemManager.txt");
            }
            else
            {

                Util.GetInstance().Log(ammo.SubtypeName + " [ReloadGuns] loading guns " + point.RawValue, "ItemManager.txt");
                ii = new MyObjectBuilder_InventoryItem()
                {
                    Amount = 4,
                    Content = new MyObjectBuilder_AmmoMagazine() { SubtypeName = ammo.SubtypeName }
                };
                Util.GetInstance().Log(ammo.SubtypeName + " [ReloadGuns] loading guns 2 " + point.RawValue, "ItemManager.txt");
            }
            //inv.
            Util.GetInstance().Log(amount + " Amount : content " + ii.Content, "ItemManager.txt");
            inv.AddItems(amount, ii.Content);
            

            point = inv.GetItemAmount(ammo, MyItemFlags.None | MyItemFlags.Damaged);
        }

        private bool IsAGun(MyEntity gun)
        {
            return gun is IMyLargeTurretBase;
        }

        public void ReloadReactors(List<IMySlimBlock> reactors)
        {
            for (int i = 0; i < reactors.Count; i++)
            {

                Reload((MyEntity)reactors[i].FatBlock, _uraniumFuel, true);
            }
        }

    }
}

