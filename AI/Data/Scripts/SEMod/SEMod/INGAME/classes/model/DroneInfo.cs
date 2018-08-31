using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using VRage.Game;
using VRage.Game.ModAPI.Ingame;
using VRageMath;
using SpaceEngineers.Game.ModAPI.Ingame;

namespace SEMod.INGAME.classes
{
    //////
    public class DroneInfo
    {
        public long EntityId;
        public Vector3D lastKnownPosition = Vector3D.Zero;
        public Vector3D LastKnownVector = Vector3D.Zero;
        public DateTime lastUpdated = DateTime.Now;
        public String Status = "none";
        public String Name;
        public bool Docked = false;
        public int NumConnectors = 0;
        public int NumDrills = 0;
        public int NumWeapons = 0;
        public int numSensors = 0;
        public double ShipSize = 0;
        public int PercentCargo = 0;
        public int CameraCount = 0;
        public bool Unloaded = false;

        public DroneInfo(long id, String name, Vector3D location, Vector3D velocity)
        {
            EntityId = id;
            Name = name;
            lastKnownPosition = location;
            LastKnownVector = velocity;
        }

        public void Update(String name, Vector3D location, Vector3D velocity, bool docked, int cameraCount, double shipsize, int drillcount, int weaponCount, int sensorCount, int connectorCount, int percentCargo)
        {
            CameraCount = cameraCount;
            PercentCargo = percentCargo;
            Name = name;
            lastKnownPosition = location;
            LastKnownVector = velocity;
            Docked = docked;
            ShipSize = shipsize;
            NumWeapons = weaponCount;
            NumDrills = drillcount;
            numSensors = sensorCount;
            NumConnectors = connectorCount;
            lastUpdated = DateTime.Now;
        }
    }
    //////
}
