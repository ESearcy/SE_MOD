using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using VRage.Game;
using VRage.Game.ModAPI.Ingame;
using VRageMath;
using SpaceEngineers.Game.ModAPI.Ingame;
using SEMod.INGAME.classes;
using SEMod.INGAME.classes.systems;
using SEMod.INGAME.classes.model;

namespace SEMod.INGAME.classes.systems
{
    //////
    public class TrackingSystem
    {
        private Logger log;
        private IMyCubeGrid cubeGrid;
        private ShipComponents shipComponets;

        List<TrackedEntity> trackedEntities = new List<TrackedEntity>();
        List<PlanetaryData> KnownPlanets = new List<PlanetaryData>();

        public TrackingSystem(Logger log, IMyCubeGrid cubeGrid, ShipComponents shipComponets)
        {
            this.log = log;
            this.cubeGrid = cubeGrid;
            this.shipComponets = shipComponets;
        }

        internal bool IsOperational()
        {
            return trackedEntities.Count()>0 || KnownPlanets.Count()>0;
        }

        public void UpdateTrackedEntity(ParsedMessage pm, bool selfcalled)
        {
            TrackedEntity te = trackedEntities.Where(x => x.EntityID == pm.TargetEntityId).FirstOrDefault();

            if (te == null)
            {
                te = new TrackedEntity(pm,log);
                trackedEntities.Add(te);
            }

            te.Location = pm.Location;
            te.Velocity = pm.Velocity;
            te.LastUpdated = DateTime.Now;
            te.Radius = pm.TargetRadius;
            te.DetailsString = pm.ToString();
            te.Relationship = pm.Relationship;

            if (pm.AttackPoint != Vector3D.Zero)
            {
                te.UpdatePoints(new PointOfInterest(pm.AttackPoint));
            }

            if (selfcalled)
            {
                te.UpdateNearestPoints(new PointOfInterest(pm.AttackPoint), cubeGrid.GetPosition());
            }
        }

        List<String> idsFound = new List<string>();
        public void UpdatePlanetData(ParsedMessage pm, bool selfcalled)
        {
            //log.Debug("attempting to update planet data");
            
            var lastfour = (pm.EntityId + "");
            lastfour = lastfour.Substring(lastfour.Length - 4);
            if (!idsFound.Contains(lastfour + ""))
            {
                idsFound.Add(lastfour + "");
                //log.Debug(lastfour + " Processed" );
            }

            var existingPlanet = KnownPlanets.Where(x => x.PlanetCenter == pm.Location).FirstOrDefault();
            if (existingPlanet != null)
            {
                existingPlanet.UpdatePlanetaryData(new Region(pm.TargetEntityId, pm.Location, new PointOfInterest(pm.AttackPoint), cubeGrid.GetPosition()), cubeGrid.GetPosition());
                //log.Debug("updated planet data");
            }
            else
            {
                KnownPlanets.Add(new PlanetaryData(log, pm.Location, new Region(pm.TargetEntityId, pm.Location, new PointOfInterest(pm.AttackPoint), cubeGrid.GetPosition()), cubeGrid.GetPosition()));
                //log.Debug("Logged New planet discovery: "+ pm.Location);
            }

        }

        PlanetaryData nearestPlanet;
        double altitude = 0;

        public PlanetaryData GetNearestPlanet()
        {
            var np = KnownPlanets.OrderBy(y=> (y.GetNearestPoint(cubeGrid.GetPosition()) - cubeGrid.GetPosition()).Length()).FirstOrDefault();
            return np;
        }

        public void UpdatePlanetData()
        {
            nearestPlanet = GetNearestPlanet();
            if(nearestPlanet!=null)
                altitude = Math.Abs((cubeGrid.GetPosition() - nearestPlanet.GetNearestPoint(cubeGrid.GetPosition())).Length());
        }

        internal double GetAltitude()
        {
            return altitude;
        }

        internal List<TrackedEntity> getTargets()
        {
            return trackedEntities;
        }

        internal TrackedEntity GetEntity(long targetEntityID)
        {
            return trackedEntities.Where(x => x.EntityID == targetEntityID).FirstOrDefault();
        }

        public List<TrackedEntity> getCombatTargets(Vector3D point)
        {
            var targetsOfConcern = trackedEntities.Where(x=>(x.GetNearestPoint(point)-point).Length()<3000 && x.Radius > 50 && x.Relationship!=MyRelationsBetweenPlayerAndBlock.Owner && (DateTime.Now-x.LastUpdated).TotalMinutes < 5);

            return targetsOfConcern.ToList();
        }

        internal PointOfInterest GetNextMiningTestPoint(Vector3D point)
        {
            var nearestUncheckedRegion = GetNearestPlanet().Regions
                .OrderBy(x => (x.surfaceCenter - point).Length())
                .Where(x=>x.PointsOfInterest.Count(y=>y.Reached)<3)
                .FirstOrDefault();

            return nearestUncheckedRegion.PointsOfInterest.Where(x=>!x.Reached).OrderBy(x=>(x.Location-point).Length()).FirstOrDefault();
        }
    }
    //////
}
