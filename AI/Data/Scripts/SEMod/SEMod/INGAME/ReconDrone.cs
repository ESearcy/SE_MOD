using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame;
using VRage.ModAPI;
using VRageMath;
using Sandbox.Game.Entities.Cube;
using Sandbox.ModAPI.Interfaces;
using VRage.Game;
using VRage.Utils;

namespace SEMod
{
    class ReconDrone
    {

        IMyGridTerminalSystem GridTerminalSystem = null;
        public void Program()
        {
            LocateParts();

            var connector = _connectors.FirstOrDefault(x => x.Status == MyShipConnectorStatus.Connected);
            Docked = connector != null;

            LOG = new Logger(_textPanels, grid);
        }
        private bool enableHalfSpeedUpdates = true;
        private bool ranLastTik = false;
        IMyCubeGrid grid = null;

        List<IMyTextPanel> _textPanels = new List<IMyTextPanel>();
        List<IMySensorBlock> _sensors = new List<IMySensorBlock>();
        List<IMyCameraBlock> _cameras = new List<IMyCameraBlock>();
        List<IMyCubeGrid> _knowngrids = new List<IMyCubeGrid>();
        List<IMyProgrammableBlock> _programBlocks = new List<IMyProgrammableBlock>();
        List<IMyRadioAntenna> _radioAntennas = new List<IMyRadioAntenna>();
        List<IMyLaserAntenna> _laserAntennas = new List<IMyLaserAntenna>();
        List<IMyOreDetector> _oredetectors = new List<IMyOreDetector>();
        List<IMyRemoteControl> _controlUnits = new List<IMyRemoteControl>();
        List<IMyShipConnector> _connectors = new List<IMyShipConnector>();
        List<IMyShipDrill> _miningDrills = new List<IMyShipDrill>();
        List<IMyThrust> _thrusters = new List<IMyThrust>();
        List<IMyGyro> _gyros = new List<IMyGyro>();

        List<IMySmallGatlingGun> _gatlingGuns = new List<IMySmallGatlingGun>();
        List<IMySmallMissileLauncher> _rocketLaunchers = new List<IMySmallMissileLauncher>();

        List<String> PendingMessages = new List<String>();
        MessageType currentOrder = MessageType.Unknown;
        DateTime lastReportTime = DateTime.Now.AddSeconds(-20);
        Vector3D OrderLocation = Vector3D.Zero;
        private DockingOrder CurrentDockingrequest;
        private MiningOrder CurrentMiningRequest;
        NavigationControls nav;
        int loglimit = 10;
        Logger LOG;
        bool BootedUp = false;
        bool RegisteredWithCommandShip = false;
        private String lastmessageOnHold = null;
        private String updateString = "run";
        int ScansPerSecond = 2;
        int secondsToUpdate = 3;
        int minutesToRegister = 2;
        int messagesSent = 0;

        DateTime lastRegisterAttempt = DateTime.Now.AddMinutes(-2);
        DateTime lastUpdateAttempt = DateTime.Now.AddMinutes(-2);
        private long ShipEntityId;

        double distanceToNavTarget = 0;
        private int avoidanceRange = 300;
        private int slowdownRange = 1000;
        private Vector3D avoidanceVector = Vector3D.Zero;
        private int MaxSpeed = 103;
        Vector3D AvoidanceVector = Vector3D.Zero;
        private bool online = false;
        private bool Docked = false;
        private int shipSize = 10;
        private DateTime lastUpdate = DateTime.Now.AddMinutes(-1);
        private string avoidancePriorities = "N";

        public void Main(String argument)
        {
            try
            {
                LOG.UpdateLCDPanels();
                UpdateInfoScreen();
                LOG.UpdateEntityStatus(trackedEntities);

                if (argument.Equals(updateString))
                {
                    if (grid != null)
                        LocateParts();

                    if (grid == null)
                        BootUp();


                    if (IsOperational() && grid != null)
                    {
                        TimedUpdate();
                    }
                }
                else
                {
                    ParseMessage(argument);
                }
            }
            catch (Exception e)
            {
                LOG.Error(e.ToString());
            }
        }

        public void TimedUpdate()
        {
            //see if docked 
            {
                var dockedConnector = _connectors.FirstOrDefault(x => x.Status == MyShipConnectorStatus.Connected);

                if (dockedConnector != null)
                {
                    LOG.Debug("Ship Docked");
                    Docked = true;
                    if (CurrentDockingrequest != null)
                    {
                        CurrentDockingrequest = null;
                        currentOrder = MessageType.Unknown;
                        Docked = true;
                    }
                }
                else
                    Docked = false;
            }

            if ((DateTime.Now - lastUpdate).TotalSeconds >= .5)
            {
                //send as many pending messages as possible 
                nav.Update(Docked, _gyros, _thrusters);
                lastUpdate = DateTime.Now;
            }
            else
                Update();

            if (!Docked)
                nav.UpdateSpeed();

            int NumberMessagesSent = AttemptSendPendingMessages();
        }

        public void ExecuteFlyToOrder(Boolean avoid)
        {
            //LOG.Debug("avoiding"); 
            var normalOrder = grid.GetPosition() - OrderLocation;
            distanceToNavTarget = Math.Abs(normalOrder.Length());
            normalOrder.Normalize();
            if (avoid)
            {
                nav.AlignTo(grid.GetPosition() + avoidanceVector);// - (normalOrder + avoidanceVector)); 
                nav.Approach(grid.GetPosition() + avoidanceVector, MaxSpeed);// - (normalOrder + avoidanceVector), MaxSpeed); 
            }
            else
            {
                nav.AlignTo(OrderLocation);
                nav.Approach(OrderLocation, MaxSpeed);
            }
        }

        Vector3D lastDockLoc = Vector3D.Zero;
        public void ExecuteDockOrder(Boolean avoid)
        {
            //var alignUp = grid.GetPosition() + (CurrentDockingrequest.UpVector*2); 
            foreach (var drill in _miningDrills)
            {
                drill.GetActionWithName("OnOff_Off").Apply(drill);
            }

            var connector = _connectors.Where(x => x.Status != MyShipConnectorStatus.Connected).FirstOrDefault();

            if (connector != null && !Docked)
            {
                if (CurrentDockingrequest.DockingCourse.Count() > 1)
                {
                    
                    if (lastDockLoc == Vector3D.Zero)
                        lastDockLoc = CurrentDockingrequest.DockingCourse.First();

                    distanceToNavTarget = Math.Abs((lastDockLoc - grid.GetPosition()).Length());
                    var normalOrder = grid.GetPosition() - lastDockLoc;

                    if (distanceToNavTarget > .2)
                    {
                        var alignTo = grid.GetPosition() + CurrentDockingrequest.ForwardVector * 100;
                        nav.AlignTo(alignTo);

                        nav.Approach(grid.GetPosition() - normalOrder, 1);
                    }
                    else
                    {
                        CurrentDockingrequest.DockingCourse.RemoveFirst();
                        lastDockLoc = CurrentDockingrequest.DockingCourse.First();
                    }
                }
                else if (CurrentDockingrequest.DockingCourse.Count() == 1 || CurrentDockingrequest.DockingCourse.Count() == 0)
                {
                    connector.GetActionWithName("OnOff_On").Apply(connector);

                    if(CurrentDockingrequest.DockingCourse.Count() == 1)
                    {
                        CurrentDockingrequest.DockingCourse.RemoveFirst();
                        lastDockLoc = CurrentDockingrequest.DockingCourse.First();
                    }

                    if (connector.Status == MyShipConnectorStatus.Connectable)
                    {
                        
                        CurrentDockingrequest = null;
                        currentOrder = MessageType.Unknown;
                        connector.Connect();
                        Docked = true;
                        nav.Update(Docked, _gyros, _thrusters);
                    }
                    else
                    {
                        var normalOrder = grid.GetPosition() - lastDockLoc;
                        nav.Approach(grid.GetPosition() - normalOrder, 1);
                        var alignTo = grid.GetPosition() + CurrentDockingrequest.ForwardVector * 100;
                        nav.AlignTo(alignTo);
                        nav.Roll(.11f);

                    }
                }
                if(connector.Status == MyShipConnectorStatus.Connected)
                {
                    CurrentDockingrequest = null;
                    currentOrder = MessageType.Unknown;
                    Docked = true;
                    nav.Update(Docked, _gyros, _thrusters);
                }
            }
            else
                LOG.Debug("cant dock: " + connector + " : " + Docked);

        }

        public void ExecuteMiningOrder(Boolean avoid)
        {
            var minepos = CurrentMiningRequest.Location;

            var distance = Math.Abs((grid.GetPosition() - minepos).Length());
            var alignTo = CurrentMiningRequest.ForwardVector;
            distanceToNavTarget = distance;

            var normalOrder = grid.GetPosition() - CurrentMiningRequest.Location;
            //if (avoid)
            //{
            //    nav.AlignTo(alignTo - (normalOrder + avoidanceVector)); 
            //    nav.Approach(grid.GetPosition() - avoidanceVector, MaxSpeed);// - (normalOrder + avoidanceVector), MaxSpeed); 
            //}
            //else
            {
                nav.AlignTo(alignTo);
                nav.Roll(5f);
                nav.Approach(grid.GetPosition() - normalOrder, 3);
            }

            //LOG.Debug("Dock multi: " + CurrentDockMultiplier + " distance: " + distance); 
            if (distance < .5)
            {
                nav.SlowDown();
            }

            foreach (var drill in _miningDrills)
            {
                if (IsAsteroidInSensorRange() || distance < 200)
                    drill.GetActionWithName("OnOff_On").Apply(drill);
                else
                    drill.GetActionWithName("OnOff_Off").Apply(drill);
            }
        }

