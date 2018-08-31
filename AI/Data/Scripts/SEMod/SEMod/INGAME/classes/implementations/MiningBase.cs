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
    class MiningBase : AIShipBase
    {
        IMyProgrammableBlock Me = null;
        IMyGridTerminalSystem GridTerminalSystem = null;
        IMyGridProgramRuntimeInfo Runtime;
        
        public MiningBase()
        //////public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            shipComponents = new ShipComponents();
            LocateAllParts();
            log = new Logger(Me.CubeGrid, shipComponents);

            communicationSystems = new CommunicationSystem(log, Me.CubeGrid, shipComponents);
            navigationSystems = new NavigationSystem(log, Me.CubeGrid, shipComponents);
            productionSystems = new ProductionSystem(log, Me.CubeGrid, shipComponents);
            storageSystem = new StorageSystem(log, Me.CubeGrid, shipComponents);
            trackingSystems = new TrackingSystem(log, Me.CubeGrid, shipComponents);
            weaponSystems = new WeaponSystem(log, Me.CubeGrid, shipComponents);

            operatingOrder.AddLast(new TaskInfo(LocateAllParts));
            operatingOrder.AddLast(new TaskInfo(InternalSystemScan));
            operatingOrder.AddLast(new TaskInfo(SensorScan));
            operatingOrder.AddLast(new TaskInfo(AnalyzePlanetaryData));
            operatingOrder.AddLast(new TaskInfo(MaintainAltitude));
            operatingOrder.AddLast(new TaskInfo(UpdateTrackedTargets));
            operatingOrder.AddLast(new TaskInfo(UpdateDisplays));
            operatingOrder.AddLast(new TaskInfo(IssueOrders));
            operatingOrder.AddLast(new TaskInfo(ProcessDockedDrones));
            maxCameraRange = 5000;
            maxCameraAngle = 80;
            //set new defaults
            hoverHeight = 300;
        }

        List<DroneContext> drones = new List<DroneContext>();
        DateTime startTime = DateTime.Now;

        protected void Main(String argument, UpdateType updateType)
        {
            
            try
            {
                if (argument.Equals(updateArg))
                {
                    Update();
                }

                else
                {
                    IntrepretMessage(argument);
                }
            }
            catch (Exception e)
            {
                log.Error(e.Message);
            }
            
        }

        private void RegisterDrone(ParsedMessage pm)
        {
            var drone = drones.Where(x => x.Info.EntityId == pm.EntityId).FirstOrDefault();
            if (drone == null)
            {
                drones.Add(new DroneContext(new DroneInfo(pm.EntityId, pm.Name, pm.Location, pm.Velocity), null));
                UpdateDrone(pm);
            }

            communicationSystems.SendMessage(ParsedMessage.CreateConfirmationMessage(Me.CubeGrid.EntityId, pm.EntityId, pm.RequestID));
            log.Debug("registered Drone");
        }
        private void UpdateDrone(ParsedMessage pm)
        {
            log.Debug("processing update for drone");
            var drone = drones.Where(x => x.Info.EntityId == pm.EntityId).FirstOrDefault();
            if (drone == null)
                drones.Add(new DroneContext(new DroneInfo(pm.EntityId, pm.Name, pm.Location, pm.Velocity), null));
            else
                drone.Info.Update(pm.Name,pm.Location,pm.Velocity,pm.Docked,pm.CameraCount,pm.ShipSize,pm.DrillCount,pm.WeaponCount,pm.SensorCount,pm.ConnectorCount,pm.PercentCargo);
        }

        public void IntrepretMessage(String argument)
        {
            if (argument == null)
                return;

            var pm = communicationSystems.ParseMessage(argument);

            if (ParsedMessage.MaxNumBounces < pm.NumBounces && pm.MessageType != MessageCode.PingEntity)
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
                    case MessageCode.Register:
                        RegisterDrone(pm);
                        break;
                    case MessageCode.Update:
                        UpdateDrone(pm);
                        break;

                    //case MessageCode.PingEntity:
                    //    if (pm.Type.Trim().ToLower().Contains("planet"))
                    //        trackingSystems.UpdatePlanetData(pm, false);
                    //    else
                    //        trackingSystems.UpdateTrackedEntity(pm, false);

                    //    break;
                }
        }

        private void ProcessDockedDrones()
        {
            var dockedDrones = drones.Where(x=>x.Info.Docked);
            //var undockOrders = drones.Where(x => x.Order != null && x.Order.Ordertype == OrderType.Dock);
            //log.Debug("Number Of Docked Ships: "+dockedDrones.Count());

            //refill and restock ship.

            foreach (var drone in dockedDrones)
            {
                ManageSuppliesAndEnergy(drone);
                if (drone.Order != null)
                {
                    if (drone.Order.Ordertype == OrderType.Dock)
                        IssueStandbyOrder(drone);
                }
                else
                    IssueStandbyOrder(drone);
                    
            }
        }

        private bool ManageSuppliesAndEnergy(DroneContext drone)
        {
            return true;
        }

        DateTime lastGlobalWakeupCall = DateTime.Now.AddMinutes(-10);
        int droneOrderIndex = 0;
        public void IssueOrders()
        {
            try { 
                if (droneOrderIndex >= drones.Count())
                    droneOrderIndex = 0;

                if((DateTime.Now - lastGlobalWakeupCall).TotalMinutes>=10)
                {
                    lastGlobalWakeupCall = DateTime.Now;
                    communicationSystems.SendAwakeningMessage();
                    log.Debug("sent awakening Message");
                }
                if (drones.Any())
                {
                    //log.Debug("attempting to order drone");
                    var drone = drones[droneOrderIndex];
                    if(drone != null)
                    {
                        //log.Debug("Drone Not null ");
                        //LOOK AT CURRENT ORDER
                        var order = drone.Order;
                        if (order != null && (DateTime.Now - order.LastUpdated).TotalSeconds < 3)
                        {
                            var time = (DateTime.Now - order.LastUpdated).TotalSeconds;
                            if (time >= 10 || (time > 1 && order.Ordertype == OrderType.Dock))
                            {
                                if (order.Ordertype == OrderType.Dock)
                                {
                                    order.PrimaryLocation = order.Connector.GetPosition();
                                }
                                //send again
                                communicationSystems.TransmitOrder(order, Me.CubeGrid.EntityId);
                                log.Debug("Resending " + order.Ordertype + " order");
                                order.LastUpdated = DateTime.Now;
                            }
                        }
                        else if (order == null)
                        {
                            //if (drone.Info.NumWeapons == 0)
                            //{
                            //    IssueSurveyOrder(order, drone);
                            //}
                            //else
                            //{
                            //if (drone.Info.NumWeapons > 0)
                            //    IssueAttackOrder(drone);
                            if (drone.Info.NumConnectors > 0 && !drone.Info.Docked)
                                IssueDockOrder(drone);
                            //}
                        }
                    }
                }


                droneOrderIndex++;
                
            }
            catch (Exception e) { log.Error("IssueOrders " + e.Message + "\n"+e.StackTrace); }
            log.UpdateFleetInformationScreens(drones, Me.CubeGrid);
        }

        private bool IssueDockOrder(DroneContext drone)
        {
            log.Debug("Attampting Dock Order");
            var unused = shipComponents.Connectors.Where(x=>x.Status!=MyShipConnectorStatus.Connectable || x.Status != MyShipConnectorStatus.Connected);
            var used = drones.Where(x => x.Order!=null && x.Order.Connector != null).Select(x=>x.Order.Connector);
            var available = unused.Where(x=> !used.Contains(x));
            var usableConnector = available.FirstOrDefault();

            log.Debug("unused: "+ unused.Count()+ " used: " + used.Count()+ " available: " + available.Count()+"  : "+ (usableConnector!=null));

            if (usableConnector != null)
            {
                log.Debug("Issuing Dock Order");
                var order = new DroneOrder(log, OrderType.Dock, communicationSystems.GetMsgSntCount(), usableConnector.EntityId, drone.Info.EntityId, usableConnector.GetPosition(), usableConnector.WorldMatrix.Forward, usableConnector.WorldMatrix.Up);
                order.Connector = usableConnector;
                communicationSystems.TransmitOrder(order, Me.CubeGrid.EntityId);
                drone.Order = order;
                return true;
            }
            return false;
        }

        private void IssueStandbyOrder(DroneContext drone)
        {
            log.Debug("Attampting Standby Order");
            var order = new DroneOrder(log, OrderType.Standby, communicationSystems.GetMsgSntCount(), 0, drone.Info.EntityId, Vector3D.Zero, Vector3D.Zero, Vector3D.Zero);
            communicationSystems.TransmitOrder(order, Me.CubeGrid.EntityId);
            drone.Order = order;
        }

        public void IssueAttackOrder(DroneContext drone)
        {
            var closestTargets = trackingSystems.getCombatTargets(Me.GetPosition());
            if (closestTargets.Any())
            {
                var biggestTarget = closestTargets.OrderByDescending(x => x.Radius).FirstOrDefault();
                if (biggestTarget != null)
                {
                    log.Debug("Issuing Attack Order");
                    var order = new DroneOrder(log, OrderType.Attack, communicationSystems.GetMsgSntCount(), biggestTarget.EntityID, drone.Info.EntityId, biggestTarget.PointsOfInterest[0].Location, navigationSystems.GetGravityDirection(), biggestTarget.Location);
                    communicationSystems.TransmitOrder(order, Me.CubeGrid.EntityId);
                    drone.Order = order;
                }
            }
        }

        public void IssueSurveyOrder(DroneOrder order,DroneContext drone)
        {
            //get planets with a high scan density and few points
            var RegionsOfIntrest = trackingSystems.GetNearestPlanet().Regions.Where(x => (x.surfaceCenter - Me.CubeGrid.GetPosition()).Length() < 2000).Where(x => x.PointsOfInterest.Count() < 13 && x.GetScanDensity() >= 50).OrderBy(x => (x.surfaceCenter - Me.CubeGrid.GetPosition()).Length()).Take(5);
            if (RegionsOfIntrest.Any())
            {
                //log.Debug(RegionsOfIntrest.Count() + " Regions of Intrest Located");
                var regionsWithLowCoverage = RegionsOfIntrest.Where(x => x.GetPercentReached() < 50);
                if (regionsWithLowCoverage.Any())
                {
                    //log.Debug(regionsWithLowCoverage.Count() + " Regions of Intrest With low coverage Located");
                    var closiestRegion = regionsWithLowCoverage.First();
                    var closestUnscannedPointToDrone = closiestRegion.GetNearestSurveyPoint(closiestRegion.surfaceCenter);
                    if (closestUnscannedPointToDrone != null)
                    {
                        log.Debug(regionsWithLowCoverage.Count() + " Point of Intrest in low coverage region Located");
                        order = new DroneOrder(log, OrderType.Scan, communicationSystems.GetMsgSntCount(), 0, drone.Info.EntityId, closestUnscannedPointToDrone.Location, navigationSystems.GetGravityDirection(), Vector3D.Zero);
                        communicationSystems.TransmitOrder(order, Me.CubeGrid.EntityId);
                        drone.Order = order;
                    }
                }
            }
        }

        protected void IssueMiningOrder()
        {
            var miningTarget = trackingSystems.GetNextMiningTestPoint(Me.GetPosition());
        }

        protected void MaintainAltitude()
        {
            try
            {
                NearestPlanet = trackingSystems.GetNearestPlanet();

                if (NearestPlanet != null)
                {
                    if (navigationSystems.GetSpeed() > 10)
                        navigationSystems.SlowDown();
                    else if (drones.Any(x => x.Order.Ordertype == OrderType.Dock && (x.Info.lastKnownPosition - x.Order.Connector.GetPosition()).Length() > 60))
                        navigationSystems.MaintainAltitude(trackingSystems.GetAltitude(), hoverHeight);
                    else
                        navigationSystems.SlowDown();

                    navigationSystems.AlignAgainstGravity();
                }
                else
                    log.Debug("No Planet");
            }
            catch (Exception e) { log.Error("MaintainAltitude " + e.Message); }
        }


        //////
    }
}
