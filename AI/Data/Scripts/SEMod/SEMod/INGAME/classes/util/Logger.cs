using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using VRage.Game;
using VRage.Game.ModAPI.Ingame;
using VRageMath;
using SpaceEngineers.Game.ModAPI.Ingame;
using SEMod.INGAME.classes.systems;
using SEMod.INGAME.classes.model;
using static SEMod.INGAME.classes.implementations.MiningBase;

namespace SEMod.INGAME.classes
{

//////
    public class Logger
    {
        public Logger(IMyCubeGrid grid)
        {
            Debug("Logger Set Up!");
        }

        public Logger(IMyCubeGrid grid, ShipComponents components)
        {
            this.grid = grid;
            this.components = components;
        }

        int loglimit = 25;
        private List<IMyTextPanel> textPanels;
        private ShipComponents components;
        private List<String> debug = new List<String>();
        private List<String> error = new List<String>();
        private IMyCubeGrid grid;

        public void Debug(String m)
        {
            debug.Add(m);

            if (debug.Count > loglimit)
                debug.Remove(debug[0]);
        }

        public void Error(String m)
        {
            error.Add(m);

            if (error.Count > loglimit)
                error.Remove(error[0]);
        }

        public void DisplayLogScreens()
        {
            var debugScreens = components.TextPanels.Where(x => x.CustomName.Contains("#debug#")).ToList();
            var errorScreens = components.TextPanels.Where(x => x.CustomName.Contains("#error#")).ToList();

            UpdateLCD(debug, debugScreens, "Debug Messages");
            UpdateLCD(error, errorScreens, "Error Messages");
        }

        public void DisplayTargets(List<TrackedEntity> trackedEntities)
        {
            var targetsScreens = components.TextPanels.Where(x => x.CustomName.Contains("#targets#")).ToList();
            UpdateTrackedEntitiesScreens(targetsScreens, trackedEntities, grid);
        }

        public void UpdateDisplays(List<IMyTextPanel> _textpanels, List<DroneInfo> drones, List<Order> orders, List<TrackedEntity> trackedEntities, IMyCubeGrid grid)
        {
            textPanels = _textpanels;
            var debugScreens = textPanels.Where(x => x.CustomName.Contains("#debug#")).ToList();
            var targetsScreens = textPanels.Where(x => x.CustomName.Contains("#targets#")).ToList();
            var errorScreens = textPanels.Where(x => x.CustomName.Contains("#error#")).ToList();

            if (orders != null)
            {
                var fleetScreens = textPanels.Where(x => x.CustomName.Contains("#fleet#")).ToList();
                
            }
            //update debug screen
            UpdateLCD(debug, debugScreens, "Debug Messages");
            UpdateLCD(error, errorScreens, "Error Messages");
            UpdateTrackedEntitiesScreens(targetsScreens, trackedEntities, grid);
            

        }

        private int lastInfoIndex = 0;
        private int maxInfoLength = 10;
        internal void DisplayShipInfo(Dictionary<string, object> infoKeys, string title = "Ship Info")
        {
            var Cameras = components.Cameras.Count();
            var Connectors = components.Connectors.Count();
            var ControlUnits = components.ControlUnits.Count();
            var Gyros = components.Gyros.Count();
            var MergeBlocks = components.MergeBlocks.Count();
            var MiningDrills = components.MiningDrills.Count();
            var Thrusters = components.Thrusters.Count();

            List<String> lines = new List<string>();
            lines.Add(" Cameras: " + Cameras);
            lines.Add(" Connectors: " + Connectors);
            lines.Add(" ControlUnits: " + ControlUnits);
            lines.Add(" Gyros: " + Gyros);
            lines.Add(" MergeBlocks: " + MergeBlocks);
            lines.Add(" MiningDrills: " + MiningDrills);
            lines.Add(" Thrusters: " + Thrusters);

            foreach (var obj in infoKeys)
                lines.Add(" "+obj.Key+": "+obj.Value);

            //if (lastInfoIndex >= lines.Count()) lastInfoIndex = 0;

            //var nextLastIndex = lastInfoIndex + maxInfoLength;

            //if (nextLastIndex > lines.Count()) nextLastIndex = lines.Count() - 1;
            
            var lcds = components.TextPanels.Where(x => x.CustomName.Contains("#info#")).ToList();
            //var dLines = lines.GetRange(lastInfoIndex, nextLastIndex);

            //lastInfoIndex = nextLastIndex;

            UpdateLCD(lines, lcds, " "+title +" "+ grid.CustomName);

        }

        public void UpdateRegionInfo(List<Region> regions, IMyCubeGrid grid)
        {
            List<string> lines = new List<string>();

            foreach (var ent in regions.OrderBy(x => (x.surfaceCenter - grid.GetPosition()).Length()/ x.GetScanDensity()).Take(5))
            {
                var near = ent.GetNearestPoint(grid.GetPosition());
                var distance = (int)(near - grid.GetPosition()).Length();
                lines.Add(" size: " + ent.PointsOfInterest.Count() + "   distance: " + (int)distance + "m\n" +
                    "       --last scanned: " + (int)(DateTime.Now - ent.LastUpdated).TotalSeconds+"s " +
                    "  scan density: "+ent.GetScanDensity()+"\n" +
                    "       --surface scan coverage: "+ent.GetPercentReached());
            }
            lines.Add(" Number of Detected Regions: "+regions.Count());
            var screens = components.TextPanels.Where(x => x.CustomName.Contains("#planets#")).ToList();
            UpdateLCD(lines, screens, "Regions");
        }

