using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace SEMod
{
    class Spawner
    {
        private String _logPath = "Spawner";
        public Spawner()
        {
            map.Add(ShipTypes.NavyFighter, "NavyDrone");
            map.Add(ShipTypes.AIDrone, "-DC-Praetorian");
            map.Add(ShipTypes.NavyFrigate, "-DC-Buzzer");
            map.Add(ShipTypes.AILeadShip, "-DC-Tusker");
            //map.Add(5, "-DC-Buzzer");
        }

        private Vector3D GetPositionWithinAnyPlayerViewDistance(Vector3D pos)
        {
            List<IMyPlayer> players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players);
            var playerViewDistance = MyAPIGateway.Session.SessionSettings.ViewDistance;
            Vector3D position = Vector3D.Zero;

            if (players.Any())
            {
                position = (players.OrderBy(x => (x.GetPosition() - pos).Length()).First().GetPosition() + new Vector3D(0, 0, playerViewDistance * .85));
            }

            return position;
        }

        private IMyGps marker = null;
        private Dictionary<ShipTypes, String> map = new Dictionary<ShipTypes, String>();

        //both full methods below can spawn a ship
        public void SpawnShip(ShipTypes type, Vector3D location, long ownerid)
        {
            AddPrefab(map[type], type, location, ownerid);
            //try
            //{

            //    var definitions = MyDefinitionManager.Static.GetPrefabDefinitions();
            //    foreach (var item in definitions)
            //    {
            //        Logger.Debug("ShipName: " + item.Key);
            //    }

            //    var t = MyDefinitionManager.Static.GetPrefabDefinition(map[type]);

            //    if (t == null)
            //    {
            //        Logger.Debug("Failed To Load Ship: " + map[type], "Spawner.txt");
            //        return;
            //    }

            //    var s = t.CubeGrids;
            //    s = (MyObjectBuilder_CubeGrid[])s.Clone();

            //    if (s.Length == 0)
            //    {
            //        return;
            //    }

            //    Vector3I min = Vector3I.MaxValue;
            //    Vector3I max = Vector3I.MinValue;

            //    s[0].CubeBlocks.ForEach(b => min = Vector3I.Min(b.Min, min));
            //    s[0].CubeBlocks.ForEach(b => max = Vector3I.Max(b.Min, max));
            //    float size = new Vector3(max - min).Length();

            //    var freeplace = MyAPIGateway.Entities.FindFreePlace(location, size * 5f);
            //    if (freeplace == null)
            //        return;

            //    var newPosition = (Vector3D)freeplace;

            //    var grid = s[0];
            //    if (grid == null)
            //    {
            //        Logger.Debug("A CubeGrid is null!");
            //        return;
            //    }

            //    List<IMyCubeGrid> shipMade = new List<IMyCubeGrid>();

            //    var spawnpoint = GetPositionWithinAnyPlayerViewDistance(newPosition);
            //    var safespawnpoint = MyAPIGateway.Entities.FindFreePlace(spawnpoint, size * 5f);
            //    spawnpoint = safespawnpoint is Vector3D ? (Vector3D)safespawnpoint : new Vector3D();

            //    //to - from
            //    var direction = newPosition - spawnpoint;
            //    var finalSpawnPoint = location;//(direction / direction.Length()) * (MyAPIGateway.Session.SessionSettings.ViewDistance * .85);

            //    MyAPIGateway.PrefabManager.SpawnPrefab(shipMade, map[type], finalSpawnPoint, Vector3.Forward, Vector3.Up, Vector3.Zero, default(Vector3), null, SpawningOptions.SpawnRandomCargo, ownerid);

            //    //MyAPIGateway.PrefabManager.SpawnPrefab(shipMade, map[type], newPosition, Vector3.Forward, Vector3.Up);
            //    var bs = new BoundingSphereD(finalSpawnPoint, 500);
            //    var ents = MyAPIGateway.Entities.GetEntitiesInSphere(ref bs);
            //    var closeBy = ents;

            //}
            //catch (Exception e)
            //{
            //    Logger.LogException(e);
            //}
        }

        public bool AddPrefab(String prefabName, ShipTypes prefabType, Vector3D location, long ownerid)
        {


            long piratePlayerId = 0;
            if (prefabType == ShipTypes.AIDrone)
            {
                var fc = MyAPIGateway.Session.Factions.GetObjectBuilder();
                var faction = fc.Factions.FirstOrDefault(f => f.Tag == "SPRT");
                if (faction != null)
                {
                    var pirateMember = faction.Members.FirstOrDefault();
                    piratePlayerId = pirateMember.PlayerId;
                }
            }


            var prefab = MyDefinitionManager.Static.GetPrefabDefinition(prefabName);
            if (prefab.CubeGrids == null)
            {
                MyDefinitionManager.Static.ReloadPrefabsFromFile(prefab.PrefabPath);
                prefab = MyDefinitionManager.Static.GetPrefabDefinition(prefab.Id.SubtypeName);
            }

            if (prefab.CubeGrids.Length == 0)
                return false;


            // Use the cubeGrid BoundingBox to determine distance to place.
            Vector3I min = Vector3I.MaxValue;
            Vector3I max = Vector3I.MinValue;
            foreach (var b in prefab.CubeGrids[0].CubeBlocks)
            {
                min = Vector3I.Min(b.Min, min);
                max = Vector3I.Max(b.Min, max);
            }
            var size = new Vector3(max - min);

            // TODO: find a empty spot in space to spawn the prefab safely.


            var distance = (Math.Sqrt(size.LengthSquared()) * prefab.CubeGrids[0].GridSizeEnum.ToGridLength() / 2) + 2;
            var position = MyAPIGateway.Entities.FindFreePlace(location, 2000);
            // offset the position out in front of player by 2m.
            var offset = position - prefab.CubeGrids[0].PositionAndOrientation.Value.Position;
            var tempList = new List<MyObjectBuilder_EntityBase>();

            // We SHOULD NOT make any changes directly to the prefab, we need to make a Value copy using Clone(), and modify that instead.
            foreach (var grid in prefab.CubeGrids)
            {
                var gridBuilder = (MyObjectBuilder_CubeGrid)grid.Clone();
                gridBuilder.PositionAndOrientation =
                    new MyPositionAndOrientation((Vector3D)(grid.PositionAndOrientation.Value.Position + offset),
                        grid.PositionAndOrientation.Value.Forward, grid.PositionAndOrientation.Value.Up);

                if (prefabType == ShipTypes.AIDrone)
                {
                    foreach (var cube in gridBuilder.CubeBlocks)
                    {
                        cube.Owner = piratePlayerId;
                        cube.ShareMode = MyOwnershipShareModeEnum.None;
                    }
                }

                tempList.Add(gridBuilder);
            }

            tempList.CreateAndSyncEntities();
            return true;
        }
    }
}
