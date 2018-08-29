using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace SEMod
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class Driver : MySessionComponentBase
    {

        static String _logPath = "Driver";
        static int _ticks = 0;
        static bool _initalized = false;
        static int numNavyDrones = 4;
        static int numAiDrones = 4;
        static int numNavyships = 2;
        static int numAiships = 2;

        static FleetController navy = new FleetController("NavyFleet", 1234, ShipTypes.NavyFighter, ShipTypes.NavyFrigate,  new Vector3D(500, 500, 500), numNavyDrones, numNavyships);
        static FleetController drones = new FleetController("AIFleet", 4321, ShipTypes.AIDrone, ShipTypes.AILeadShip,  new Vector3D(-500, -500, -500), numAiDrones, numAiships);

        public override void UpdateBeforeSimulation()
        {
            try
            {

                bool shouldRun = (MyAPIGateway.Utilities != null) && (MyAPIGateway.Multiplayer != null) && (MyAPIGateway.Session != null)
                            && ((MyAPIGateway.Utilities.IsDedicated) || (MyAPIGateway.Multiplayer.IsServer));
                
                //Logger.DebugEnabled = true;

                if (!shouldRun)
                    return;

                //if (_ticks % 2 == 0)
                //{
                    Logger.Debug("Executing Drone Code");
                    if (_initalized)
                    {
                    //ClearHud();
                    FindAllDrones();

                    navy.Update();
                    //drones.Update();
                }
                    else
                    {
                        Setup();
                    }
                    Logger.Debug("Finished Executing Drone Code");
                //}

                _ticks++;
            }
            catch (Exception e)
            {
                Logger.LogException(e);
            }
        }

        public void ClearHud()
        {
            long id = MyAPIGateway.Session.Player.IdentityId;
            foreach (var gps in MyAPIGateway.Session.GPS.GetGpsList(id))
            {
                MyAPIGateway.Session.GPS.RemoveGps(id, gps);
            }
        }

        protected override void UnloadData()
        {
            Logger.Debug("Closing...");
            Logger.Terminate();
            base.UnloadData();
        }

        public void FindAllDrones()
        {
            Logger.Debug("-------------------scanning for drones----------------------");
            HashSet<IMyEntity> entities = new HashSet<IMyEntity>();
            //List<IMyPlayer> players = new List<IMyPlayer>();

            try
            {
                MyAPIGateway.Entities.GetEntities(entities, x => x is IMyCubeGrid && !navy.HasFighterEntity(x) && !drones.HasFighterEntity(x) && (x.Physics!=null && x.Physics.Mass > 100));
                //MyAPIGateway.Players.GetPlayers(players);
            }
            catch (Exception e)
            {
                Logger.LogException(e);
                return;
            }
            

            //filter out any grids that are already accounted for
            foreach (IMyEntity entity in entities)
            {
                if (!entity.Transparent)
                {
                    SetUpDrone(entity);
                }
            }
        }

        private void SetUpDrone(IMyEntity entity)
        {
            Logger.Debug("SetUpDrone");
            Sandbox.ModAPI.IMyGridTerminalSystem gridTerminal = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid((IMyCubeGrid)entity);
            List<Sandbox.ModAPI.Ingame.IMyTerminalBlock> T = new List<Sandbox.ModAPI.Ingame.IMyTerminalBlock>();
            gridTerminal.GetBlocksOfType<IMyTerminalBlock>(T);

            var droneType = IsDrone(T);
            Logger.Debug("Setting up " + droneType);
            try
                {
                Logger.Debug("Found Drone of type: " + droneType);

                switch (droneType)
                    {
                        case ShipTypes.NavyFighter:
                        {
                            Ship ship = new Ship((IMyCubeGrid)entity, navy.PlayerId, navy.LogPath);
                            Logger.Debug("[SetUpDrone] Found New Pirate Ship. id=" + ship.GetOwnerId());
                            
                            navy.AddFighterToFleet(ship, droneType);
                           // AddDiscoveredShip(ship);
                            break;
                        }

                    case ShipTypes.AIDrone:
                        {
                            Ship ship = new Ship((IMyCubeGrid)entity, drones.PlayerId, drones.LogPath);
                            Logger.Debug("[SetUpDrone] Found New Pirate Ship. id=" + ship.GetOwnerId());
                            drones.AddFighterToFleet(ship, droneType);
                            //AddDiscoveredShip(ship);
                            break;
                        }
                    case ShipTypes.NavyFrigate:
                        {
                            Ship ship = new Ship((IMyCubeGrid)entity, navy.PlayerId, navy.LogPath);
                            Logger.Debug("[SetUpDrone] Found New Pirate Ship. id=" + ship.GetOwnerId());

                            navy.AddShipToFleet(ship, droneType);
                            // AddDiscoveredShip(ship);
                            break;
                        }

                    case ShipTypes.AILeadShip:
                        {
                            Ship ship = new Ship((IMyCubeGrid)entity, drones.PlayerId, drones.LogPath);
                            Logger.Debug("[SetUpDrone] Found New Pirate Ship. id=" + ship.GetOwnerId());
                            drones.AddShipToFleet(ship, droneType);
                            //AddDiscoveredShip(ship);
                            break;
                        }
                }
                }
                catch (Exception e)
                {
                    Logger.LogException(e);
                }

        }

        private string navydrone = "#NavyDrone#";
        private string aidrone = "#AIDrone#";
        private string navydronel = "#LargeNavyDrone#";
        private string aidronel = "#LargeAIDrone#";


        private ShipTypes IsDrone(List<Sandbox.ModAPI.Ingame.IMyTerminalBlock> T)
        {

            if (T.Exists(x => ((x).CustomName.Contains(navydronel) && x.IsWorking)))
            {
                Logger.Debug(" is a navy drone!");


                return ShipTypes.NavyFrigate;
            }
            if (T.Exists(x => ((x).CustomName.Contains(aidronel) && x.IsWorking)))
            {
                Logger.Debug(" is an ai drone!");


                return ShipTypes.AILeadShip;
            }

            if (T.Exists(x => ((x).CustomName.Contains(navydrone) && x.IsWorking)))
            {Logger.Debug(" is a navy drone!");

                return ShipTypes.NavyFighter;
            }
            if (T.Exists(x => ((x).CustomName.Contains(aidrone) && x.IsWorking)))
            {Logger.Debug(" is an ai drone!");

                return ShipTypes.AIDrone;
            }
            

            return ShipTypes.NotADrone;

        }


        private void Setup()
        {
            _initalized = true;
            Logger.Init();
            Logger.Debug("Test startup");
            ////_lastSaveTime = DateTime.Now;
            //TestExecutor.SpawnShip(ShipTypes.AIDrone, new Vector3D(0, 0, 0), drones.PlayerId);
            //TestExecutor.SpawnShip(ShipTypes.NavyFighter, new Vector3D(100, 100, 100), navy.PlayerId);

        }
    }
}
