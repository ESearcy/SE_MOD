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
    public class Docktracker
    {
        public DateTime TimeConnected = DateTime.Now;
        public IMyShipConnector Connector;
        public DroneInfo DroneInfo;

        public Docktracker(IMyShipConnector connector, DroneInfo di)
        {
            DroneInfo = di;
            Connector = connector;
        }
    }
    //////
}
