using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.ModAPI.Ingame;

namespace SEMod.INGAME.classes.systems
{
    //////
    public class StorageSystem
    {
        private Logger log;
        private IMyCubeGrid cubeGrid;
        private ShipComponents shipComponets;

        public StorageSystem(Logger log, IMyCubeGrid cubeGrid, ShipComponents shipComponets)
        {
            this.log = log;
            this.cubeGrid = cubeGrid;
            this.shipComponets = shipComponets;
        }

        internal bool IsOperational()
        {
            return true;
        }

        internal int GetPercentFull()
        {
            return 0;
        }
    }
    //////
}
