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
    class CombatDrone : AIShipBase
    {
        IMyProgrammableBlock Me = null;
        IMyGridTerminalSystem GridTerminalSystem = null;
        IMyGridProgramRuntimeInfo Runtime;

        public CombatDrone()
        //////public Program()
        {

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
            operatingOrder.AddLast(new TaskInfo(FollowOrders));
            operatingOrder.AddLast(new TaskInfo(SensorScan));
            operatingOrder.AddLast(new TaskInfo(AnalyzePlanetaryData));
            operatingOrder.AddLast(new TaskInfo(InternalSystemScan));
            //operatingOrder.AddLast(new TaskInfo(MaintainAltitude));
            operatingOrder.AddLast(new TaskInfo(UpdateTrackedTargets));
            operatingOrder.AddLast(new TaskInfo(FollowOrders));
            operatingOrder.AddLast(new TaskInfo(UpdateDisplays));
            maxCameraRange = 5000;
            maxCameraAngle = 80;

            //set new defaults
            hoverHeight = 100;
        }

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

        public void IntrepretMessage(String argument)
        {
            if (argument == null)
                return;

            var pm = communicationSystems.ParseMessage(argument);

            if (!registered && pm.TargetEntityId == Me.CubeGrid.EntityId && pm.MessageType == MessageCode.Confirmation)
            {
                registered = true;
                CommandShipEntity = pm.EntityId;
                log.Debug("Registered!!");
            }
            
            if (ParsedMessage.MaxNumBounces < pm.NumBounces && pm.MessageType != MessageCode.PingEntity)
            {
                pm.NumBounces++;
                //LOG.Debug("Bounced Message");
                communicationSystems.SendMessage(pm.ToString());
            }

            if (pm.IsAwakeningSignal)
                ForceStartup();
            else if(registered) {
                switch (pm.MessageType)
                {
                    case MessageCode.Order:
                        if (CommandShipEntity == pm.CommanderId) {
                            //log.Debug(pm.OrderType+" order recieved");
                            if (pm.OrderType == OrderType.Dock && CurrentOrder != null && CurrentOrder.Ordertype == OrderType.Dock)
                            {
                                try
                                {
                                    CurrentOrder.PrimaryLocation = pm.Location;
                                    CurrentOrder.UpdateDockingCoords();
                                }
                                catch (Exception e) { log.Error(e.StackTrace); }

                            }
                            else
                                NextOrder = new DroneOrder(log,pm.OrderType, pm.RequestID, pm.TargetEntityId, pm.EntityId, pm.Location, pm.AlignUp, pm.AlignForward);
                        }
                        break;
                    //case MessageCode.PingEntity:
                    //    if (pm.Type.Trim().ToLower().Contains("planet"))
                    //        trackingSystems.UpdatePlanetData(pm, false);
                    //    else
                    //        trackingSystems.UpdateTrackedEntity(pm, false);

                    //    break;
                }
            }
        }

        //Order related variables
        DateTime LastUpdateTime = DateTime.Now.AddMinutes(-5);
        long CommandShipEntity = 0;
        bool registered = false;
        DroneOrder CurrentOrder;
        DroneOrder NextOrder;
        public void FollowOrders()
        {
            try
            {
                //send update or register with any command ship
                if ((DateTime.Now - LastUpdateTime).TotalSeconds >= 1)
                {
                    if (!registered)
                    {
                        SendRegistrationRequest();
                        //log.Debug("sending registration request");
                    }
                    else
                    {
                        SendUpdate();
                    }
                    LastUpdateTime = DateTime.Now;
                }

                ProcessCurrentOrder();
            }
            catch (Exception e) { log.Error("FollowOrders " + e.Message+" "+e.StackTrace); }
        }

        public void SendUpdate()
        {
            communicationSystems.SendMessage(ParsedMessage.CreateUpdateMessage(Me.CubeGrid.EntityId, CommandShipEntity, navigationSystems.LinearVelocity, Me.CubeGrid.GetPosition(), communicationSystems.GetMsgSntCount(), Docked,
                shipComponents.Cameras.Count(), shipComponents.Connectors.Count(), shipComponents.MiningDrills.Count(), shipComponents.Sensors.Count(), shipComponents.GatlingGuns.Count(),
                Me.CubeGrid.GridSize, storageSystem.GetPercentFull()));
        }

        private void SendRegistrationRequest()
        {
            communicationSystems.SendMessage(ParsedMessage.CreateRegisterMessage(Me.CubeGrid.EntityId, communicationSystems.GetMsgSntCount()));
        }

        int combatAltitude = 800;
        private void ProcessCurrentOrder()
        {
            maxCameraRange = 3000;
            maxCameraAngle = 100;
            
            //weaponSystems.Disengage();

            if (NextOrder != null)
            {
                CurrentOrder = NextOrder;
                NextOrder = null;
            }

            //log.Debug("processing");
            if (CurrentOrder != null)
            {
                //log.Debug("processing order");
                
                if (CurrentOrder.Ordertype == OrderType.Scan)
                    navigationSystems.HoverApproach(CurrentOrder.Destination, Mass);
                else if (CurrentOrder.Ordertype == OrderType.Dock)
                {
                    try
                    {
                        //log.Debug("Processing Dock Order");
                        //log.Debug(CurrentOrder.dockroute.Count()+" Number of dock Orders");
                        var preDockLocation = CurrentOrder.dockroute[CurrentOrder.DockRouteIndex];
                        if (preDockLocation != null) { 
                        //CurrentOrder.PrimaryLocation + (CurrentOrder.DirectionalVectorOne * 20);

                            var remoteControl = shipComponents.ControlUnits.FirstOrDefault();
                            var connector = shipComponents.Connectors.FirstOrDefault();

                            var shipDockPoint = remoteControl.GetPosition();
                            var connectorAdjustVector = connector.GetPosition() - remoteControl.GetPosition();


                            if (connector.Status != MyShipConnectorStatus.Connected)
                            {
                                var distanceFromCPK1 = ((shipDockPoint + connectorAdjustVector) - preDockLocation).Length();

                                if (distanceFromCPK1 <= 2 && CurrentOrder.DockRouteIndex > 0)
                                {
                                    CurrentOrder.DockRouteIndex--;
                                }

                                var distanceFromConnector = ((shipDockPoint) - CurrentOrder.PrimaryLocation).Length();

                                var maxSpeed = distanceFromCPK1 > 10 ? 2 : 1;

                                if (!navigationSystems.DockApproach(connector.GetPosition(), preDockLocation, maxSpeed))
                                    navigationSystems.Roll(0.15f);
                                else
                                    navigationSystems.Roll(0.00f);

                                if (distanceFromConnector < 10)
                                {
                                    log.Debug("Dock cp3");
                                    connector.GetActionWithName("OnOff_On").Apply(connector);
                                    if (connector.Status == MyShipConnectorStatus.Connectable)
                                    {
                                        connector.Connect();
                                    }
                                }

                                log.Debug("from dock " + distanceFromConnector + " from point: " + distanceFromCPK1 + " index: " + CurrentOrder.dockroute.Count);

                                navigationSystems.AlignTo(Me.CubeGrid.GetPosition() + (CurrentOrder.DirectionalVectorOne * 100));
                            }
                            else
                            {
                                navigationSystems.EnableDockedMode();
                                Docked = true;
                            }
                        }
                        else
                        {
                            log.Error("No Predock Location");
                            navigationSystems.SlowDown();
                        }

                    }
                    catch (Exception e)
                    {
                        log.Error("In Dock\n"+e.Message+"\n"+e.StackTrace);
                    }
                }
                else if (CurrentOrder.Ordertype == OrderType.Standby)
                {
                    if (Docked)
                    {
                        navigationSystems.EnableDockedMode();
                        return;

                    }
                }
            }
            else if (Docked)
                navigationSystems.EnableDockedMode();
            else 
            {
                //if no command ship

                //navigationSystems.AlignAcrossGravity();
                navigationSystems.SlowDown();
                navigationSystems.AlignAgainstGravity();
                //navigationSystems.Roll(0);
                //navigationSystems.StopRoll();


                navigationSystems.MaintainAltitude(trackingSystems.GetAltitude(),10);
            }

        }

        bool Docked = false;
        //////
    }
}
