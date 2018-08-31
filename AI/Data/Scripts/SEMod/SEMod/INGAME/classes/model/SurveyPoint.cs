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
    public class SurveyPoint
    {
        public DateTime LastUpdated = DateTime.Now;
        public Vector3D Location;

        public SurveyPoint(Vector3D location)
        {
            Location = location;
        }
    }
    //////
}
