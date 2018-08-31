using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SEMod.INGAME.classes.model
{
    //////
    public class DroneContext
    {
        public DroneInfo Info;
        public DroneOrder Order;
        public DroneContext(DroneInfo info, DroneOrder o)
        {
            Order = o;
            Info = info;
        }
    }
    //////
}
