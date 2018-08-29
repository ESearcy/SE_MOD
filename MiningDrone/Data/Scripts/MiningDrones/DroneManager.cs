using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DroneConquest;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;
using IMyTerminalBlock = Sandbox.ModAPI.IMyTerminalBlock;

using IMyControllableEntity = VRage.Game.ModAPI.Interfaces.IMyControllableEntity;

namespace MiningDrones
{
    class DroneManager
    {
        Dictionary<long, PlayerAssets> assets = new Dictionary<long, PlayerAssets>();
        private static DroneManager _instance = null;
        private int DronesPerPlayerSquad = 4;
        private static string logPath = "DroneManager.txt";
        private Spawner _shipSpawner = new Spawner();
        

        public HashSet<SpacePirateShip> GetDrones()
        {
            var set = new HashSet<SpacePirateShip>(assets.Values.SelectMany(x=>x.MiningDrones));
            return set;
        }

        private SpacePirateShip ship;
        private SpacePirateShip ship2;
        private SpacePirateShip ship3;
        private SpacePirateShip ship4;
        public void Update()
        {
            

            UpdateKnownPlayerLocations();
            FindAllDrones();
            int count = 0;
            foreach (var asset in assets.Values)
            {
                asset.Update(count);
                count++;
            }
        }

        private void UpdateKnownPlayerLocations()
        {
            List<IMyPlayer> players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players);

            foreach (var player in players)
            {
                if (!assets.ContainsKey(player.PlayerID))
                {
                    assets.Add(player.PlayerID, new PlayerAssets(player.PlayerID){_playerLocation = player.GetPosition()});
                }
                else
                {
                    assets[player.PlayerID]._playerLocation = player.GetPosition();

                }
            }
        }

        public void AddSpacePirate(SpacePirateShip drone)
        {
            if (assets.Keys.Contains(drone.GetOwnerId()))
            {
                assets[drone.GetOwnerId()].MiningDrones.Add(drone);
                Util.GetInstance().Log("[AddSpacePirate] squad existed: drone added!", logPath);
            }
            else
            {
                assets.Add(drone.GetOwnerId(), new PlayerAssets(drone.GetOwnerId()){MiningDrones = new HashSet<SpacePirateShip>() { drone }});
                Util.GetInstance().Log("[AddSpacePirate] squad created: drone added!", logPath);
            }
        }

        public void AddDrone(Drone drone)
        {
            //if (assets.Keys.Contains(drone.GetOwnerId()))
            //{
            //    assets[drone.GetOwnerId()].MiningDrones.Add(drone);
            //    Util.GetInstance().Log("[PlayerDronemanager.AddDrone] squad existed: drone added!");
            //}
            //else
            //{
            //    assets.Add(drone.GetOwnerId(), new PlayerAssets(drone.GetOwnerId()){MiningDrones = new HashSet<Drone>() { drone }});
            //    Util.GetInstance().Log("[PlayerDronemanager.AddDrone] squad created: drone added!");
            //}
        }

        public void AddCommand(DroneCommandCenter drone)
        {
            //if (assets.Keys.Contains(drone.PlayerId))
            //{
            //    assets[drone.PlayerId].CommandStations.Add(drone);
            //    Util.GetInstance().Log("[PlayerDronemanager.AddCommand] COmmand Center added for player "+drone.PlayerId +"!");
            //}
            //else
            //{
            //    assets.Add(drone.PlayerId, new PlayerAssets(drone.PlayerId) { CommandStations = new HashSet<DroneCommandCenter>() { drone } });
            //    Util.GetInstance().Log("[PlayerDronemanager.AddDrone] squad created: drone added!");
            //}
        }


        public void StopAllDrones()
        {
            foreach (var squad in assets.Values.SelectMany(x=>x.MiningDrones))
            {
                squad.Stop();
            }
        }

        public void ClearAllDrones()
        {
            foreach (var squad in assets.Values.SelectMany(x => x.MiningDrones))
            {
                try
                {
                    squad.DeleteShip();
                }
                catch (Exception) { }
            }
        }

