using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Multiplayer;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;

namespace SEMod
{
    class ItemManager
    {

        private static string logPath = "ItemManager.txt";
        private static SerializableDefinitionId _gatlingAmmo;
        private static SerializableDefinitionId _launcherAmmo;
        private static SerializableDefinitionId _uraniumFuel;
        private static SerializableDefinitionId _iceFuel;
        public ItemManager()
        {

            Logger.Debug("inside constructor");
            _gatlingAmmo = new SerializableDefinitionId(new MyObjectBuilderType(new MyObjectBuilder_AmmoMagazine().GetType()), "NATO_25x184mm");

            Logger.Debug(_gatlingAmmo + " gat ammo");
            _launcherAmmo = new SerializableDefinitionId(new MyObjectBuilderType(new MyObjectBuilder_AmmoMagazine().GetType()), "Missile200mm");

            Logger.Debug(_launcherAmmo + " launcher ammo");
            _uraniumFuel = new SerializableDefinitionId(new MyObjectBuilderType(new MyObjectBuilder_Ingot().GetType()), "Uranium");

            Logger.Debug(_uraniumFuel + " fuel");
            
            _iceFuel = new SerializableDefinitionId(new MyObjectBuilderType(new MyObjectBuilder_Ore().GetType()), "Ice");
            Logger.Debug(_launcherAmmo + " launcher ammo");
        }


        public void Reload(List<IMyTerminalBlock> guns)
        {
            for (int i = 0; i < guns.Count; i++)
            {
                if (IsAGun((MyEntity)guns[i]))
                    Reload((MyEntity)guns[i], _gatlingAmmo);
                else
                    Reload((MyEntity)guns[i], _launcherAmmo);
            }
        }

        private void Reload(MyEntity gun, SerializableDefinitionId ammo, bool reactor = false)
        {
            var cGun = gun;
            MyInventory inv = cGun.GetInventory(0);
            VRage.MyFixedPoint amount = new VRage.MyFixedPoint();
            amount.RawValue = 2000000;
            var hasEnough = inv.ContainItems(amount,new MyObjectBuilder_Ingot() {SubtypeName = ammo.SubtypeName});
            VRage.MyFixedPoint point = inv.GetItemAmount(ammo, MyItemFlags.None | MyItemFlags.Damaged);

            if (hasEnough)
                return;
            //inv.Clear();
            
            Logger.Debug(ammo.SubtypeName + " [ReloadGuns] Amount " + amount);
            MyObjectBuilder_InventoryItem ii;
            if (reactor)
            {

                Logger.Debug(ammo.SubtypeName + " [ReloadGuns] loading reactor " + point.RawValue);
                ii = new MyObjectBuilder_InventoryItem()
                {
                    Amount = 10,
                    Content = new MyObjectBuilder_Ingot() { SubtypeName = ammo.SubtypeName }
                };
                Logger.Debug(ammo.SubtypeName + " [ReloadGuns] loading reactor 2 " + point.RawValue);
            }
            else
            {

                Logger.Debug(ammo.SubtypeName + " [ReloadGuns] loading guns " + point.RawValue);
                ii = new MyObjectBuilder_InventoryItem()
                {
                    Amount = 4,
                    Content = new MyObjectBuilder_AmmoMagazine() { SubtypeName = ammo.SubtypeName }
                };
                Logger.Debug(ammo.SubtypeName + " [ReloadGuns] loading guns 2 " + point.RawValue);
            }
            //inv.
            Logger.Debug(amount + " Amount : content " + ii.Content);
            inv.AddItems(amount, ii.Content);


            point = inv.GetItemAmount(ammo, MyItemFlags.None | MyItemFlags.Damaged);
        }

        private void ReloadIce(MyEntity gun, SerializableDefinitionId ammo)
        {
            var cGun = gun;
            MyInventory inv = cGun.GetInventory(0);
            VRage.MyFixedPoint point = inv.GetItemAmount(ammo, MyItemFlags.None | MyItemFlags.Damaged);

            if (point.RawValue > 1000000)
                return;
            //inv.Clear();
            VRage.MyFixedPoint amount = new VRage.MyFixedPoint();
            amount.RawValue = 2000000;

            MyObjectBuilder_InventoryItem ii = new MyObjectBuilder_InventoryItem()
            {
                Amount = 100,
                Content = new MyObjectBuilder_Ore() { SubtypeName = ammo.SubtypeName }
            };

            inv.AddItems(amount, ii.Content);
        }

        private bool IsAGun(MyEntity gun)
        {
            return gun is IMyLargeTurretBase || gun is IMySmallGatlingGun;
        }

        public void ReloadHydrogenTanks(List<IMyTerminalBlock> hydrogenTanks)
        {
            for (int i = 0; i < hydrogenTanks.Count; i++)
            {

                ReloadIce((MyEntity)hydrogenTanks[i], _iceFuel);
            }
        }

        public void ReloadReactors(List<IMyTerminalBlock> reactors)
        {
            for (int i = 0; i < reactors.Count; i++)
            {

                Reload((MyEntity)reactors[i], _uraniumFuel, true);
            }
        }

    }
}
