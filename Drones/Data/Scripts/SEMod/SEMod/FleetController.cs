using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace SEMod
{
    class FleetController
    {
        public String LogPath;
        private IMyCubeGrid station;
        List<Ship> fighters = new List<Ship>();
        List<Ship> ships = new List<Ship>();
        public long PlayerId;
        private int maxNumberOfFighters = 0;
        private int maxNumberOfShips = 0;
        private int fighterUpdate = 0;
        private ShipTypes fighterShip;
        DateTime lastfighterSpawnedAt = DateTime.Now;
        private Vector3D spawnZone;
        private int maxFighterDristDistanceBeforeDead = 10000;
        private ShipTypes largeShip;

        public FleetController(String fleetName, long playerid, ShipTypes fighter, ShipTypes largeShip, Vector3D spawnLocation, int maxFighterCount, int maxshipcount)
        {
            this.largeShip = largeShip;
            maxNumberOfFighters = maxFighterCount;
            maxNumberOfShips = maxshipcount;
            LogPath = fleetName;
            PlayerId = playerid;
            fighterShip = fighter;
            spawnZone = spawnLocation;
            numberOfFleets++;
            fleetNum = numberOfFleets;
        }

        private static int numberOfFleets = 0;
        private int fleetNum;

        private int ticks = 0;
        public void Update()
        {
            try
            {
                Logger.Debug("****************Updating " + LogPath);
                ticks++;

                if (ticks % fleetNum != 0)
                    return;

                shipUpdate++;
                fighterUpdate++;


                Logger.Debug("Update: fighter count:  " + fighters.Count);

                if (fighters.Count > 0)
                    CalculateFighterMovements();

                if (ships.Count > 0)
                    CalculateShipMovements();

                if (ships.Count < maxNumberOfShips)
                {
                    if ((DateTime.Now - lastfighterSpawnedAt).TotalSeconds > 10)
                    {
                        Logger.Debug("spawning ship");
                        lastfighterSpawnedAt = DateTime.Now;
                        //SpawnFighter();
                        SpawnLargeShip();
                    }
                }
                if (fighters.Count < maxNumberOfFighters* ships.Count)
                {
                    if ((DateTime.Now - lastfighterSpawnedAt).TotalSeconds > 10)
                    {
                        Logger.Debug("spawning fighter");
                        lastfighterSpawnedAt = DateTime.Now;
                        SpawnFighter();
                        //SpawnLargeShip();
                    }
                }
                
            }
            catch (Exception e)
            {
                Logger.LogException(e);
            }
        }


        private int cou = 0;
        private void AttemptJoinFleet()
        {
            var faction = MyAPIGateway.Session.Factions.TryGetFactionByName(LogPath);
            

            try
            {
                if (faction != null)
                {
                    var cfaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(PlayerId);

                    if (cfaction == null)
                    {
                        MyAPIGateway.Session.Factions.AddNewNPCToFaction(faction.FactionId);
                    }

                    List<IMyIdentity> identities = new List<IMyIdentity>();
                    MyAPIGateway.Players.GetAllIdentites(identities);
                    //MyAPIGateway.Players.
                    foreach (var id in identities)
                    {
                        if (id.DisplayName.Contains(faction.Tag))
                        {
                            PlayerId = id.IdentityId;
                            Logger.Debug(" Changing fleet Identity to be: " + LogPath + " playerid: " + PlayerId +
                                         " name: " + id.DisplayName);
                            return;
                        }

                    }
                }
                else
                {
                    List<IMyPlayer> ids = new List<IMyPlayer>();
                    if (MyAPIGateway.Session.IsServer)
                        MyAPIGateway.Multiplayer.Players.GetPlayers(ids);
                    else
                    {
                        MyAPIGateway.Session.Factions.CreateFaction(MyAPIGateway.Session.Player.IdentityId, "AI"+cou, LogPath, "beware", "");
                        cou++;
                    }
                    if (ids.Any())
                    {
                        var workingid = ids.First().IdentityId;
                        MyAPIGateway.Session.Factions.CreateFaction(workingid, "AI"+ cou, LogPath, "beware", "");
                        cou++;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogException(e);
            }
        }
        

        internal bool HasFighterEntity(IMyEntity x)
        {
            return fighters.Count(y=>y._cubeGrid as IMyEntity == x) > 0;
        }

        private int numspawnedfighters = 0;
        private void SpawnFighter()
        {
            //if (numspawnedfighters > 0)
            //    return;

            //numspawnedfighters++;
            var ship = ships.First(x => fighters.Count(yx => yx.getCommandShip() == x) < maxNumberOfFighters);
            TestExecutor.SpawnShip(fighterShip, ship?.GetPosition() ?? spawnZone, PlayerId);
        }

        private void SpawnLargeShip()
        {
            TestExecutor.SpawnShip(largeShip, spawnZone, PlayerId);
        }

        private int shipUpdate = 0;
        private void CalculateShipMovements()
        {
            try
            {
                Logger.Debug("CalculateFleetMovements.");
                var killedships = ships.Where(x => !x.IsOperational()).ToList();
                ships = ships.Where(x => x.IsOperational()).ToList();

                shipUpdate = shipUpdate >= ships.Count ? 0 : shipUpdate;

                if (ships.Count == 0)
                {
                    Logger.Debug("== no ships to update");
                    return;
                }

                foreach (var killedship in killedships)
                {
                    Logger.Debug("ship has been destoried ");
                    killedship.DeleteShip();
                }

                var ship = ships[shipUpdate];
                //foreach (var fighter in fighters)
                //{
                if ((DateTime.Now - ship.lastUpdate).TotalSeconds >= 10)
                {


                    ship.ScanLocalArea();
                    ;
                    ship.Detonate();
                    if ((DateTime.Now - ship.lastReload).TotalSeconds >= 60)
                    {
                        Logger.Debug("Update: reloading weapons/reactors.");
                        ship.ReloadWeaponsAndReactors();
                        ship.lastReload = DateTime.Now;
                    }

                    AttemptJoinFleet();
                    ship.lastUpdate = DateTime.Now;
                }

                //Vector3D loc = MyAPIGatewayShortcuts.GetLocalPlayerPosition();

                ship.SetOwner(PlayerId);
                ship.Update();
                ship.ReportDiagnostics();
                //}
            }
            catch (Exception e)
            {
                Logger.LogException(e);
            }
        }

        private void CalculateFighterMovements()
        {
            try
            {
                Logger.Debug("CalculateFleetMovements.");
                var killedfighters = fighters.Where(x => !x.IsOperational() || (x.GetPosition() - x.defaultOrbitPoint).Length() >= maxFighterDristDistanceBeforeDead).ToList();
                fighters = fighters.Where(x => x.IsOperational() && (x.GetPosition() - x.defaultOrbitPoint).Length() < maxFighterDristDistanceBeforeDead).ToList();

                fighterUpdate = fighterUpdate >= fighters.Count ? 0 : fighterUpdate;

                if (fighters.Count == 0)
                {
                    Logger.Debug("== no fighters to update");
                    return;
                }

                foreach (var killedfighter in killedfighters)
                {
                    Logger.Debug("Fighter has been destoried ");
                    //killedfighter.DeleteShip();
                }

                var fighter = fighters[fighterUpdate];

                if(!fighter.HasCommandShip())
                    fighter.SetCommandShip(ships.First(x => fighters.Count(yx=> yx.getCommandShip() == x) < maxNumberOfFighters));
                //foreach (var fighter in fighters)
                //{
                if ((DateTime.Now - fighter.lastUpdate).TotalSeconds >= 10)
                    {
                        

                        fighter.ScanLocalArea();
;
                        fighter.Detonate();
                        if ((DateTime.Now - fighter.lastReload).TotalSeconds >= 60)
                        {
                            Logger.Debug("Update: reloading weapons/reactors.");
                            fighter.ReloadWeaponsAndReactors();
                            fighter.lastReload = DateTime.Now;
                        }

                        fighter.lastUpdate = DateTime.Now;
                    }

                //Vector3D loc = MyAPIGatewayShortcuts.GetLocalPlayerPosition();

                fighter.SetOwner(PlayerId);
                fighter.UpdateFighter();

                fighter.ReportDiagnostics();
                //}
            }
            catch (Exception e)
            {
                Logger.LogException(e);
            }
        }
        

        internal void AddFighterToFleet(Ship ship, ShipTypes droneType)
        {
            if (!ship.IsOperational() || fighters.Exists(x => x._cubeGrid == ship._cubeGrid))
                return;
            ship.SetOwner(PlayerId);
            ship.SetFleetZone(ship.GetPosition());
            fighters.Add(ship);
            //MyAPIGateway.Session.Factions.AddNewNPCToFaction(12);
        }

        internal int GetNumberOfFighters()
        {
            return fighters.Count();
        }

        public void AddShipToFleet(Ship ship, ShipTypes droneType)
        {
            if (!ship.IsOperational() || ships.Exists(x => x._cubeGrid == ship._cubeGrid))
                return;
            ship.SetOwner(PlayerId);
            ship.SetFleetZone(ship.GetPosition());
            ships.Add(ship);
        }
    }
}
