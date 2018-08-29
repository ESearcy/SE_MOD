using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Sandbox.ModAPI;
using VRageMath;
using Task = ParallelTasks.Task;

namespace MiningDrones
{
    public class PlayerAssets
    {

        private static string logPath = "PlayerAsset.txt";
        public HashSet<SpacePirateShip> MiningDrones = new HashSet<SpacePirateShip>();
        public Vector3D _playerLocation = Vector3D.Zero;
        public HashSet<DroneCommandCenter> CommandStations = new HashSet<DroneCommandCenter>();
        public long PlayerId;

        public PlayerAssets(long i)
        {
            PlayerId = i;
            target = v2;
        }

        public void Update(int count)
        {
           // var command = UpdateCommandCenters();
            UpdatePirates(count);
        }


        Vector3D v = new Vector3D(100, 100, 100);
        Vector3D v3 = new Vector3D(-200, 200, -200);
        Vector3D v2 = new Vector3D(-100, -100, -100);
        
        private Vector3D target;

        private void UpdatePirates(int count)
        {
            MiningDrones = new HashSet<SpacePirateShip>(MiningDrones.Where(x => x.IsAlive()));
            foreach (var drone in MiningDrones)
            {
                if (_playerLocation != Vector3D.Zero)
                {
                    Util.GetInstance().Log("following player", logPath);

                }
                else
                {
                    _playerLocation = drone.GetLocation();
                    Util.GetInstance().Log("Orbiting Location", logPath);
                }


                //if (target == v2)
                //{
                //    if ((drone.Ship.GetPosition() - v2).Length() < 75)
                //    {
                //        target = v;
                //    }
                //}
                //else if (target == v)
                //{
                //    if ((drone.Ship.GetPosition() - v).Length() < 75)
                //    {
                //        target = v3;
                //    }
                //}
                //else
                //{
                //    if ((drone.Ship.GetPosition() - v3).Length() < 75)
                //    {
                //        target = v2;
                //    }
                //}
                ////idrone.AlignTo(_playerLocation);
                ////drone.FlyToLocation(target);
                //if (count == 0)
                //{
                //    drone.AimFreeOrbit(new Vector3D(0, 0, 0));
                //    drone.AlignTo(new Vector3D(100,100,100));
                //}
                //else if (count == 1)
                //    drone.FlyToLocation(target);
                //else
                //{
                //    drone.FlyToLocation(target);
                //}
                //drone.FlyToLocationAndFromLocationWhileAimingAtLocation(new Vector3D(10, 10, 10), new Vector3D(10, 10, 10),_playerLocation);
                //drone.AlignTo(_playerLocation);
                //drone.Orbit(_playerLocation);

                drone.Update(_playerLocation);
                //});
                //);
                count++;
            }
            //keyValuePair.Value.Update(location);
        }
    }
}
