using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using VRage.Game;
using VRage.Game.ModAPI.Ingame;
using VRageMath;
using SpaceEngineers.Game.ModAPI.Ingame;
using SEMod.INGAME.classes;
using SEMod.INGAME.classes.systems;
using SEMod.INGAME.classes.model;

namespace SEMod.INGAME.classes.implementations
{
    class AIShipBase
    {
        IMyGridProgramRuntimeInfo Runtime;
        IMyProgrammableBlock Me = null;
        IMyGridTerminalSystem GridTerminalSystem = null;
        
        //////

        protected LinkedList<TaskInfo> operatingOrder = new LinkedList<TaskInfo>();

        protected Logger log;

        protected CommunicationSystem communicationSystems;
        protected NavigationSystem navigationSystems;
        protected ProductionSystem productionSystems;
        protected ShipComponents shipComponents;
        protected StorageSystem storageSystem;
        protected TrackingSystem trackingSystems;
        protected WeaponSystem weaponSystems;

        protected Dictionary<String, object> shipInfoKeys = new Dictionary<string, object>();

        //configuration variables
        protected string updateArg = "run";
        protected int sensorScansPerSecond = 2;
        protected int hoverHeight = 900;

        //changing variables
        protected int lastOperationIndex = 0;
        protected DateTime lastReportTime = DateTime.Now;
        protected long messagesRecieved = 0;

        

        protected void LocateAllParts()
        {
            shipComponents.Sync(GridTerminalSystem, Me.CubeGrid);
        }

        protected Dictionary<String, bool> breakPoints = new Dictionary<string, bool>();
        protected bool RunEveryOther(string pointname)
        {
            if (!breakPoints.Keys.Contains(pointname))
                breakPoints.Add(pointname, true);

            bool returnVal = !breakPoints[pointname];
            breakPoints[pointname] = returnVal;
            return returnVal;
        }

        protected void Update()
        {
            if (RunEveryOther("Update"))
                return;

            RunNextOperation();
            communicationSystems.Update();
        }

        protected void RunNextOperation()
        {
            if (lastOperationIndex == operatingOrder.Count())
                lastOperationIndex = 0;

            long msStart = DateTime.Now.Ticks;
            TaskInfo info = operatingOrder.ElementAt(lastOperationIndex);
            info.CallMethod();

            long msStop = DateTime.Now.Ticks;
            long timeTaken = msStop - msStart;

            //info.AddResult(new TaskResult(timeTaken, 0, (Runtime.CurrentInstructionCount/ Runtime.MaxInstructionCount) *100, (Runtime.CurrentCallChainDepth/ Runtime.MaxCallChainDepth) * 100));
            //info.AddResult(new TaskResult(timeTaken, 0, Runtime.MaxInstructionCount, Runtime.MaxCallChainDepth));
            //info.AddResult(new TaskResult(timeTaken, 0, Runtime.CurrentInstructionCount, Runtime.CurrentCallChainDepth));
            info.AddResult(new TaskResult(timeTaken, 0, Runtime.CurrentInstructionCount/40000, Runtime.CurrentCallChainDepth/40000));
            lastOperationIndex++;
        }

        protected void InternalSystemScan()
        {
            try
            {
                var cs = communicationSystems.IsOperational();
                var ns = navigationSystems.IsOperational();
                var ps = productionSystems.IsOperational();
                var ss = storageSystem.IsOperational();
                var ts = trackingSystems.IsOperational();
                var ws = weaponSystems.IsOperational();

                UpdateInfoKey("WeaponSystems", BoolToOnOff(ws) + "");
                UpdateInfoKey("CommunicationSystems", BoolToOnOff(cs) + "");
                UpdateInfoKey("NavigationSystems", BoolToOnOff(ns) + "");
                UpdateInfoKey("TrackingSystems", BoolToOnOff(ts) + "");
                UpdateInfoKey("ProductionSystems", BoolToOnOff(ps) + "");
                UpdateInfoKey("StorageSystem", BoolToOnOff(ss) + "");

                navigationSystems.Update();
                
            }
            catch (Exception e ) { log.Error("InternalSystemScan " + e.Message); }
        }

