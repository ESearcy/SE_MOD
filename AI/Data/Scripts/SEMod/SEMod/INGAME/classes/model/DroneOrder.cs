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

namespace SEMod.INGAME.classes.model
{
    //////
    public class DroneOrder
    {
        public OrderType Ordertype = OrderType.Unknown;
        public long DroneId;
        public double RequestId;
        public bool Initalized = false;
        public Vector3D PrimaryLocation;
        public Vector3D DirectionalVectorOne;
        public Vector3D ThirdLocation;
        public DateTime IssuedAt;
        public DateTime LastUpdated;
        public long TargetEntityID;
        public bool Confirmed = false;

        public Vector3D Destination;
        internal IMyShipConnector Connector;
        Logger log;

        public DroneOrder(Logger l,  OrderType type, double requestID, long targetEntityId, long DroneID, Vector3D primaryLocation, Vector3D desiredUpDirection, Vector3D thirdLocation)
        {
            log = l;
            TargetEntityID = targetEntityId;
            RequestId = requestID;
            IssuedAt = DateTime.Now;
            LastUpdated = IssuedAt;
            DroneId = DroneID;
            Ordertype = type;
            PrimaryLocation = primaryLocation;
            DirectionalVectorOne = desiredUpDirection;
            ThirdLocation= thirdLocation;
            DirectionalVectorOne.Normalize();
            Initalize();
            DockRouteIndex = dockroute.Count() - 1;
        }

        internal void Initalize()
        {
            switch (Ordertype)
            {
                case OrderType.Scan:
                    Destination = PrimaryLocation + (-DirectionalVectorOne * 500);
                    break;
                case OrderType.Attack:
                    Destination = PrimaryLocation;
                    break;
                case OrderType.Dock:
                    UpdateDockingCoords();
                    break;
            }
        }

        int dockingDistance = 20;
        public int DockRouteIndex=0;
        public List<Vector3D> dockroute = new List<Vector3D>();
        internal void UpdateDockingCoords()
        {
            dockroute.Clear();
            //log.Debug("setting up dock routes");
            for (int i= 1; i < dockingDistance; i++)
            {
                //log.Debug("Point Added");
                dockroute.Add(PrimaryLocation + (DirectionalVectorOne * i));
            }
        }
    }
    //////
}