        DateTime lastBroadcast = DateTime.Now;
        private void Update()
        {
            if (enableHalfSpeedUpdates)
            {
                if (ranLastTik)
                {
                    ranLastTik = false;
                    return;
                }
                else
                    ranLastTik = true;
            }

            if ((DateTime.Now - lastBroadcast).TotalMinutes > 1)
            {
                BroadcastTargets();
                lastBroadcast = DateTime.Now;
            }

            online = true;

            if (!RegisteredWithCommandShip)
            {
                LOG.Debug("Ship Not Connected to a CC");
                nav.SlowDown();
                return;
            }

            ScanAndReport();
            ScanWithCameras();

            var avoid = CalculateAvoidanceVectorAndSpeed();
            distanceToNavTarget = 0;
            
            //LOG.Debug("Order: " + currentOrder); 

            if (currentOrder == MessageType.FlyToOrder)
            {
                ExecuteFlyToOrder(avoid); 
            }
            else
            if (currentOrder == MessageType.Dock)
            {
                ExecuteDockOrder(avoid);
            }
            else
            if (currentOrder == MessageType.Mining)
            {
                ExecuteMiningOrder(avoid);
            }
            else if (currentOrder == MessageType.Unknown)
            {
                //LOG.Debug("
                "); 
                nav.SlowDown();
                nav.StopSpin();
            }

            CalculateCargo();
            String shipDetails = "C: " + CargoCapacityUsage + "%\nV: " + (int)nav.GetSpeed() + "\nD: " + (int)distanceToNavTarget;

            foreach (var antenna in _radioAntennas)
            {
                //avoidanceVector != Vector3D.Zero ? "Avoiding\n" + shipDetails :  
                antenna.CustomName = currentOrder + "\n" + shipDetails;
            }

        }

        int CargoCapacityUsage = 0;

        public void CalculateCargo()
        {
            List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocks(blocks);
            double maxCargo = 0;
            double cargoUsed = 0;

            foreach (var block in blocks)
            {
                if (block.HasInventory)
                {
                    var inv = block.GetInventory();

                    maxCargo += inv.MaxVolume.RawValue / 1000000000f;
                    cargoUsed += inv.CurrentMass.RawValue / 1000000000f;
                    
                }
            }
            CargoCapacityUsage = (int)(cargoUsed / maxCargo * 100);
        }

        public bool CalculateAvoidanceVectorAndSpeed()
        {
            avoidanceVector = Vector3D.Zero;
            MaxSpeed = 100;

            int count = 0;
            var avoidanceVectorToBe = Vector3D.Zero;

            foreach (var obj in trackedEntities)
            {
                if (avoidancePriorities == "N" || (avoidancePriorities == "S" && obj.name.ToLower().Contains("Asteriod")) || (avoidancePriorities == "A" && !obj.name.ToLower().Contains("Asteriod")))
                    return false;

                var dirToObject = (grid.GetPosition() - obj.Location);
                var dirFromObject = (obj.Location - grid.GetPosition());
                var distance = Math.Abs(dirToObject.Length());


                //max speed depends on whoever is closiest 
                if (distance < slowdownRange)
                {
                    MaxSpeed = (int)Math.Sqrt(distance);
                }

                if (!(distance < slowdownRange + obj.Radius))
                    continue;

                double angle = 180;
                var shipDirection = nav.LinearVelocity;

                if (nav.LinearVelocity != Vector3D.Zero)
                {
                    angle = Math.Abs(NavigationControls.AngleBetween(dirToObject, shipDirection, true));
                    if (obj.Velocity != Vector3D.Zero)
                    {
                        var angle2 = Math.Abs(NavigationControls.AngleBetween(dirFromObject, obj.Velocity, true));
                        if (angle2 < angle)
                            angle = angle2;
                    }
                }
                else
                {
                    continue;
                }

                //LOG.Debug("Angle "+angle); 


                bool ignoreangle = !double.IsNaN(angle);

                LOG.Debug("radius " + obj.Radius);
                LOG.Debug((ignoreangle) + ":" + (angle < 45) + ":" + (distance < avoidanceRange));
                if ((angle < 45) && distance < (avoidanceRange) + obj.Radius)
                {
                    count++;
                    var vector = (grid.GetPosition() - OrderLocation);
                    vector.Normalize();
                    avoidanceVectorToBe = avoidanceVectorToBe - vector;
                }
            }
            avoidanceVector = avoidanceVectorToBe;


            return avoidanceVector != Vector3D.Zero;
        }

        public void ForceStartup()
        {
            var timer = GridTerminalSystem.GetBlockWithName("#RS#");
            if (timer != null)
                timer.ApplyAction("TriggerNow");
        }

        public bool IsAsteroidInSensorRange()
        {
            foreach (var sensor in _sensors)
            {
                sensor.DetectAsteroids = true;

                var ent = sensor.LastDetectedEntity;

                if (!ent.IsEmpty() && ent.Name.ToLower().Contains("asteroid"))
                    return true;
            }
            return false;
        }

