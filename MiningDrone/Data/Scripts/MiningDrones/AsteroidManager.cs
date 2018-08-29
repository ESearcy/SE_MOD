using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.ModAPI;
using VRage.Voxels;
using VRageMath;

namespace MiningDrones
{
    class AsteroidManager
    {

        private static String logPath = "AsteroidManager.txt";
        Dictionary<IMyVoxelBase, AsteroidInfo> _scannedAsteroids = new Dictionary<IMyVoxelBase, AsteroidInfo>();
        public void Scan(IMyVoxelBase asteroid)
        {
            try
            {
                FindMaterial(asteroid, asteroid.GetPosition(), 4, asteroid.LocalAABB.Max.Normalize());
            }
            catch (Exception e)
            {
                Util.GetInstance().LogError(e.ToString());
            }
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
            Util.GetInstance().Log("Finding ores","ores.txt");
            var tmp = voxelMap as MyVoxelMap;
            int hits = 0;
            var materials = MyDefinitionManager.Static.GetVoxelMaterialDefinitions().Where(v => v.IsRare).ToArray();
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

            Dictionary<string, List<Vector3D>> ores = new Dictionary<string, List<Vector3D>>();
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
                                if (!ores.ContainsKey(name))
                                {
                                    ores.Add(name,new List<Vector3D>());
                                }
                                ores[name].Add(position);
                                Util.GetInstance().Log(hits + " Ore " + name + " scanore " + position, "ores.txt");
                                //var gps = MyAPIGateway.Session.GPS.Create("Ore " + name, "scanore", position, true, false);
                                //MyAPIGateway.Session.GPS.AddLocalGps(gps);
                                hits++;
                            }
                        }
                    }
            if (!_scannedAsteroids.ContainsKey(voxelMap))
            {
                _scannedAsteroids.Add(voxelMap, new AsteroidInfo(){Ores = ores});
            }
            else
            {
                var info = _scannedAsteroids[voxelMap];
                _scannedAsteroids.Remove(voxelMap);
                _scannedAsteroids.Add(voxelMap, new AsteroidInfo() { Ores = ores });
            }
            return hits;
        }

        public KeyValuePair<IMyVoxelBase, AsteroidInfo> NearestAsteriodWithOre(Vector3D loc)
        {
            Util.GetInstance().Log("nearest asteroid ","mining.txt");
            var tmp = _scannedAsteroids.Where(p=>p.Value.Ores.Count>0).OrderBy(x => (x.Key.GetPosition() - loc).Length()).ToList();
            Util.GetInstance().Log(tmp.FirstOrDefault().Value.Ores.Count + " " + _scannedAsteroids.Count, "mining.txt");
            return tmp.FirstOrDefault();

        }
    }
}
