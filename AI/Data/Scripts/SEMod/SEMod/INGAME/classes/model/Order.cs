using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using VRage.Game;
using VRage.Game.ModAPI.Ingame;
using VRageMath;
using SpaceEngineers.Game.ModAPI.Ingame;
using SEMod.INGAME.classes.model;

namespace SEMod.INGAME.classes
{
    //////
    public class Order
    {
        public static DateTime starttime = DateTime.Now;
        public static int OrderIdIndex = 0;
        public double orderid;
        public OrderType Type = OrderType.Unknown;
        public long DroneID;
        public IMyShipConnector Connector = null;
        public long TargetEntityID;
        public bool DockRouteSet = false;
        public Vector3D OrderLocation;
        public Vector3D AlignTo;
        public Vector3D AlignUp;
        public bool Confirmed = false;
        public DateTime IssuedAt = DateTime.Now;
        public DateTime LastUpdated = DateTime.Now;
        public List<PointOfInterest> MiningPoints = new List<PointOfInterest>();
        public bool ReachedPrepPosition = false;

        public Order(OrderType typ, long droneid, Vector3D orderloc, Vector3D alignto, Vector3D alignup, IMyShipConnector connector = null)
        {
            DroneID = droneid;
            orderid = (DateTime.Now - starttime).TotalSeconds;
            Connector = connector;
            Type = typ;
            OrderLocation = orderloc;
            AlignTo = alignto;
            AlignUp = alignup;
            if (typ == OrderType.Mine)
                InitalizeMiningOrder();
        }

        public Order(OrderType typ, long droneid, Vector3D orderloc, Vector3D alignto, Vector3D alignup, long Entityid, IMyShipConnector connector = null)
        {
            DroneID = droneid;
            orderid = (DateTime.Now - starttime).TotalSeconds;
            Connector = connector;
            Type = typ;
            OrderLocation = orderloc;
            AlignTo = alignto;
            AlignUp = alignup;
            TargetEntityID = Entityid;
            if(typ == OrderType.Mine)
                InitalizeMiningOrder();
        }

        public List<DockVector> DockRoute = new List<DockVector>();

        public Order(OrderType typ, double orderid, long droneid, Vector3D orderloc, Vector3D alignto, Vector3D alignup)
        {
            DroneID = droneid;
            this.orderid = orderid;
            Type = typ;
            OrderLocation = orderloc;
            AlignTo = alignto;
            AlignUp = alignup;
            dockpushoutrange = (int)(orderloc - alignto).Length();
        }

        int dockpushoutrange;
        int dockSplitCount = 20;
        public void InitalizeDockRoute(Vector3D startLocation)
        {
            DockRouteSet = true;
            DockRoute.Add(new DockVector(startLocation));
            //add midpoint
            DockRoute.Add(new DockVector(OrderLocation + (AlignTo * 50)));
            DockRoute.Add(new DockVector(OrderLocation + (AlignTo * 2)));
        }

        public void InitalizeMiningOrder()
        {
            var dirToPlanet = OrderLocation - AlignTo;
            dirToPlanet.Normalize();

            var bottomOfHoleVector = OrderLocation + (dirToPlanet*20);
            var distance = bottomOfHoleVector.Normalize();
            for (int i = 0; i < distance; i=i+2)
                MiningPoints.Add(new PointOfInterest(OrderLocation + (dirToPlanet * i)));

        }

        int dockindex = 0;
        public Vector3D GetCurrentDockPoint(Vector3D shipPosition)
        {
            var current = DockRoute[dockindex];
            if ((shipPosition - current.Location).Length() <= 0.001 && dockindex < (DockRoute.Count - 1))
            {
                dockindex++;
                current = DockRoute[dockindex];
            }

            return current.Location;
        }
    }
    //////
}
