using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SEMod
{
    enum ShipTypes
    {
        NavyFighter = 0,
        NavyFrigate = 1,
        NotADrone = 2,
        AIDrone = 3,
        AIFrigate = 4,
        AILeadShip = 5
    }

    enum DroneWeaponActions
    {
        Standby,
        Attacking,
        LockedOn
    }

    enum DroneNavigationActions
    {
        Stationary,
        Approaching,
        Orbiting,
        Avoiding,
        BreakAway,
        AttackRun
    }

    enum OrbitTypes
    {
        X, XY, Y, YZ, Z, XZ,
        Default
    }

}