        private void UpdateTrackedEntitiesScreens(List<IMyTextPanel> targetsScreens, List<TrackedEntity> trackedEntities, IMyCubeGrid grid)
        {
            List<string> lines = new List<string>();
            foreach (var ent in trackedEntities.OrderBy(x => x.Relationship))
            {
                var near = ent.GetNearestPoint(grid.GetPosition());
                var distance = (int)(near - grid.GetPosition()).Length();
                var lastfour = (ent.EntityID + "");
                lastfour = lastfour.Substring(lastfour.Length - 4);
                lines.Add(lastfour + "  " + ent.Type + "  " + ent.name + " " + near + " " + ent.Relationship + "  " + (int)distance + "m" + "  " + (int)(DateTime.Now - ent.LastUpdated).TotalSeconds);
            }

            UpdateLCD(lines, targetsScreens, "Tracked Targets");
        }

        public void UpdateFleetInformationScreens(List<DroneContext> droneContexts, IMyCubeGrid grid)
        {
            List<IMyTextPanel> fleetScreens = components.TextPanels.Where(x => x.CustomName.Contains("#fleet#")).ToList();
            var droneinfos = "";
            var drones = droneContexts.Select(x => x.Info).ToList();
            var orders = droneContexts.Where(x=>x.Order!=null).Select(x => x.Order).ToList();
            foreach (var drone in drones)
            {
                var dronetype = (drone.NumDrills > 0 ? "M" : " ") + (drone.NumConnectors > 0 ? "D" : " ") + (drone.numSensors > 0 ? "S" : " ");
                var distance = (int)(drone.lastKnownPosition - grid.GetPosition()).Length();
                int velocity = (int)(Math.Abs(drone.LastKnownVector.X) + Math.Abs(drone.LastKnownVector.Y) + Math.Abs(drone.LastKnownVector.Z));

                droneinfos += "| " + drone.EntityId + "   |  " + dronetype + "     |    " + drone.numSensors + "    |      " + drone.CameraCount + "      |    " + drone.NumDrills + " |       " + drone.NumWeapons + "       |    " + drone.PercentCargo + "    |    " + velocity + "/" + distance + " | "+(drone.Docked?"Docked":"un-docked")+"\n";
            }
            if (drones.Count == 0)
                droneinfos = "| No Drone Info |";

            var missioninfos = "";
            foreach (var order in orders)
            {
                var drone = drones.FirstOrDefault(x => x.EntityId == order.DroneId);

                var distancefromDrone = "";
                var distancefromCC = "";

                if (drone != null)
                {
                    distancefromDrone = (int)(drone.lastKnownPosition - order.PrimaryLocation).Length() + "";
                    distancefromCC = (int)(grid.GetPosition() - order.PrimaryLocation).Length() + "";
                }


                int velocity = (int)(Math.Abs(drone.LastKnownVector.X) + Math.Abs(drone.LastKnownVector.Y) + Math.Abs(drone.LastKnownVector.Z));

                missioninfos += "| " + drone.EntityId + "   |  " + order.Ordertype + "     |    " + (int)((DateTime.Now - order.IssuedAt).TotalSeconds) + "    |      " + order.Confirmed + "      |       " + distancefromCC + "      |      " + distancefromDrone + "      |\n";
            }
            if (drones.Count == 0)
                missioninfos = "| No Mission Info |";

            var scanorders = orders.Where(x => x.Ordertype == OrderType.Scan).Count();

            var fleet = new List<string> {"| Fleet Information |",
                                              "| Num Drones "+ "| Active Orders  |",
                                              "|     "+drones.Count+"     "+ "|       "+orders.Count+"      |\n",
                                              "| Drone Information |",
                                              "|          Drone Id              |   Type | Sensors | Cameras | Drills | Weapons | cargo | Vel/Dis",
                                              droneinfos,
                                              "| Outstanding Orders |",
                                              "|          Drone Id              | Order Type "+ "| Mission Time "+ "| Confirmed | Target-D | Drone-D |",
                                              missioninfos};

            UpdateLCD(fleet, fleetScreens, "");
        }

        public void UpdateLCD(List<string> logs, List<IMyTextPanel> lcds, string headerString)
        {
            String str = "";
            int index = 0;

            foreach (var strin in logs)
            {
                if (index < loglimit)
                    str += strin + "\n";
                else continue;
            }

            if(lcds!=null)
            foreach (var screen in lcds)
                screen.WritePublicText(headerString + "\n" + DateTime.Now + "\nLogcount: " + logs.Count() + "\n" + str);
        }

        internal void UpdateAltitudeLCD(double altitude, List<IMyTextPanel> textPanels)
        {
            var screens = textPanels.Where(x => x.CustomName.Contains("#altitude#")).ToList();
            if(screens!=null && screens.Count() > 0)
            {
                screens.First().WritePublicText(" Altitude:\n "+altitude+"");
            }
        }
    }
    //////
}
