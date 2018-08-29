using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiningDrones
{
    enum DroneWeaponActions
    {
        Attacking,
        LockedOn,
        Standby
    }

    enum DroneNavigationActions
    {
        Approaching,
        Orbiting,
        KeepingAtRange,
        Stationary,
        Avoiding
    }
}
