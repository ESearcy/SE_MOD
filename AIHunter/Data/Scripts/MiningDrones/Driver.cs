using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DroneConquest;
using Sandbox.Common;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.Utils;
using VRage.Voxels;
using VRageMath;

namespace MiningDrones
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class Driver : MySessionComponentBase
    {

        private static String logPath = "Driver.txt";
        private int ticks = 0;
        private AsteroidManager aManager = new AsteroidManager();
        DroneManager dManager = new DroneManager();

        public override void UpdateBeforeSimulation()
        {
            try
            {
                Util.GetInstance().DebuggingOn = true;
                //Util.GetInstance().Notify("Test In Mod");
                if (ticks % 100 == 0)
                    Util.SaveLogs();

                if (ticks % 10 == 0)
                    dManager.Update();

                //    if (ticks == 500)
                //    {
                //        List<IMyVoxelBase> asteroids = new List<IMyVoxelBase>();
                //        MyAPIGateway.Session.VoxelMaps.GetInstances(asteroids);

                //        Util.GetInstance().Log(asteroids.Count+"","asteroids.txt");
                //        foreach (var asteroid in asteroids)
                //        {
                //            var size = asteroid.Storage.Size;
                //            var range = asteroid.StorageName;

                //            //var storage = new VRage.Voxels.MyStorageData();
                //            BoundingSphereD newSphere;
                //            newSphere.Center = asteroid.GetPosition();
                //            newSphere.Radius = asteroid.LocalAABB.Max.Normalize();

                //            FindMaterial(asteroid, asteroid.GetPosition(), 4, asteroid.LocalAABB.Max.Normalize());
                //        }
                //    }
            }
            catch (Exception e)
            {
                Util.GetInstance().LogError(e.ToString());
            }

            ticks++;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="voxelMap"></param>
        /// <param name="resolution">0 to 8. 0 for fine/slow detail.</param>
        /// <param name="distance"></param>
        /// <returns></returns>
        private int FindMaterial(IMyVoxelBase voxelMap, Vector3D center, int resolution, double distance)
        {
            Util.GetInstance().Log("Finding Mineral","findingOres.txt");
            int hits = 0;
            var materials = MyDefinitionManager.Static.GetVoxelMaterialDefinitions().Where(v=> v.IsRare).ToArray();
            var findMaterial = materials.Select(f => f.Index).ToArray();

            var storage = voxelMap.Storage;
            var scale = (int)Math.Pow(2, resolution);
            //MyAPIGateway.Utilities.ShowMessage("center", center.ToString());
            var point = new Vector3I(center - voxelMap.PositionLeftBottomCorner);
            //MyAPIGateway.Utilities.ShowMessage("point", point.ToString());

            var min = ((point - (int)distance) / 64) * 64;
            min = Vector3I.Max(min, Vector3I.Zero);
            //MyAPIGateway.Utilities.ShowMessage("min", min.ToString());

            var max = ((point + (int)distance) / 64) * 64;
            max = Vector3I.Max(max, min + 64);
            //MyAPIGateway.Utilities.ShowMessage("max", max.ToString());

            //MyAPIGateway.Utilities.ShowMessage("size", voxelMap.StorageName + " " + storage.Size.ToString());

            if (min.X >= storage.Size.X ||
                min.Y >= storage.Size.Y ||
                min.Z >= storage.Size.Z)
            {
                //MyAPIGateway.Utilities.ShowMessage("size", "out of range");
                return 0;
            }

            var oldCache = new MyStorageData();

            //var smin = new Vector3I(0, 0, 0);
            //var smax = new Vector3I(31, 31, 31);
            ////var size = storage.Size;
            //var size = smax - smin + 1;
            //size = new Vector3I(16, 16, 16);
            //oldCache.Resize(size);
            //storage.ReadRange(oldCache, MyStorageDataTypeFlags.ContentAndMaterial, resolution, Vector3I.Zero, size - 1);

            var smax = (max / scale) - 1;
            var smin = (min / scale);
            var size = smax - smin + 1;
            oldCache.Resize(size);
            storage.ReadRange(oldCache, MyStorageDataTypeFlags.ContentAndMaterial, resolution, smin, smax);

            //MyAPIGateway.Utilities.ShowMessage("smax", smax.ToString());
            //MyAPIGateway.Utilities.ShowMessage("size", size .ToString());
            //MyAPIGateway.Utilities.ShowMessage("size - 1", (size - 1).ToString());

            Vector3I p;
            for (p.Z = 0; p.Z < size.Z; ++p.Z)
                for (p.Y = 0; p.Y < size.Y; ++p.Y)
                    for (p.X = 0; p.X < size.X; ++p.X)
                    {
                        // place GPS in the center of the Voxel
                        var position = voxelMap.PositionLeftBottomCorner + (p * scale) + (scale / 2) + min;

                        if (Math.Sqrt((position - center).LengthSquared()) < distance)
                        {
                            var content = oldCache.Content(ref p);
                            var material = oldCache.Material(ref p);
                            
                            if (content > 0 && findMaterial.Contains(material))
                            {
                                var index = Array.IndexOf(findMaterial, material);
                                var name = materials[index].MinedOre;
                                Util.GetInstance().Log(hits+" Ore " + name+ " scanore "+ position, "findingOres.txt");
                                //var gps = MyAPIGateway.Session.GPS.Create("Ore " + name, "scanore", position, true, false);
                                //MyAPIGateway.Session.GPS.AddLocalGps(gps);
                                hits++;
                            }
                        }
                    }
            return hits;
        }
    }
}