        protected string BoolToOnOff(bool conv)
        {
            return conv ? "Online" : "Offline";
        }

        protected void SensorScan()
        {
            try
            {
                ScanWithSensors();
                ScanWithCameras();
            }
            catch (Exception e) { log.Error("SensorScan " + e.Message); }
        }


        protected void ScanWithSensors()
        {
            var miliseconds = (DateTime.Now - lastReportTime).TotalMilliseconds;
            if (miliseconds >= 1000 / sensorScansPerSecond)
            {
                lastReportTime = DateTime.Now;
                var foundentities = new Dictionary<long, String>();
                foreach (var sensor in shipComponents.Sensors)
                {
                    sensor.DetectEnemy = true;
                    sensor.DetectPlayers = true;
                    sensor.DetectLargeShips = true;
                    sensor.DetectSmallShips = true;
                    sensor.DetectOwner = false;
                    sensor.DetectStations = true;
                    sensor.DetectAsteroids = true;

                    var ent = sensor.LastDetectedEntity;//LastDetectedEntity; 

                    if (ent.EntityId != 0)
                    {
                        String EntityInformation = ParsedMessage.BuildPingEntityMessage(ent, Me.CubeGrid.EntityId, communicationSystems.GetMsgSntCount());
                        //communicationSystems.SendMessage(EntityInformation);
                        if (!foundentities.Keys.Contains(ent.EntityId))
                            foundentities.Add(ent.EntityId, EntityInformation);

                        ParseMessage(EntityInformation, true);
                    }
                }
                foreach (var entity in foundentities)
                    communicationSystems.SendMessage(entity.Value);
            }
        }

        protected int pitch = 0;
        protected int yaw = 0;
        protected int range = 0;
        protected int maxCameraRange = 2000;
        protected int maxCameraAngle = 90;

        protected void ScanWithCameras()
        {
            var foundentities = new Dictionary<long, String>();
            foreach (var camera in shipComponents.Cameras)
            {
                var maxAngle = maxCameraAngle;
                    //== 0 ? camera.RaycastConeLimit : maxCameraAngle;
                var maxRange = maxCameraRange;
                    //== 0? camera.RaycastDistanceLimit: maxCameraRange;
                if (!camera.EnableRaycast)
                    camera.EnableRaycast = true;

                var timeToScan = camera.TimeUntilScan(range);

                if (timeToScan <= 0)
                {
                    pitch -= 5;

                    if (pitch <= -maxAngle)
                    {
                        pitch = pitch * -1;
                        yaw -= 5;
                        //log.Debug("flipping pitch");
                    }
                    if (yaw <= -maxAngle)
                    {
                        yaw = yaw * -1;
                        range -= 500;
                        //log.Debug("flipping yaw");
                    }
                    if (range <= 1)
                    {
                        range = maxCameraRange;
                       // log.Debug("flipping range");
                    }

                    
                    //var ent = camera.Raycast(range, pitch, yaw); 
                    var ent = camera.Raycast(range, pitch, yaw);
                    //log.Debug("Scanning Raycast: \nrange:pitch:yaw " + range + ":" + pitch + ":" + yaw);

                    if (ent.EntityId != 0)
                    {
                        String EntityInformation = ParsedMessage.BuildPingEntityMessage(ent, Me.CubeGrid.EntityId, communicationSystems.GetMsgSntCount());
                        ParseMessage(EntityInformation, true);
                        //log.Debug("Entity Found: "+ ent.Type);
                        if (!foundentities.Keys.Contains(ent.EntityId))
                            foundentities.Add(ent.EntityId, EntityInformation);
                    }
                }
            }
            foreach (var entity in foundentities)
                communicationSystems.SendMessage(entity.Value);
        }

