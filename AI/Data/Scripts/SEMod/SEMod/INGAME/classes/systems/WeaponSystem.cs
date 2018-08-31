using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.ModAPI.Ingame;

namespace SEMod.INGAME.classes.systems
{
    //////
    public class WeaponSystem
    {
        private Logger log;
        private IMyCubeGrid cubeGrid;
        private ShipComponents shipComponets;

        public WeaponSystem(Logger log, IMyCubeGrid cubeGrid, ShipComponents shipComponets)
        {
            this.log = log;
            this.cubeGrid = cubeGrid;
            this.shipComponets = shipComponets;
        }

        internal bool IsOperational()
        {
            return true;
        }

        internal void Engage()
        {
            foreach (var weapon in shipComponets.GatlingGuns)
            {
                ((IMySmallGatlingGun)weapon).GetActionWithName("Shoot_On").Apply(weapon);
            }
            foreach (var weapon in shipComponets.RocketLaunchers)
            {
                ((IMySmallMissileLauncher)weapon).GetActionWithName("ShootOnce").Apply(weapon);
            }
        }

        internal void Disengage()
        {
            foreach (var weapon in shipComponets.GatlingGuns)
            {
                (weapon).GetActionWithName("Shoot_Off").Apply(weapon);
            }
            foreach (var weapon in shipComponets.RocketLaunchers)
            {
                (weapon).GetActionWithName("Shoot_Off").Apply(weapon);
            }
        }
    }
    //////
}
