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
    public enum OrderType
    {
        FlyTo,
        AlignTo,
        Dock,
        Undock,
        Mine,
        Scan,
        Attack,
        Unknown,
        Standby
    }
    //////
}