        public void ScanAndReport()
        {
            var miliseconds = (DateTime.Now - lastReportTime).TotalMilliseconds;
            if (miliseconds >= 1000 / ScansPerSecond)
            {
                lastReportTime = DateTime.Now;
                foreach (var sensor in _sensors)
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
                        String EntityInformation = ParsedMessage.CreatePingEntityMessage(ent, ShipEntityId, messagesSent);
                        PendingMessages.Add(EntityInformation);
                        ParseMessage(EntityInformation);
                    }
                }
            }
        }

        int pitch = 0;
        int yaw = 0;
        int range = 100;
        int maxCameraRange = 2000;

        public void ScanWithCameras()
        {
            foreach (var camera in _cameras)
            {
                var maxAngle = camera.RaycastConeLimit;
                var maxRange = camera.RaycastDistanceLimit;
                if (!camera.EnableRaycast)
                    camera.EnableRaycast = true;

                var timeToScan = camera.TimeUntilScan(range);


                if (timeToScan <= 0)
                {
                    pitch += 2;

                    if (pitch >= maxAngle)
                    {
                        pitch = pitch * -1;
                        yaw += 2;
                    }
                    if (yaw >= maxAngle)
                    {
                        yaw = yaw * -1;
                        range += 200;
                    }
                    if (maxRange == -1 && range >= maxCameraRange)
                    {
                        range = 100;
                    }
                    else if (maxRange != -1 && range >= maxRange)
                    {
                        range = 100;
                    }
                    LOG.Debug("Scanning Raycast: \nrange:pitch:yaw " + range + ":" + pitch + ":" + yaw);
                    //var ent = camera.Raycast(range, pitch, yaw); 
                    var ent = camera.Raycast(maxCameraRange, pitch, yaw);

                    if (ent.EntityId != 0)
                    {
                        String EntityInformation = ParsedMessage.CreatePingEntityMessage(ent, ShipEntityId, messagesSent);
                        PendingMessages.Add(EntityInformation);
                        ParseMessage(EntityInformation);
                    }
                }
            }
        }

        public void RegisterAndUpdate()
        {
            var secondsSinceRegisterAttempt = (DateTime.Now - lastRegisterAttempt).TotalMinutes;
            if (RegisteredWithCommandShip)
            {
                var seconds = (DateTime.Now - lastUpdateAttempt).TotalSeconds;

                if (seconds >= secondsToUpdate)
                {
                    var connector = _connectors.OrderBy(x => x.Status == MyShipConnectorStatus.Connected).FirstOrDefault();

                    LOG.Debug("Transmitting Update.");
                    lastUpdateAttempt = DateTime.Now;
                    String message = "";
                    if (currentOrder == MessageType.Dock)
                        message = ParsedMessage.CreateUpdateMessage(ShipEntityId, nav.LinearVelocity, connector.GetPosition(), messagesSent, Docked,_cameras.Count, _connectors.Count, _miningDrills.Count, _sensors.Count, _gatlingGuns.Count + _rocketLaunchers.Count, shipSize, CargoCapacityUsage);
                    else
                        message = ParsedMessage.CreateUpdateMessage(ShipEntityId, nav.LinearVelocity, grid.GetPosition(), messagesSent, Docked, _cameras.Count, _connectors.Count, _miningDrills.Count, _sensors.Count, _gatlingGuns.Count + _rocketLaunchers.Count, shipSize, CargoCapacityUsage);

                    PendingMessages.Add(message);
                }


            }
            else if (secondsSinceRegisterAttempt >= minutesToRegister && !RegisteredWithCommandShip)
            {
                LOG.Debug("Sending Registration Request.");
                lastRegisterAttempt = DateTime.Now;
                String message = ParsedMessage.CreateRegisterMessage(ShipEntityId, messagesSent);
                PendingMessages.Add(message);
            }
        }

        private void BroadcastTargets()
        {
            foreach (var ent in trackedEntities)
            {
                PendingMessages.Add(ent.DetailsString);
            }
        }

        public void UpdateInfoScreen()
        {
            if (nav != null)
            {
                if (CurrentDockingrequest == null)
                {
                    LOG.InfoStay("Current Order: " + currentOrder + " Location: " + OrderLocation + "\n Degrees Yaw: " +
                                 (int)nav._degreesToVectorYaw + " Degrees Pitch:" + (int)nav._degreesToVectorPitch +
                                 "\n Registered: " + RegisteredWithCommandShip + "\n MessagesSent: " + messagesSent);
                }
                else
                {
                    LOG.InfoStay("Current Order: " + currentOrder + "\nLocation: " + CurrentDockingrequest.Location + " forward: " + CurrentDockingrequest.ForwardVector + " up: " + CurrentDockingrequest.UpVector
                        + "\n Degrees Yaw: " + (int)nav._degreesToVectorYaw + " Degrees Pitch:" + (int)nav._degreesToVectorPitch +
                                 "\n Registered: " + RegisteredWithCommandShip + "\n MessagesSent: " + messagesSent);
                }
            }
            else
                LOG.InfoStay(" No Navigation" +
                             "\n Registered: " + RegisteredWithCommandShip);
        }

        private void ParseMessage(String argument)
        {
            ParsedMessage pm = new ParsedMessage(argument, LOG);
            LOG.AddRecievedMessage(argument);

            if (pm.IsAwakeningSignal)
            {
                ForceStartup();

                //bounce to awaken drones at extended ranges
                if (ParsedMessage.MaxNumBounces < pm.NumBounces)
                {
                    pm.NumBounces++;
                    LOG.Debug("Bounced Message");
                    PendingMessages.Add(pm.ToString());
                }
            }
            else if (long.Parse(pm.EntityId) == ShipEntityId)
            {
                switch (pm.MessageType)
                {
                    case MessageType.Confirmation:
                        try
                        {
                            var longpmid = long.Parse(pm.EntityId);
                            if (longpmid == ShipEntityId)
                                RegisteredWithCommandShip = true;
                        }
                        catch (Exception e)
                        {
                            //LOG.Info("Message Recieved - Failed to parse entityID"); 
                        }
                        break;
                    case MessageType.Update:
                        //LOG.Info("Message Recieved - Update, nothing to do"); 
                        break;
                    case MessageType.FlyToOrder:
                        // LOG.Debug("Recieved Order: FlyToOrder " + pm.Location); 
                        OrderLocation = pm.Location;
                        currentOrder = pm.MessageType;
                        break;
                    case MessageType.Dock:
                        var connector = _connectors.OrderBy(x => x.Status == MyShipConnectorStatus.Connected).FirstOrDefault();
                        var diffInConnectLoc = Math.Abs((grid.GetPosition() - connector.GetPosition()).Length());

                        if (CurrentDockingrequest != null && pm.Location != CurrentDockingrequest.Location)
                            CurrentDockingrequest = new DockingOrder(pm.AlignUp, pm.AlignForward, pm.Location, diffInConnectLoc, CurrentDockingrequest.DockingCourse.Count);
                        else if (CurrentDockingrequest == null)
                            CurrentDockingrequest = new DockingOrder(pm.AlignUp, pm.AlignForward, pm.Location, diffInConnectLoc);

                        currentOrder = pm.MessageType;
                        break;
                    case MessageType.Attack:
                        OrderLocation = pm.Location;

                        currentOrder = pm.MessageType;
                        break;
                    case MessageType.Mining:
                        CurrentMiningRequest = new MiningOrder(pm.AlignForward, pm.Location);

                        currentOrder = pm.MessageType;
                        break;
                    case MessageType.Unknown:
                        // LOG.Info("Message Recieved - Invalid, Unknown Origin"); 
                        break;
                }
                if (pm.MessageType == MessageType.PingEntity)
                    UpdateTrackedEntity(pm);
            }
            else
            {
                if (pm.MessageType == MessageType.PingEntity)
                    UpdateTrackedEntity(pm);

                if (ParsedMessage.MaxNumBounces < pm.NumBounces)
                {
                    pm.NumBounces++;
                    LOG.Debug("Bounced Message");
                    PendingMessages.Add(pm.ToString());
                }

                //LOG.Debug("Recieved Order for another drone"); 
            }
        }

        private bool TransmitMessage(String message)
        {
            foreach (var antenna in _radioAntennas)
            {

                if (antenna.TransmitMessage(message, MyTransmitTarget.Owned))
                {
                    LOG.AddOutgoingMessage("Transmiting: " + message);
                    messagesSent++;
                    return true;
                }
            }
            return false;
        }

        public int AttemptSendPendingMessages()
        {
            var sentMessageCount = 0;
            bool ableToTransmit = _radioAntennas.Any();

            if (lastmessageOnHold != null && ableToTransmit)
            {
                ableToTransmit = TransmitMessage(lastmessageOnHold);
                if (ableToTransmit)
                    lastmessageOnHold = null;
            }

            while (PendingMessages.Any() && ableToTransmit)
            {
                lastmessageOnHold = PendingMessages.First();
                PendingMessages.Remove(lastmessageOnHold);

                if (lastmessageOnHold != null)
                {
                    ableToTransmit = TransmitMessage(lastmessageOnHold);

                    if (ableToTransmit)
                        lastmessageOnHold = null;

                    break;
                }
                else
                    LOG.Error("Failed to transmit: (expected one pending message)" + lastmessageOnHold);
            }

            return sentMessageCount;
        }

        public bool IsOperational()
        {
            return nav != null;
        }

        private void BootUp()
        {
            //LOG.Info("Booting Up Navigation"); 
            if (_connectors.Any(x => x.Status == MyShipConnectorStatus.Connected))
                return;

            if (grid == null)
            {
                IMyTerminalBlock part = _connectors.Any() ? (IMyTerminalBlock)_connectors.FirstOrDefault() : (IMyTerminalBlock)_sensors.FirstOrDefault();
                if (part == null)
                    return;

                grid = part.CubeGrid;
                shipSize = Math.Abs((grid.Max - grid.Min).Length());
                ShipEntityId = grid.EntityId;
            }

            //now that grid is not null all parts will be found 
            LocateParts();

            var controlUnit = _controlUnits.FirstOrDefault();
            if (controlUnit != null)
            {
                grid.CustomName = "AI Drone #" + (ShipEntityId + "").Substring(0, 5);
                try
                {
                    nav = new NavigationControls(grid, controlUnit, GridTerminalSystem, LOG);
                }
                catch (Exception e)
                {
                    LOG.Error(e.ToString());
                }
            }
            else
            {
                LOG.Error("Failed to bootup Navigation: No RemoteControl Unit Detected");
            }
            BootedUp = true;
        }

        private void LocateParts()
        {
            if (grid == null)
            {
                _connectors.Clear();
                _sensors.Clear();

                GridTerminalSystem.GetBlocksOfType(_sensors);
                GridTerminalSystem.GetBlocksOfType(_connectors);
            }
            else
            {
                _miningDrills.Clear();
                _gatlingGuns.Clear();
                _rocketLaunchers.Clear();
                _programBlocks.Clear();
                _laserAntennas.Clear();
                _radioAntennas.Clear();
                _textPanels.Clear();
                _oredetectors.Clear();
                _controlUnits.Clear();
                _connectors.Clear();
                _sensors.Clear();
                _gyros.Clear();
                _thrusters.Clear();

                GridTerminalSystem.GetBlocksOfType(_cameras, b => b.CubeGrid == grid);
                GridTerminalSystem.GetBlocksOfType(_gyros, b => b.CubeGrid == grid);
                GridTerminalSystem.GetBlocksOfType(_thrusters, b => b.CubeGrid == grid);
                GridTerminalSystem.GetBlocksOfType(_sensors, b => b.CubeGrid == grid);
                GridTerminalSystem.GetBlocksOfType(_connectors, b => b.CubeGrid == grid);
                GridTerminalSystem.GetBlocksOfType(_programBlocks, b => b.CubeGrid == grid);
                GridTerminalSystem.GetBlocksOfType(_laserAntennas, b => b.CubeGrid == grid);
                GridTerminalSystem.GetBlocksOfType(_radioAntennas, b => b.CubeGrid == grid);
                GridTerminalSystem.GetBlocksOfType(_textPanels, b => b.CubeGrid == grid);
                GridTerminalSystem.GetBlocksOfType(_oredetectors, b => b.CubeGrid == grid);
                GridTerminalSystem.GetBlocksOfType(_controlUnits, b => b.CubeGrid == grid);
                GridTerminalSystem.GetBlocksOfType(_miningDrills, b => b.CubeGrid == grid);
                GridTerminalSystem.GetBlocksOfType(_gatlingGuns, b => b.CubeGrid == grid);
                GridTerminalSystem.GetBlocksOfType(_rocketLaunchers, b => b.CubeGrid == grid);
            }

            //activate all sensors 
            foreach (var sensor in _sensors)
            {
                sensor.DetectEnemy = true;
                sensor.DetectPlayers = true;
                sensor.DetectLargeShips = true;
                sensor.DetectSmallShips = true;
                sensor.DetectOwner = true;
                sensor.DetectStations = true;
                sensor.DetectAsteroids = true;

                sensor.BackExtend = 50;
                sensor.FrontExtend = 50;
                sensor.LeftExtend = 50;
                sensor.RightExtend = 50;
                sensor.TopExtend = 50;
                sensor.BottomExtend = 50;
            }
        }

        public class DockingOrder
        {
            public Vector3D UpVector;
            public Vector3D ForwardVector;
            public Vector3D Location;
            public double MinRange;
            public LinkedList<Vector3D> DockingCourse = new LinkedList<Vector3D>();

            public DockingOrder(Vector3D up, Vector3D ford, Vector3D loc, double distanceToConnector, int points = 75)
            {
                MinRange = distanceToConnector;
                Location = loc;
                UpVector = up;
                ForwardVector = ford;
                GeneratePath(points);
            }
            private void GeneratePath(int points)
            {
                for (double i = MinRange; i < points; i += 2)
                {
                    DockingCourse.AddFirst(Location + (ForwardVector * i));
                }
            }
        }

        public class MiningOrder
        {
            public Vector3D ForwardVector;
            public Vector3D Location;

            public MiningOrder(Vector3D ford, Vector3D loc)
            {
                Location = loc;
                ForwardVector = ford;
            }
        }

        public class Logger
        {
            int loglimit = 10;
            IMyCubeGrid grid_l;
            private List<IMyTextPanel> textPanels_l;

            private List<String> debugmessages = new List<String>();
            private List<String> errormessages = new List<String>();

            public Logger(List<IMyTextPanel> textpanels, IMyCubeGrid grid)
            {
                textPanels_l = textpanels;
            }

            public void UpdateLCDPanels()
            {
                UpdateDebugScreens();
                UpdateInfoScreens();
                UpdateErrorScreens();
                UpdateMessagesScreens();
            }

            public void Error(String message)
            {
                errormessages.Add(message);
            }

            public void Debug(String message)
            {
                debugmessages.Add(DateTime.Now.ToString("hh:mm:ss") + " " + message);
            }

            String errorstay = "";
            public void ErrorStay(String message)
            {
                errorstay = message;
            }
            String debugstay = "";
            public void DebugStay(String message)
            {
                debugstay = message;
            }
            String infostay = "";
            public void InfoStay(String message)
            {
                infostay = message;
            }

            String recievedMessagesflag = "#cc-lcd-messages-recieved#";
            private List<String> recievedMessages = new List<String>();
            public void AddRecievedMessage(String message)
            {

                recievedMessages.Add(message);

                if (recievedMessages.Count > loglimit)
                    recievedMessages.Remove(recievedMessages[0]);
            }
            public void UpdateMessagesScreens()
            {
                String incomming = "";

                foreach (var strin in recievedMessages)
                {
                    incomming += strin + "\n    ";
                }

                foreach (var lcd in textPanels_l.Where(x => x.CustomName.Contains(recievedMessagesflag)))
                {
                    if (grid_l != null)
                    {
                        lcd.WritePublicText(grid_l.CustomName + DateTime.Now + "\n Recieved Messages" +
                            "\n Messages Logs: " + recievedMessages.Count() + "\n" + incomming);
                    }
                    else
                        grid_l = lcd.CubeGrid;
                }


                String outgoing = "";

                foreach (var strin in outgoingMessages)
                {
                    outgoing += strin + "\n    ";
                }

                foreach (var lcd in textPanels_l.Where(x => x.CustomName.Contains(outgoingMessagesflag)))
                {
                    if (grid_l != null)
                    {
                        lcd.WritePublicText(grid_l.CustomName + DateTime.Now + "\n Outgoing Messages" +
                            "\n Messages Logs: " + outgoingMessages.Count() + "\n" + outgoing);
                    }
                    else
                        grid_l = lcd.CubeGrid;
                }
            }

            String outgoingMessagesflag = "#cc-lcd-messages-outgoing#";
            private List<String> outgoingMessages = new List<String>();
            public void AddOutgoingMessage(String message)
            {

                outgoingMessages.Add(message);

                if (outgoingMessages.Count > loglimit)
                    outgoingMessages.Remove(outgoingMessages[0]);
            }

            public void UpdateDebugScreens()
            {

                String debugString = "";
                int index = 0;

                foreach (var strin in debugmessages)
                {
                    if (index < loglimit)
                        debugString += strin + "\n   ";
                    else continue;
                }
                int removeMax = 0;
                if (debugmessages.Count() > loglimit)
                {
                    removeMax = loglimit < debugmessages.Count() ? loglimit : debugmessages.Count();
                    debugmessages.RemoveRange(0, removeMax);
                }

                foreach (var lcd in textPanels_l.Where(x => x.CustomName.Contains("#cc-lcd-debug#")))
                {
                    if (grid_l != null)
                    {
                        lcd.WritePublicText(grid_l.CustomName + DateTime.Now + "\n Debug Messages" +
                            "\n" + debugstay + "\n Ongoing Logs: " + debugmessages.Count() + " Removed logs: " + removeMax + "\n" + debugString);
                    }
                    else
                        grid_l = lcd.CubeGrid;
                }
            }

            public void UpdateInfoScreens()
            {
                foreach (var lcd in textPanels_l.Where(x => x.CustomName.Contains("#cc-lcd-info#")))
                {
                    if (grid_l != null)
                    {
                        lcd.WritePublicText(grid_l.CustomName + " " + DateTime.Now + "\n Info Screen" +
                            "\n " + infostay);
                    }
                    else
                        grid_l = lcd.CubeGrid;
                }
            }

            public void UpdateErrorScreens()
            {
                String debugString = "";
                int index = 0;

                foreach (var strin in errormessages)
                {
                    if (index < loglimit)
                        debugString += strin + "\n    ";
                    else continue;
                }
                int removeMax = 0;
                if (errormessages.Count() > loglimit)
                {
                    removeMax = loglimit < errormessages.Count() ? loglimit : errormessages.Count();
                    errormessages.RemoveRange(0, removeMax);
                }

                foreach (var lcd in textPanels_l.Where(x => x.CustomName.Contains("#cc-lcd-error#")))
                {
                    if (grid_l != null)
                    {
                        lcd.WritePublicText(grid_l.CustomName + DateTime.Now + "\n Error Messages" +
                            "\n " + errorstay + "\n Ongoing Logs: " + errormessages.Count() + " Removed logs: " + removeMax + "\n" + debugString);
                    }
                    else
                        grid_l = lcd.CubeGrid;
                }
            }

            String targetstatus = "#cc-lcd-targets-status#";
            public void UpdateEntityStatus(List<TrackedEntity> entities)
            {
                String message = "   Drone Status Screen " + DateTime.Now;
                foreach (var target in entities)
                {
                    message += "\nEntityId: " + target.EntityID + "\n     Name: " + target.name + " Location: " + target.Location + " Velocity: " + target.Velocity + "\n     Attack Location: " + target.AttackPoint + " Last Updated: " + target.LastUpdated + " Size: " + target.Radius;
                }
                foreach (var lcd in textPanels_l.Where(x => x.CustomName.Contains(targetstatus)))
                {
                    if (grid_l != null)
                    {
                        lcd.WritePublicText(grid_l.CustomName + DateTime.Now + "\n" + message);
                    }
                    else
                        grid_l = lcd.CubeGrid;
                }

            }

        }

        public class NavigationControls
        {
            public static string debugalignmode = "yaw";
            private string _logPath = "NavigationControls";
            private IMyCubeGrid _grid;
            private IMyRemoteControl _remoteControl;
            public Vector3D LinearVelocity = new Vector3D(0, 0, 0);
            DateTime lastSpeedCheck = DateTime.Now;

            Vector3D lastPosition = new Vector3D(0, 0, 0);

            //alignment 
            private Base6Directions.Direction _shipUp = 0;
            private Base6Directions.Direction _shipLeft = 0;
            private Base6Directions.Direction _shipForward = 0;
            private Base6Directions.Direction _shipDown = 0;
            private Base6Directions.Direction _shipRight = 0;
            private Base6Directions.Direction _shipBackward = 0;
            public double _degreesToVectorYaw = 0;
            public double _degreesToVectorPitch = 0;
            private float _alignSpeedMod = .02f;
            private float _rollSpeed = 2f;
            private List<GyroOverride> _gyroOverrides = new List<GyroOverride>();
            private List<IMyGyro> _gyros = new List<IMyGyro>();
            private List<IMyThrust> _thrusters = new List<IMyThrust>();
            private List<Vector3D> _coords = new List<Vector3D>();
            IMyGridTerminalSystem GridTerminalSystem = null;
            Logger LOG;

            public double GetSpeed()
            {
                if (LinearVelocity.Equals(new Vector3D(0, 0, 0)))
                    return 0;

                Vector3D vect = new Vector3D(LinearVelocity.X, LinearVelocity.Y, LinearVelocity.Z);
                return vect.Normalize();
            }

            public void UpdateSpeed()
            {
                var seconds = (DateTime.Now - lastSpeedCheck).TotalSeconds;
                var multiplyer = 1 / seconds;

                LinearVelocity = (_grid.GetPosition() - lastPosition) * multiplyer;
                lastPosition = _grid.GetPosition();
                lastSpeedCheck = DateTime.Now;
            }

            public void Update(bool docked, List<IMyGyro> gyros, List<IMyThrust> thrusters)
            {
                _gyros = gyros;
                _thrusters = thrusters;
                if (docked)
                {
                    EnableDockedMode();
                    return;
                }
                else
                    EnableFlightMode();

                if (IsOperational())
                {
                    SetShipOrientation();
                }
                else
                {

                    if (!IsOperational())
                        LOG.Error("navigation broken");
                }
            }

            public void EnableDockedMode()
            {
                foreach (var thruster in _thrusters)
                {
                    thruster.SetValueFloat("Override", 0);
                    thruster.GetActionWithName("OnOff_Off").Apply(thruster);
                }
                foreach (var gyro in _gyros)
                {
                    gyro.GetActionWithName("OnOff_Off").Apply(gyro);
                }
            }

            public void EnableFlightMode()
            {
                LOG.Debug("NumThrusters " + _thrusters.Count);
                foreach (var thruster in _thrusters)
                {
                    thruster.GetActionWithName("OnOff_On").Apply(thruster);
                }
                foreach (var gyro in _gyros)
                {
                    gyro.GetActionWithName("OnOff_On").Apply(gyro);
                }
            }

            public NavigationControls(IMyCubeGrid entity, IMyRemoteControl cont, IMyGridTerminalSystem gridterminal, Logger LOG)
            {
                this.LOG = LOG;
                _remoteControl = cont;
                _grid = entity;
                GridTerminalSystem = gridterminal;

                SetShipOrientation();
            }

            public void SetShipOrientation()
            {
                if (_remoteControl != null)
                {
                    _shipUp = _remoteControl.Orientation.Up;
                    _shipLeft = _remoteControl.Orientation.Left;
                    _shipForward = _remoteControl.Orientation.Forward;
                }
            }

            private IMyShipConnector _dockngConnector = null;

            public double AlignTo(Vector3D position)
            {
                TurnOffGyros(true);
                DegreesToVector(position);
                PointToVector(0.00);
                var angoff = (_degreesToVectorPitch + _degreesToVectorYaw);
                //LOG.Debug(angoff + " AlignUp Angle"); 
                return Math.Abs(angoff);
            }

            public double AlignToWobble(Vector3D position)
            {
                TurnOffGyros(true);
                DegreesToVector(position);
                if (_degreesToVectorPitch > 0)
                    _degreesToVectorPitch += .001;
                else
                    _degreesToVectorPitch += -.001;

                if (_degreesToVectorYaw > 0)
                    _degreesToVectorYaw += .001;
                else
                    _degreesToVectorYaw += -.001;

                PointToVector(0.00);
                var angoff = (_degreesToVectorPitch + _degreesToVectorYaw);
                //LOG.Debug(angoff + " AlignUp Angle"); 
                return Math.Abs(angoff);
            }

            public double AlignUp(Vector3D position)
            {
                var currentAlign = _remoteControl.WorldMatrix.Up + _grid.GetPosition();
                var anglebetween = AngleBetween(currentAlign, position, true);
                TurnOffGyros(true);
                //LOG.Debug(anglebetween+" AlignUp Angle"); 
                Roll((float)anglebetween);

                return anglebetween;
            }

            internal void Approach(Vector3D vector3D, int shipMaxSpeed)
            {
                var distance = (_grid.GetPosition() - vector3D).Length();
                var mySpeed = GetSpeed();
                if (mySpeed > shipMaxSpeed)
                    SlowDown();
                else
                    ThrustTwordsDirection(_grid.GetPosition() - vector3D);

            }

            internal void CombatApproach(Vector3D vector3D)
            {
                var distance = (_grid.GetPosition() - vector3D).Length();

                var maxSpeed = Math.Sqrt(distance) * 2;

                if (distance > 300)
                    ThrustTwordsDirection(_grid.GetPosition() - vector3D);
                else if (distance < 100)
                    ThrustTwordsDirection(vector3D - _grid.GetPosition());
                else
                    SlowDown();

            }

            private void TurnOffGyros(bool off)
            {
                for (int i = 0; i < _gyros.Count; i++)
                {
                    if ((_gyros[i]).GyroOverride != off)
                    {
                        TerminalBlockExtentions.ApplyAction(_gyros[i], "Override");
                    }
                }
            }

            private void DegreesToVector(Vector3D TV)
            {
                if (_remoteControl != null)
                {
                    var Origin = _remoteControl.GetPosition();
                    var Up = _remoteControl.WorldMatrix.Up;
                    var Forward = _remoteControl.WorldMatrix.Forward;
                    var Right = _remoteControl.WorldMatrix.Right;
                    // ExplainVector(Origin, "Origin"); 
                    // ExplainVector(Up, "up"); 

                    Vector3D OV = Origin; //Get positions of reference blocks.     
                    Vector3D FV = Origin + Forward;
                    Vector3D UV = Origin + Up;
                    Vector3D RV = Origin + Right;

                    //Get magnitudes of vectors. 
                    double TVOV = (OV - TV).Length();

                    double TVFV = (FV - TV).Length();
                    double TVUV = (UV - TV).Length();
                    double TVRV = (RV - TV).Length();

                    double OVUV = (UV - OV).Length();
                    double OVRV = (RV - OV).Length();

                    double ThetaP = Math.Acos((TVUV * TVUV - OVUV * OVUV - TVOV * TVOV) / (-2 * OVUV * TVOV));
                    //Use law of cosines to determine angles.     
                    double ThetaY = Math.Acos((TVRV * TVRV - OVRV * OVRV - TVOV * TVOV) / (-2 * OVRV * TVOV));

                    double RPitch = 90 - (ThetaP * 180 / Math.PI); //Convert from radians to degrees.     
                    double RYaw = 90 - (ThetaY * 180 / Math.PI);

                    if (TVOV < TVFV) RPitch = 180 - RPitch; //Normalize angles to -180 to 180 degrees.     
                    if (RPitch > 180) RPitch = -1 * (360 - RPitch);

                    if (TVOV < TVFV) RYaw = 180 - RYaw;
                    if (RYaw > 180) RYaw = -1 * (360 - RYaw);

                    _degreesToVectorYaw = RYaw;
                    _degreesToVectorPitch = RPitch;
                }

            }

            public void PointToVector(double precision)
            {
                if (_gyros != null)
                {
                    foreach (var gyro in _gyroOverrides)
                    {
                        try
                        {
                            gyro.TurnOn();

                            if (Math.Abs(_degreesToVectorYaw) > precision)
                            {
                                gyro.OverrideYaw((float)_degreesToVectorYaw * _alignSpeedMod);
                            }
                            else
                            {
                                gyro.OverrideYaw(0);
                            }


                            if (Math.Abs(_degreesToVectorPitch) > precision)
                            {
                                gyro.OverridePitch((float)_degreesToVectorPitch * _alignSpeedMod);
                            }
                            else
                            {
                                gyro.OverridePitch(0);
                            }

                            gyro.OverrideRoll(0);
                        }
                        catch (Exception e)
                        {
                            LOG.Error(e.ToString());
                        }
                    }
                }
            }

            private void ResetGyros()
            {
                _gyroOverrides.Clear();
                foreach (var gyro in _gyros)
                {
                    _gyroOverrides.Add(new GyroOverride(gyro, _shipForward, _shipUp, _shipLeft, _shipDown, _shipRight, _shipBackward, LOG));
                }
            }

            public bool IsOperational()
            {
                int numGyros = GetWorkingGyroCount();
                int numThrusters = GetWorkingThrusterCount();
                int numThrustDirections = GetNumberOfValidThrusterDirections();

                bool hasSufficientGyros = _gyros.Count > 0 && _gyroOverrides.Count > 0;

                bool operational = (numGyros > 0 && numThrusters > 0) && numThrustDirections >= 2;
                LOG.Debug("Navigation is Operational: " + operational + " gyros:thrusters => " + numGyros + ":" + numThrusters);

                var atleasthalfWorking = numThrusters >= _thrusters.Count() / 2;


                return operational && atleasthalfWorking && hasSufficientGyros;
            }

            public int GetWorkingThrusterCount()
            {
                return _thrusters.Count;
            }

            public int GetWorkingGyroCount()
            {
                ResetGyros();
                return _gyros.Count;
            }

            private int GetNumberOfValidThrusterDirections()
            {
                int up = 0;
                int down = 0;
                int left = 0;
                int right = 0;
                int forward = 0;
                int backward = 0;

                for (int i = 0; i < _thrusters.Count(x => x.IsWorking); i++)
                {

                    Base6Directions.Direction thrusterForward = _thrusters[i].Orientation.TransformDirectionInverse(_shipForward);

                    if (thrusterForward == Base6Directions.Direction.Up)
                    {
                        up++;
                    }
                    else if (thrusterForward == Base6Directions.Direction.Down)
                    {
                        down++;
                    }
                    else if (thrusterForward == Base6Directions.Direction.Left)
                    {
                        left++;
                    }
                    else if (thrusterForward == Base6Directions.Direction.Right)
                    {
                        right++;
                    }
                    else if (thrusterForward == Base6Directions.Direction.Forward)
                    {
                        forward++;
                    }
                    else if (thrusterForward == Base6Directions.Direction.Backward)
                    {
                        backward++;
                    }
                }
                int sum = (up > 0 ? 1 : 0)
                + (down > 0 ? 1 : 0)
                + (left > 0 ? 1 : 0)
                + (right > 0 ? 1 : 0)
                + (forward > 0 ? 1 : 0)
                + (backward > 0 ? 1 : 0)
                ;

                // Util.NotifyHud(sum + " dirs ");// +up+"+ up "+down+ "+ down" + left+ "+ left" + right+ "+ right" + forward+""+backward+ "+ forward"); 
                return sum;



            }

            public void Roll(float angle)
            {
                foreach (var gyro in _gyroOverrides)
                {
                    try
                    {
                        gyro.EnableOverride();
                        gyro.OverrideRoll(angle); //(_gyroRoll[i], (_rollSpeed) * (_gyroRollReverse[i]), gyro); 

                    }
                    catch (Exception e)
                    {
                        LOG.Error(e.ToString());
                        //Util.Notify(e.ToString()); 
                        //This is only to catch the occasional situation where the ship tried to align to something but has between the time the method started and now has lost a gyro or whatever 
                    }
                }
            }

            public void StopRoll()
            {
                foreach (var gyro in _gyroOverrides)
                {
                    try
                    {

                        gyro.DisableOverride();

                        gyro.OverrideRoll(0);// GyroSetFloatValue(_gyroRoll[i], 0, gyro); 

                    }
                    catch (Exception e)
                    {
                        LOG.Error(e.ToString());
                        //Util.Notify(e.ToString()); 
                        //This is only to catch the occasional situation where the ship tried to align to something but has between the time the method started and now has lost a gyro or whatever 
                    }
                }
            }

            public void StopSpin()
            {
                TurnOffGyros(false);
            }

            public void SlowDown()
            {
                foreach (var thru in _thrusters)
                {
                    thru.SetValueFloat("Override", 0);
                }

                if (!_remoteControl.DampenersOverride)
                    _remoteControl.DampenersOverride = true;

            }

            public bool ThrustTwordsDirection(Vector3D thrustVector, bool secondTry = false, bool usingHalfPower = false, int setPower = 10000)
            {
                //LOG.Debug("Speed: " + setPower+" "); 
                if (setPower < 10)
                    setPower = 12;
                //RefreshThrusters(); 
                if (secondTry)
                {
                    Roll(.5f);
                    //StopSpin(); 
                }
                int fullpower = usingHalfPower ? setPower / 2 : setPower;
                int lowpower = setPower / 4;
                thrustVector.Normalize();
                //var currentDirection = new Vector3D(LinearVelocity.X, LinearVelocity.Y, LinearVelocity.Z); 
                //currentDirection.Normalize(); 



                var desiredVector = thrustVector;
                //midwayvector ^^ to curve flight 


                //get drift vector 


                bool successfullyMoved = false;
                int numThrustersActivated = 0;
                foreach (var thruster in _thrusters)
                {
                    //Vector3D fow = thruster.WorldMatrix.Forward; 
                    //int xt = fow.X > 0 ? 1 : fow.X < 0 ? -1 : 0; 
                    //int yt = fow.Y > 0 ? 1 : fow.Y < 0 ? -1 : 0; 
                    //int zt = fow.Z > 0 ? 1 : fow.Z < 0 ? -1 : 0; 
                    //var thrusterVector = new Vector3D(xt, yt, zt); 

                    //bool notDrifting = counterDriftVector == Vector3D.Zero; 
                    //bool thrusterPointsDesiredDirection = thrusterVector.Equals(desiredVector); 
                    //bool thrusterCountersDrift = thrusterVector.Equals(counterDriftVector) && !notDrifting; 
                    //bool thrusterNeedsPower = thrusterCountersDrift || thrusterPointsDesiredDirection; 

                    var thrusterVector = thruster.WorldMatrix.Forward;
                    thrusterVector.Normalize();

                    double angle = Math.Abs(AngleBetween(thrusterVector, desiredVector, true));
                    //LOG.Debug("Angle " + angle); 
                    //LOG.Debug("power being used : " + setPower); 
                    if (angle < 75)
                    {

                        thruster.GetActionWithName("OnOff_On").Apply(thruster);

                        //for (var i = 0; i < fullpower - thruster.ThrustOverride; i++) 
                        //thruster.GetActionWithName("IncreaseOverride").Apply(thruster); 
                        thruster.SetValueFloat("Override", fullpower);
                        successfullyMoved = true;
                        numThrustersActivated++;
                    }
                    else
                    {
                        thruster.SetValueFloat("Override", 0);
                        //for (var i = 0; i < thruster.ThrustOverride; i++) 
                        //    thruster.GetActionWithName("DecreaseOverride").Apply(thruster); 
                    }
                }
                //LOG.Debug("Thrusted : " + numThrustersActivated+" num Thrusters"+ _thrusters.Count); 
                if (!successfullyMoved && !secondTry)
                    successfullyMoved = ThrustTwordsDirection(thrustVector, true, usingHalfPower, setPower);

                if (numThrustersActivated < 2)
                    Roll(.5f);

                //LOG.Debug(successfullyMoved+" :moved"); 
                return successfullyMoved;
            }

            public static double AngleBetween(Vector3D u, Vector3D v, bool returndegrees)
            {
                double toppart = 0;
                toppart += u.X * v.X;
                toppart += u.Y * v.Y;
                toppart += u.Z * v.Z;

                double u2 = 0; //u squared 
                double v2 = 0; //v squared 

                u2 += u.X * u.X;
                v2 += v.X * v.X;
                u2 += u.Y * u.Y;
                v2 += v.Y * v.Y;
                u2 += u.Z * u.Z;
                v2 += v.Z * v.Z;

                double bottompart = 0;
                bottompart = Math.Sqrt(u2 * v2);

                double rtnval = Math.Acos(toppart / bottompart);
                if (returndegrees) rtnval *= 360.0 / (2 * Math.PI);
                return rtnval;
            }

            private class GyroOverride
            {
                Base6Directions.Direction up = Base6Directions.Direction.Up;
                Base6Directions.Direction down = Base6Directions.Direction.Down;
                Base6Directions.Direction left = Base6Directions.Direction.Left;
                Base6Directions.Direction right = Base6Directions.Direction.Right;
                Base6Directions.Direction forward = Base6Directions.Direction.Forward;
                Base6Directions.Direction backward = Base6Directions.Direction.Backward;

                public IMyGyro gyro;
                String Pitch = "Pitch";
                String Yaw = "Yaw";
                String Roll = "Roll";
                int pitchdir = 1;
                int yawdir = 1;
                int rolldir = 1;
                Logger LOG;

                public void DisableOverride()
                {
                    if (gyro.GyroOverride)
                    {
                        gyro.GetActionWithName("Override").Apply(gyro);
                    }
                }

                public void EnableOverride()
                {
                    if (!gyro.GyroOverride)
                    {
                        gyro.GetActionWithName("Override").Apply(gyro);
                    }
                }

                public void TurnOff()
                {
                    gyro.GetActionWithName("OnOff_Off").Apply(gyro);
                }

                public void TurnOn()
                {
                    gyro.GetActionWithName("OnOff_On").Apply(gyro);
                }

                public void OverridePitch(float value)
                {
                    GyroSetFloatValue(Pitch, pitchdir * value);
                }

                public void OverrideRoll(float value)
                {
                    GyroSetFloatValue(Roll, rolldir * value);
                }

                public void OverrideYaw(float value)
                {
                    GyroSetFloatValue(Yaw, yawdir * value);
                }

                private void GyroSetFloatValue(String dir, float value)
                {
                    if (dir == "Yaw")
                    {
                        gyro.Yaw = value;
                    }
                    else if (dir == "Pitch")
                    {
                        gyro.Pitch = value;
                    }
                    else if (dir == "Roll")
                    {
                        gyro.Roll = value;
                    }
                }

                public GyroOverride(IMyGyro _gyro, Base6Directions.Direction _shipForward, Base6Directions.Direction _shipUp, Base6Directions.Direction _shipLeft, Base6Directions.Direction _shipDown, Base6Directions.Direction _shipRight, Base6Directions.Direction _shipBackward, Logger logger)
                {
                    LOG = logger;
                    Base6Directions.Direction gyroup = _gyro.Orientation.TransformDirectionInverse(_shipUp);
                    Base6Directions.Direction gyroleft = _gyro.Orientation.TransformDirectionInverse(_shipLeft);
                    Base6Directions.Direction gyroforward = _gyro.Orientation.TransformDirectionInverse(_shipForward);

                    this.gyro = _gyro;
                    if (gyroup == up)
                    {
                        if (gyroforward == left)
                        {
                            //LOG.DebugStay(gyroup + ":" + gyroforward + " up:left"); 
                            Pitch = "Roll"; rolldir = 1;
                            Roll = "Pitch"; pitchdir = 1;
                            Yaw = "Yaw"; yawdir = 1;
                        }
                        if (gyroforward == right)
                        {
                            //LOG.DebugStay(gyroup + ":" + gyroforward + " up:right"); 
                            Pitch = "Roll"; rolldir = 1;
                            Roll = "Pitch"; pitchdir = -1;
                            Yaw = "Yaw"; yawdir = 1;
                        }
                        if (gyroforward == forward)
                        {
                            // LOG.DebugStay(gyroup + ":" + gyroforward + " up:forward"); 
                            Pitch = "Pitch"; pitchdir = -1;
                            Roll = "Roll"; rolldir = 1;
                            Yaw = "Yaw"; yawdir = 1;
                        }
                        if (gyroforward == backward)
                        {
                            // LOG.DebugStay(gyroup + ":" + gyroforward + " up:back"); 
                            Pitch = "Pitch"; pitchdir = 1;
                            Roll = "Roll"; rolldir = -1;
                            Yaw = "Yaw"; yawdir = 1;
                        }
                    }
                    else if (gyroup == down)
                    {
                        if (gyroforward == left)
                        {
                            //LOG.DebugStay(gyroup + ":" + gyroforward + " down:left"); 
                            Pitch = "Roll"; rolldir = 1;
                            Roll = "Pitch"; pitchdir = -1;
                            Yaw = "Yaw"; yawdir = -1;
                        }
                        if (gyroforward == right)
                        {
                            //LOG.DebugStay(gyroup + ":" + gyroforward + " down:right"); 
                            Pitch = "Roll"; rolldir = -1;
                            Roll = "Pitch"; pitchdir = 1;
                            Yaw = "Yaw"; yawdir = -1;
                        }
                        if (gyroforward == forward)
                        {
                            //LOG.DebugStay(gyroup + ":" + gyroforward + " down:forward"); 
                            Pitch = "Pitch"; pitchdir = 1;
                            Roll = "Roll"; rolldir = 1;
                            Yaw = "Yaw"; yawdir = -1;
                        }
                        if (gyroforward == backward)
                        {
                            //LOG.DebugStay(gyroup + ":" + gyroforward + " down:back"); 
                            Pitch = "Pitch"; pitchdir = -1;
                            Roll = "Roll"; rolldir = -1;
                            Yaw = "Yaw"; yawdir = -1;
                        }
                    }
                    else if (gyroup == left)
                    {
                        if (gyroforward == forward)
                        {
                            //LOG.DebugStay(gyroup + ":" + gyroforward + " left:forward"); 
                            Pitch = "Yaw"; yawdir = -1;
                            Yaw = "Pitch"; pitchdir = -1;
                            Roll = "Roll"; rolldir = 1;
                        }
                        if (gyroforward == backward)
                        {
                            //LOG.DebugStay(gyroup + ":" + gyroforward + " left:back"); 
                            Pitch = "Yaw"; yawdir = -1;
                            Yaw = "Pitch"; pitchdir = 1;
                            Roll = "Roll"; rolldir = -1;
                        }
                        if (gyroforward == up)
                        {
                            //LOG.DebugStay(gyroup + ":" + gyroforward + " left:up"); 
                            Pitch = "Roll"; yawdir = -1;
                            Yaw = "Pitch"; rolldir = -1;
                            Roll = "Yaw"; pitchdir = -1;
                        }
                        if (gyroforward == down)
                        {
                            //LOG.DebugStay(gyroup + ":" + gyroforward + " left:down"); 
                            Pitch = "Roll"; yawdir = -1;
                            Yaw = "Pitch"; rolldir = 1;
                            Roll = "Yaw"; pitchdir = 1;
                        }
                    }
                    else if (gyroup == right)
                    {
                        if (gyroforward == forward)
                        {
                            //LOG.DebugStay(gyroup + ":" + gyroforward + " right:forward");
                            Pitch = "Yaw"; yawdir = 1;
                            Yaw = "Pitch"; pitchdir = 1;
                            Roll = "Roll"; rolldir = 1;
                        }
                        if (gyroforward == backward)
                        {
                            //LOG.DebugStay(gyroup + ":" + gyroforward + " right:back"); 
                            Pitch = "Yaw"; yawdir = 1;
                            Yaw = "Pitch"; pitchdir = -1;
                            Roll = "Roll"; rolldir = -1;
                        }
                        if (gyroforward == up)
                        {
                            //LOG.DebugStay(gyroup + ":" + gyroforward + " right:up"); 
                            Pitch = "Roll"; yawdir = 1;
                            Yaw = "Pitch"; rolldir = -1;
                            Roll = "Yaw"; pitchdir = 1;
                        }
                        if (gyroforward == down)
                        {
                            //LOG.DebugStay(gyroup + ":" + gyroforward + " right:down"); 
                            Pitch = "Roll"; yawdir = 1;
                            Yaw = "Pitch"; rolldir = 1;
                            Roll = "Yaw"; pitchdir = -1;
                        }
                    }
                    else if (gyroup == forward)
                    {
                        if (gyroforward == down)
                        {
                            //LOG.DebugStay(gyroup + ":" + gyroforward + " forward:down"); 
                            Roll = "Yaw"; yawdir = -1;
                            Pitch = "Pitch"; pitchdir = -1;
                            Yaw = "Roll"; rolldir = 1;
                        }
                        if (gyroforward == up)
                        {
                            //LOG.DebugStay(gyroup + ":" + gyroforward + " forward:up"); 
                            Roll = "Yaw"; yawdir = -1;
                            Pitch = "Pitch"; pitchdir = 1;
                            Yaw = "Roll"; rolldir = -1;
                        }
                        if (gyroforward == left)
                        {
                            //LOG.DebugStay(gyroup + ":" + gyroforward + " forward:left"); 
                            Pitch = "Yaw"; rolldir = 1;
                            Roll = "Pitch"; yawdir = -1;
                            Yaw = "Roll"; pitchdir = 1;
                        }
                        if (gyroforward == right)
                        {
                            //LOG.DebugStay(gyroup + ":" + gyroforward + " forward:right"); 
                            Pitch = "Yaw"; rolldir = -1;
                            Roll = "Pitch"; yawdir = -1;
                            Yaw = "Roll"; pitchdir = -1;
                        }
                    }
                    else if (gyroup == backward)
                    {
                        if (gyroforward == down)
                        {
                            //LOG.DebugStay(gyroup + ":" + gyroforward + " back:down"); 
                            Roll = "Yaw"; yawdir = 1;
                            Pitch = "Pitch"; pitchdir = 1;
                            Yaw = "Roll"; rolldir = 1;
                        }
                        if (gyroforward == up)
                        {
                            //LOG.DebugStay(gyroup + ":" + gyroforward + " back:up"); 
                            Roll = "Yaw"; yawdir = 1;
                            Pitch = "Pitch"; pitchdir = -1;
                            Yaw = "Roll"; rolldir = -1;
                        }
                        if (gyroforward == left)
                        {
                            //LOG.DebugStay(gyroup + ":" + gyroforward + " back:left"); 
                            Pitch = "Yaw"; rolldir = 1;
                            Roll = "Pitch"; yawdir = 1;
                            Yaw = "Roll"; pitchdir = -1;
                        }
                        if (gyroforward == right)
                        {
                            //LOG.DebugStay(gyroup + ":" + gyroforward + " back:right");
                            Pitch = "Yaw"; rolldir = -1;
                            Roll = "Pitch"; yawdir = 1;
                            Yaw = "Roll"; pitchdir = 1;
                        }
                    }

                    LOG.DebugStay(gyroup + ":" + gyroforward + " back:right "+debugalignmode); 

                }

            }
        }

        private List<TrackedEntity> trackedEntities = new List<TrackedEntity>();

        private void UpdateTrackedEntity(ParsedMessage pm)
        {
            TrackedEntity te = trackedEntities.Where(x => x.EntityID == pm.TargetEntityId).FirstOrDefault();

            if (te == null)
            {
                te = new TrackedEntity(pm);
                trackedEntities.Add(te);
            }

            te.Location = pm.Location;
            te.Velocity = pm.Velocity;
            te.LastUpdated = DateTime.Now;
            te.Radius = pm.TargetRadius;
            te.DetailsString = pm.ToString();
            te.Relationship = pm.Relationship;

            if (pm.AttackPoint != Vector3D.Zero)
                te.AttackPoint = pm.AttackPoint;

        }

        public class TrackedEntity
        {
            public Vector3D Location;
            public Vector3D Velocity;
            public Vector3D AttackPoint;
            public DateTime LastUpdated;
            public String DetailsString;
            public long EntityID;
            public String name;
            public int Radius;
            public MyRelationsBetweenPlayerAndBlock Relationship;

            public TrackedEntity(ParsedMessage pm)
            {
                LastUpdated = DateTime.Now;
                Location = pm.Location;
                Velocity = pm.Velocity;
                AttackPoint = pm.AttackPoint;
                EntityID = pm.TargetEntityId;
                name = pm.Name;
                Radius = pm.TargetRadius;
                DetailsString = pm.ToString();
                Relationship = pm.Relationship;
            }
        }

        public class ParsedMessage
        {
            Dictionary<String, String> messageElements = new Dictionary<string, string>();
            Logger LOG;
            public MessageType MessageType = MessageType.Unknown;
            public String RequestID = null;
            public String EntityId = null;
            public long TargetEntityId = 0;
            public String Name = null;
            public Vector3D Location = Vector3D.Zero;
            public Vector3D Velocity = Vector3D.Zero;
            public Vector3D AttackPoint = Vector3D.Zero;
            public String Status = null;
            public long CommanderId = 0;
            public String MessageString;
            public String BounceString;
            public int NumBounces = 0;
            public static int MaxNumBounces = 2;
            public bool IsAwakeningSignal = false;
            public int TargetRadius = 0;
            public Vector3D AlignForward = Vector3D.Zero;
            public Vector3D AlignUp = Vector3D.Zero;
            public bool Docked = false;
            public int ConnectorCount = 0;
            public int DrillCount = 0;
            public int SensorCount = 0;
            public double ShipSize = 0;
            public int WeaponCount = 0;
            public int PercentCargo = 0;
            public MyRelationsBetweenPlayerAndBlock Relationship = MyRelationsBetweenPlayerAndBlock.Neutral;

            // message flags 
            const String MESSAGETYPE_FLAG = "11";
            const String REQUESTID_FLAG = "12";
            const String NAME_FLAG = "13";
            const String LOCATION_FLAG = "14";
            const String ATTACKPOINT_FLAG = "16";
            const String VELOCITY_FLAG = "15";
            const String ENTITYID_FLAG = "17";
            const String TARGETID_FLAG = "19";
            const String COMMANDID_FLAG = "111";
            const String STATUS_FLAG = "18";
            const String MAXBOUNCE_FLAG = "113";
            const String NUMBOUNCES_FLAG = "114";
            const String TARGETRADIUS_FLAG = "115";
            const String ALIGNFORWARDVECTOR_FLAG = "116";
            const String ALIGNUPVECTOR_FLAG = "117";
            const String DOCKEDSTATUS_FLAG = "118";
            const String SHIPSIZE_FLAG = "122";
            const String PERCENTCARGO_FLAG = "546";
            const String AWAKENING_FLAG = "666";
            const String RELATIONSHIP_FLAG = "fof";

            //message types 
            const String REGISTER_FLAG = "21";
            const String CONFIRMATION_FLAG = "22";
            const String UPDATE_FLAG = "23";
            const String PINGENTITY_FLAG = "24";

            //component counts 
            const String NUMCONNECTORS_FLAG = "119";
            const String NUMMININGDRILLS_FLAG = "120";
            const String NUMSENSORS_FLAG = "121";
            const String NUMWEAPONS_FLAG = "124";
            const String NUMROCKETLAUNCHERS_FLAG = "123";
            const String NUMCAMERA_FLAG = "125";

            //ordertypes 
            const String ORDER_FLAG = "25";
            const String DOCKORDER_FLAG = "26";
            const String ATTACKORDER = "27";
            const String MININGORDER = "28";

            public static String CreateAwakeningMessage()
            {
                String msgStr = "";

                msgStr += AWAKENING_FLAG + ":" + 0;
                msgStr += "," + NUMBOUNCES_FLAG + ":" + 0;


                return "{" + msgStr + "}";
            }

            public static String CreateConfirmationMessage(String entityId, String requestId)
            {
                String msgStr = "";

                msgStr += MESSAGETYPE_FLAG + ":" + CONFIRMATION_FLAG;
                msgStr += "," + ENTITYID_FLAG + ":" + entityId;
                msgStr += "," + NUMBOUNCES_FLAG + ":" + 0;
                msgStr += "," + REQUESTID_FLAG + ":" + requestId;


                return "{" + msgStr + "}";
            }

            public static String CreateRegisterMessage(long entityId, int requestsSent)
            {
                String msgStr = "";
                msgStr += MESSAGETYPE_FLAG + ":" + REGISTER_FLAG;
                msgStr += "," + REQUESTID_FLAG + ":" + entityId + requestsSent;
                msgStr += "," + NUMBOUNCES_FLAG + ":" + 0;
                msgStr += "," + ENTITYID_FLAG + ":" + entityId;
                ;
                msgStr = "{" + msgStr + "}";

                return msgStr;
            }

            public static String CreateFlyToOrderMessage(long entityId, Vector3D targetLocation, long commandentityID)
            {
                String msgStr = "";
                msgStr += MESSAGETYPE_FLAG + ":" + ORDER_FLAG;
                msgStr += "," + COMMANDID_FLAG + ":" + commandentityID;
                msgStr += "," + ENTITYID_FLAG + ":" + entityId;
                msgStr += "," + NUMBOUNCES_FLAG + ":" + 0;
                msgStr += "," + LOCATION_FLAG + ":" + VectorToString(targetLocation);
                msgStr = "{" + msgStr + "}";

                return msgStr;
            }

            public static String CreateAttackMessage(long entityId, Vector3D targetLocation, Vector3D targetVelocity, long commandentityID)
            {
                String msgStr = "";
                msgStr += MESSAGETYPE_FLAG + ":" + ATTACKORDER;
                msgStr += "," + COMMANDID_FLAG + ":" + commandentityID;
                msgStr += "," + ENTITYID_FLAG + ":" + entityId;
                msgStr += "," + NUMBOUNCES_FLAG + ":" + 0;
                msgStr += "," + LOCATION_FLAG + ":" + VectorToString(targetLocation);
                msgStr += "," + VELOCITY_FLAG + ":" + VectorToString(targetLocation);
                msgStr = "{" + msgStr + "}";

                return msgStr;
            }

            public static String CreateMiningMessage(long entityId, long asteriodid, Vector3D targetLocation, Vector3D alignVector, long commandentityID)
            {
                String msgStr = "";
                msgStr += MESSAGETYPE_FLAG + ":" + MININGORDER;
                msgStr += "," + ALIGNFORWARDVECTOR_FLAG + ":" + VectorToString(alignVector);
                msgStr += "," + COMMANDID_FLAG + ":" + commandentityID;
                msgStr += "," + ENTITYID_FLAG + ":" + entityId;
                msgStr += "," + TARGETID_FLAG + ":" + asteriodid;
                msgStr += "," + NUMBOUNCES_FLAG + ":" + 0;
                msgStr += "," + LOCATION_FLAG + ":" + VectorToString(targetLocation);
                msgStr = "{" + msgStr + "}";

                return msgStr;
            }

            public static String CreateDockingOrderMessage(long entityId, Vector3D targetLocation, Vector3D alignForward, Vector3D alignUp, long commandentityID)
            {
                String msgStr = "";
                msgStr += MESSAGETYPE_FLAG + ":" + DOCKORDER_FLAG;
                msgStr += "," + ALIGNFORWARDVECTOR_FLAG + ":" + VectorToString(alignForward);
                msgStr += "," + ALIGNUPVECTOR_FLAG + ":" + VectorToString(alignUp);
                msgStr += "," + COMMANDID_FLAG + ":" + commandentityID;
                msgStr += "," + ENTITYID_FLAG + ":" + entityId;
                msgStr += "," + NUMBOUNCES_FLAG + ":" + 0;
                msgStr += "," + LOCATION_FLAG + ":" + VectorToString(targetLocation);
                msgStr = "{" + msgStr + "}";

                return msgStr;
            }

            public static String CreateUpdateMessage(long entityId, Vector3D velocity, Vector3D location, int requestsSent, bool docked,int numberofCameras, int numberofConnectors, int numDrills, int numsensors, int numWeapons, double shipsize, int percentCargo)
            {
                String msgStr = "";

                msgStr += MESSAGETYPE_FLAG + ":" + UPDATE_FLAG;
                msgStr += "," + ENTITYID_FLAG + ":" + entityId;
                msgStr += "," + SHIPSIZE_FLAG + ":" + shipsize;
                msgStr += "," + PERCENTCARGO_FLAG + ":" + percentCargo;
                msgStr += "," + NUMCONNECTORS_FLAG + ":" + numberofConnectors;
                msgStr += "," + NUMCAMERA_FLAG + ":" + numberofCameras;
                msgStr += "," + NUMSENSORS_FLAG + ":" + numsensors;
                msgStr += "," + NUMMININGDRILLS_FLAG + ":" + numDrills;
                msgStr += "," + NUMWEAPONS_FLAG + ":" + numWeapons;
                msgStr += "," + DOCKEDSTATUS_FLAG + ":" + docked;
                msgStr += "," + REQUESTID_FLAG + ":" + entityId + requestsSent;
                msgStr += "," + NUMBOUNCES_FLAG + ":" + 0;
                msgStr += "," + VELOCITY_FLAG + ":" + VectorToString(velocity);
                msgStr += "," + LOCATION_FLAG + ":" + VectorToString(location);
                msgStr = "{" + msgStr + "}";

                return msgStr;
            }

            public static String CreatePingEntityMessage(MyDetectedEntityInfo info, long entityid, int requestsSent)
            {
                String msgStr = "";

                var hitpos = info.HitPosition;

                msgStr += MESSAGETYPE_FLAG + ":" + PINGENTITY_FLAG;
                msgStr += "," + TARGETID_FLAG + ":" + info.EntityId;

                msgStr += "," + ENTITYID_FLAG + ":" + entityid;
                msgStr += "," + TARGETRADIUS_FLAG + ":" + (int)Math.Abs((info.BoundingBox.Min - info.BoundingBox.Max).Length());
                msgStr += "," + REQUESTID_FLAG + ":" + info.EntityId + requestsSent;
                msgStr += "," + VELOCITY_FLAG + ":" + VectorToString(info.Velocity);
                msgStr += "," + LOCATION_FLAG + ":" + VectorToString(info.Position);
                msgStr += "," + RELATIONSHIP_FLAG + ":" + info.Relationship;
                msgStr += "," + NUMBOUNCES_FLAG + ":" + 0;
                msgStr += "," + NAME_FLAG + ":" + info.Name;


                if (hitpos.HasValue)
                    msgStr += "," + ATTACKPOINT_FLAG + ":" + VectorToString(hitpos.Value);

                msgStr = "{" + msgStr + "}";

                return msgStr;
            }

            private static String VectorToString(Vector3D vect)
            {
                String str = Math.Round(vect.X, 4) + "|" + Math.Round(vect.Y, 4) + "|" + Math.Round(vect.Z, 4);
                return str;
            }

            public ParsedMessage(String message, Logger log)
            {
                LOG = log;
                MessageString = message;
                String messageNoBrackets = message.Replace("{", "").Replace("}", "");
                ReadProperties(messageNoBrackets);

                foreach (var pair in messageElements)
                {
                    switch (pair.Key)
                    {
                        case MESSAGETYPE_FLAG:
                            ParseMessageType(pair.Value);
                            break;
                        case REQUESTID_FLAG:
                            RequestID = pair.Value;
                            break;
                        case ENTITYID_FLAG:
                            EntityId = pair.Value;
                            break;
                        case PERCENTCARGO_FLAG:
                            PercentCargo = int.Parse(pair.Value);
                            break;
                        case COMMANDID_FLAG:
                            CommanderId = long.Parse(pair.Value);
                            break;
                        case RELATIONSHIP_FLAG:
                            MyRelationsBetweenPlayerAndBlock.TryParse(pair.Value, out Relationship);
                            break;
                        case TARGETID_FLAG:
                            TargetEntityId = long.Parse(pair.Value);
                            break;
                        case NUMMININGDRILLS_FLAG:
                            DrillCount = int.Parse(pair.Value);
                            break;
                        case NUMSENSORS_FLAG:
                            SensorCount = int.Parse(pair.Value);
                            break;
                        case NUMCONNECTORS_FLAG:
                            ConnectorCount = int.Parse(pair.Value);
                            break;
                        case NUMBOUNCES_FLAG:
                            NumBounces = (int)double.Parse(pair.Value);
                            break;
                        case SHIPSIZE_FLAG:
                            ShipSize = double.Parse(pair.Value);
                            break;
                        case MAXBOUNCE_FLAG:
                            MaxNumBounces = (int)double.Parse(pair.Value);
                            break;
                        case ATTACKPOINT_FLAG:
                            AttackPoint = TryParseVector(pair.Value);
                            break;
                        case NAME_FLAG:
                            Name = pair.Value;
                            break;
                        case LOCATION_FLAG:
                            Location = TryParseVector(pair.Value);
                            break;
                        case VELOCITY_FLAG:
                            Velocity = TryParseVector(pair.Value);
                            break;
                        case DOCKEDSTATUS_FLAG:
                            Docked = bool.Parse(pair.Value);
                            break;
                        case STATUS_FLAG:
                            Status = pair.Value;
                            break;
                        case ALIGNFORWARDVECTOR_FLAG:
                            AlignForward = TryParseVector(pair.Value);
                            break;
                        case ALIGNUPVECTOR_FLAG:
                            AlignUp = TryParseVector(pair.Value);
                            break;
                        case AWAKENING_FLAG:
                            IsAwakeningSignal = true;
                            break;
                        case TARGETRADIUS_FLAG:
                            TargetRadius = (int)double.Parse(pair.Value);
                            break;
                        case NUMWEAPONS_FLAG:
                            WeaponCount = int.Parse(pair.Value);
                            break;
                        default:
                            return;
                    }
                }
            }

            public bool IsValid()
            {
                if (MessageType != MessageType.Unknown)
                    return true;

                return false;
            }

            //should be formatted as x-y-z 
            private Vector3D TryParseVector(String vector)
            {
                var splits = vector.Split('|');
                try
                {
                    if (splits.Count() == 3)
                    {
                        var loc = new Vector3D(double.Parse(splits[0]), double.Parse(splits[1]), double.Parse(splits[2]));
                        //LOG.Debug("Location Parsed: "+loc); 
                        return loc;
                    }
                    else
                    {
                        LOG.Error("Unable to parse into 3 splits: " + vector);
                    }

                }
                catch
                {
                    LOG.Error("Unable to parse Location: " + vector);
                }

                return Vector3D.Zero;
            }

            private void ParseMessageType(String messaget)
            {
                switch (messaget)
                {
                    case REGISTER_FLAG:
                        MessageType = MessageType.Register;
                        break;
                    case CONFIRMATION_FLAG:
                        MessageType = MessageType.Confirmation;
                        break;
                    case UPDATE_FLAG:
                        MessageType = MessageType.Update;
                        break;
                    case PINGENTITY_FLAG:
                        MessageType = MessageType.PingEntity;
                        break;
                    case ORDER_FLAG:
                        MessageType = MessageType.FlyToOrder;
                        break;
                    case DOCKORDER_FLAG:
                        MessageType = MessageType.Dock;
                        break;
                    case ATTACKORDER:
                        MessageType = MessageType.Attack;
                        break;
                    case MININGORDER:
                        MessageType = MessageType.Mining;
                        break;
                }
            }

            public void ReadProperties(String message)
            {
                String bouncemsg = "{";
                var splits = message.Split(',');
                int index = 0;

                foreach (var pair in splits)
                {
                    var keyval = pair.Split(':');
                    if (keyval.Length == 2)
                    {
                        var key = keyval[0];
                        var value = keyval[1];
                        messageElements.Add(key, value);

                        if (index == 0 && key == NUMBOUNCES_FLAG)
                            bouncemsg += key + ":" + value + "";
                        else if (index == 0)
                            bouncemsg += key + ":" + value + "";
                        else if (key == NUMBOUNCES_FLAG)
                            bouncemsg += "," + key + ":" + (int.Parse(value) + 1) + "";
                        else
                            bouncemsg += "," + key + ":" + value + "";
                        index++;
                    }
                    else
                    {
                        LOG.Error("failed to parse message {" + message + "} @ " + pair);
                    }
                }
                bouncemsg += "}";
                BounceString = bouncemsg;
            }

            public override String ToString()
            {
                return BounceString;
            }
        }

        public enum MessageType
        {
            Register,
            Confirmation,
            Update,
            PingEntity,
            Attack,
            FlyToOrder,
            Dock,
            Scan,
            Mining,
            Unknown
        }


































    }
}
