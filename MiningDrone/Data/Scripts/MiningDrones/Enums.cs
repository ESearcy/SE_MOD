using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiningDrones
{
    public enum OrbitTypes
    {
        X,XY,Y,YZ,Z,XZ,
        Default
    }
    public enum ActionTypes
    {
        Guard,
        Orbit,
        Return,
        Sentry,
        Assist,
        Patrol
    }

    public enum GameCommands
    {
        On,
        Off,
        Clearing,
        Reporting
    }

    public enum DroneModes
    {
        AtRange,
        Fighter
    }
    public enum ConquestDrones
    {
        SmallOne,
        SmallTwo,
        SmallThree,
        MediumOne,
        MediumTwo,
        MediumThree,
        LargeOne,
        LargeTwo,
        LargeThree
    }

    public enum DroneTypes
    {
        PlayerDrone,
        SpacePirateShip,
        NotADrone
    }

    public enum Standing
    {
        Hostile,
        Passive
    }

    public enum BroadcastingTypes
    {
        Beacon,
        Antenna
    }

}