        public void FindAllDrones()
        {

            //var origin = new Vector3D(0, 0, 0);
            //if (ship == null || !ship.IsAlive())
            //{
            //    ship = _shipSpawner.SpawnShip(ConquestDrones.SmallOne, origin);
            //    ship.SetOwner(001);
            //    SetUpDrone(ship.Ship);
            //}


            //if (ship4 == null || !ship4.IsAlive())
            //{
            //    ship4 = _shipSpawner.SpawnShip(ConquestDrones.MediumTwo, new Vector3D(50, 250, 50));
            //    ship4.SetOwner(001);
            //    SetUpDrone(ship4.Ship);
            //}

            //if (ship3 == null || !ship3.IsAlive())
            //{
            //    ship3 = _shipSpawner.SpawnShip(ConquestDrones.MediumTwo, new Vector3D(250, 50, 50));
            //    ship3.SetOwner(001);
            //    SetUpDrone(ship3.Ship);
            //}

            Util.GetInstance().Log("[ConquestMod.FindAllDrones] Searching for drones", logPath);
            HashSet<SpacePirateShip> allDrones = GetDrones();

            HashSet<IMyEntity> entities = new HashSet<IMyEntity>();
            List<IMyPlayer> players = new List<IMyPlayer>();

            try
            {
                MyAPIGateway.Entities.GetEntities(entities);
                MyAPIGateway.Players.GetPlayers(players);
            }
            catch (Exception e)
            {
                Util.GetInstance().LogError(e.ToString());
                return;
            }

            //filter out any grids that are already accounted for
            foreach (IMyEntity entity in entities.Where(x=> x is IMyCubeGrid))
            {
                if (allDrones.All(x => x.Ship != entity))
                {
                    if (!entity.Transparent)
                    {
                        SetUpDrone(entity);
                    }
                }
            }
        }


        private void SetUpDrone(IMyEntity entity)
        {
            Sandbox.ModAPI.IMyGridTerminalSystem gridTerminal = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid((IMyCubeGrid)entity);
            List<Sandbox.ModAPI.Ingame.IMyTerminalBlock> T = new List<Sandbox.ModAPI.Ingame.IMyTerminalBlock>();
            gridTerminal.GetBlocksOfType<IMyTerminalBlock>(T);

            var droneType = IsDrone(T);

            if (droneType != 0)
            {
                try
                {
                    switch (droneType)
                    {
                        case DroneTypes.SpacePirateShip:
                        {
                            SpacePirateShip drone = new SpacePirateShip((IMyCubeGrid)entity);
                            Util.GetInstance().Log("[SetUpDrone] Found New Pirate Ship. id=" + drone.GetOwnerId(), logPath);
                            AddSpacePirate(drone);
                            break;
                        }
                        case DroneTypes.PlayerDrone:
                        {
                            //SpacePirateShip dro = new SpacePirateShip((IMyCubeGrid)entity);
                            //Util.GetInstance().Log("[MiningDrones.SetUpDrone] Found New Pirate Ship. id=" + dro.GetOwnerId(), "createDrone.txt");
                            //AddDrone(dro);
                            break;
                        }
                        case DroneTypes.NotADrone:
                        {
                            break;
                        }  
                    }
                }
                catch (Exception e)
                {
                    //MyAPIGateway.Entities.RemoveEntity(entity);
                    Util.GetInstance().LogError(e.ToString());
                }
            }
        }

        private string Drone = "#PirateDrone#";
        // return means 1 = mining drone with beacons, 2 = mining drone with antenna, 0 = not a mining drone
        private DroneTypes IsDrone(List<Sandbox.ModAPI.Ingame.IMyTerminalBlock> T)
        {
            Util.GetInstance().Log("[MiningDrones.SetUpDrone] is a drone?", "createDrone.txt");
            if (T.Exists(x => ((x).CustomName.Contains(Drone) && x.IsWorking)))
            {
                Util.GetInstance().Log("[MiningDrones.SetUpDrone] is a drone!", "createDrone.txt");
                return DroneTypes.SpacePirateShip;
            }

            return DroneTypes.NotADrone;

        }
    }
}
