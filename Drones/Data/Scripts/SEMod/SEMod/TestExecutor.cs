using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace SEMod
{
    class TestExecutor
    {
        private static String _logpath = "TestExecutor";
        private static Ship testShip;
        private static Spawner spawner = new Spawner();

        public static void ExecuteTests()
        {
            try
            {
                
                SetupFaction();
            }
            catch (Exception e)
            {
                Logger.LogException(e);
            }
            //NavigateShipToOrigin(ship);
        }

        public static void SpawnShip(ShipTypes type , Vector3D location, long ownerid)
        {
            var freeplace = MyAPIGateway.Entities.FindFreePlace(location, 20);

            spawner.SpawnShip(type, (Vector3D) freeplace, ownerid);

        }

        private static void NavigateShipToOrigin(Ship ship)
        {
            
        }

        private static IMyFaction CreateFaction(long founderId, String factionTag, String factionName, String desc, bool acceptmembers, bool acceptPeace)
        {
            try { 
            bool navyExists = MyAPIGateway.Session.Factions.FactionNameExists(factionName);
                
                if (!navyExists)
            {
                MyAPIGateway.Session.Factions.CreateFaction(founderId, factionTag, factionName, desc, "none");
            }

                Logger.Debug(MyAPIGateway.Session.Factions.FactionNameExists(factionName)+"");
            IMyFaction faction = MyAPIGateway.Session.Factions.TryGetFactionByName(factionName);
            MyAPIGateway.Session.Factions.ChangeAutoAccept(faction.FactionId, founderId, acceptmembers, acceptPeace);
            //MyAPIGateway.Session.Factions.CreateFaction();
        }
            catch (Exception e)
            {
                Logger.LogException(e);
            }
            return null;
        }

        public static void SetupFaction()
        {
            //Util.NotifyHud("Creating Factions: ");
            ////IMyFaction drones = CreateFaction(1, "DRO", "Drones", "Order mataining force", false, false);
            //IMyFaction navy = CreateFaction(FleetController.PlayerId, "NVY", "Navy", "Peace maintaining force", false, true);
            //Util.NotifyHud("Done Creating Factions: ");

        }
    }
}