        protected void ParseMessage(string argument, bool selfCalled=false)
        {
            if (argument == null)
                return;

            var pm = communicationSystems.ParseMessage(argument);

            if (ParsedMessage.MaxNumBounces < pm.NumBounces && !selfCalled && pm.MessageType != MessageCode.PingEntity)
            {
                pm.NumBounces++;
                //LOG.Debug("Bounced Message");
                communicationSystems.SendMessage(pm.ToString());
            }

            if (pm.IsAwakeningSignal)
                ForceStartup();
            else
                switch (pm.MessageType)
                {
                    case MessageCode.PingEntity:
                        if (pm.Type.Trim().ToLower().Contains("planet"))
                            trackingSystems.UpdatePlanetData(pm, selfCalled);
                        else
                            trackingSystems.UpdateTrackedEntity(pm, selfCalled);

                        break;
                }
        }

        protected void AnalyzePlanetaryData()
        {
            try
            {
                trackingSystems.UpdatePlanetData();
            }
            catch (Exception e) { log.Error("AnalyzePlanetaryData " + e.Message); }
        }

        internal PlanetaryData NearestPlanet = null;
        

        protected void UpdateTrackedTargets()
        {
            try
            {
                log.DisplayTargets(trackingSystems.getTargets());
            }
            catch (Exception e) { log.Error("UpdateTrackedTargets " + e.Message); }
        }

        protected void UpdateInfoKey(string name, string value)
        {
            if (shipInfoKeys.Keys.Contains(name))
                shipInfoKeys.Remove(name);
            
                shipInfoKeys.Add(name, value);
        }

        protected double GetCargoMass()
        {
            double mass = 0;
            List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocks(blocks);
            for (int i = 0; i < blocks.Count; i++)
            {
                var count = blocks[i].GetInventoryCount(); // Multiple inventories in Refineriers, Assemblers, Arc Furnances.
                for (var inv = 0; inv < count; inv++)
                {
                    var inventory = blocks[i].GetInventory(inv);
                    if (inventory != null) // null means, no items in inventory.
                        mass += (double)inventory.CurrentMass;
                }
            }
            return mass;
        }

        protected int Mass = 0;
        protected void UpdateDisplays()
        {
            try
            {

                Mass = (int)(GetCargoMass() + shipComponents.AllBlocks.Sum(x=>x.Mass));
                var controlBlock = shipComponents.ControlUnits.FirstOrDefault();
                if (controlBlock != null) {
                    var maxMass = (int)shipComponents.Thrusters.Where(x => x.WorldMatrix.Forward == controlBlock.WorldMatrix.Forward).Sum(x=>x.MaxThrust)/(controlBlock.GetNaturalGravity().Length());
                    UpdateInfoKey("Weight Information"," Mass: " + Mass + "kg  MaxMass: " +(int)maxMass+"kg");
                }

                //display operation details
                foreach (var op in operatingOrder)
                    UpdateInfoKey(op.CallMethod.Method.Name + "", ((int)op.GetAverageExecutionTime() + "ms"+ " CallCountPerc: "+op.GetAverageCallCount() + "% CallDepthPer: " + op.GetAverageCallCount()+"%"));
                //UpdateInfoKey("Thruster Data","N used: "+navigationSystems.CurrentThrustPower+ " N avail: " + navigationSystems.CurrentThrustPower + "mass lifted: " + Mass);

                if (NearestPlanet != null)
                {
                    log.DisplayShipInfo(shipInfoKeys, "PlanetInfo:  altitude: " + (int)trackingSystems.GetAltitude() + "m" + "  Speed: " + navigationSystems.GetSpeed() + "m/s");
                    log.UpdateRegionInfo(NearestPlanet.Regions, Me.CubeGrid);
                }
                else
                    log.DisplayShipInfo(shipInfoKeys, " No Planet ");
            }
            catch (Exception e) {log.Error("UpdateDisplays " + e.Message); }

            log.DisplayLogScreens();
            UpdateAntenna();
        }

        protected void UpdateAntenna()
        {
            foreach (var antenna in shipComponents.RadioAntennas)
            {
                antenna.CustomName = "\nA: "+(int)trackingSystems.GetAltitude()+"\n"+
                    "S: "+(int)navigationSystems.GetSpeed();
            }
        }

        protected void ForceStartup()
        {
            var timer = GridTerminalSystem.GetBlockWithName("#RS#");
            timer.ApplyAction("TriggerNow");
        }
        //////
    }
}
