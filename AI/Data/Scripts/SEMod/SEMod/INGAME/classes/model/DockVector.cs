using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using VRage.Game;
using VRage.Game.ModAPI.Ingame;
using VRageMath;
using SpaceEngineers.Game.ModAPI.Ingame;


namespace SEMod.INGAME.classes.model
{
    //////
    public class DockVector
    {
        public Vector3D Location;
        public bool Reached = false;

        public DockVector(Vector3D pos)
        {
            Location = pos;
        }
    }
    //////
}
