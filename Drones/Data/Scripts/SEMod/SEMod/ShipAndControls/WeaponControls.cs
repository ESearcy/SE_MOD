using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Game.Entities;
using Sandbox.Game.Gui;
using Sandbox.Game.Screens.Helpers;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Library.Collections;
using VRage.ModAPI;
using VRageMath;
using ITerminalAction = Sandbox.ModAPI.Interfaces.ITerminalAction;

namespace SEMod
{
    internal class WeaponControls
    {

        internal IMyPlayer _targetPlayer = null;
        internal IMyCubeGrid _target = null;
        private IMyEntity Ship;
        private static string _logPath = "WeaponControls";
        private long _ownerId;
        internal int _minTargetSize = 10;
        List<IMyTerminalBlock> allWeapons = new List<IMyTerminalBlock>();
        List<IMyTerminalBlock> directionalWeapons = new List<IMyTerminalBlock>();
        List<IMyTerminalBlock> turrets = new List<IMyTerminalBlock>();
        private IMyGridTerminalSystem _gridTerminalSystem;

        Dictionary<IMyCubeGrid, TargetDetails> targets = new Dictionary<IMyCubeGrid, TargetDetails>();

        List<IMyCubeGrid> _nearbyFloatingObjects = new List<IMyCubeGrid>();
        List<IMyVoxelBase> _nearbyAsteroids = new List<IMyVoxelBase>();
        private String fleetname;

        //same time no recalc
        private TimeSpan oneSecond;

        private ITerminalAction gunon = null;
        private ITerminalAction gunoff = null;
        private ITerminalAction poweron = null;
        private ITerminalAction poweroff = null;

        public void EnableWeapons()
        {
            try
            {
                directionalWeapons = directionalWeapons.Where(x => x != null && x.IsFunctional).ToList();
                //var launchers = directionalWeapons.Where(x => x.Name.Contains("launcher")).ToList();

                foreach (var weapon in directionalWeapons)
                {
                    if (poweron == null || gunon == null)
                    {
                        poweron = weapon.GetActionWithName("OnOff_On"); //.Apply(weapon);
                        gunon = weapon.GetActionWithName("Shoot_On"); //.Apply(weapon);
                    }
                    else
                    {
                        poweron.Apply(weapon);
                        gunon.Apply(weapon);
                        
                    }
                }
                //if (poweron != null && gunon != null)
                //    MyAPIGateway.Parallel.Start(() => FireLaunchers(launchers, poweron, gunon));
            }
            catch (Exception e)
            {
                Logger.LogException(e);
            }
        }



        private void FireLaunchers(List<IMyTerminalBlock> launchers, ITerminalAction onoff, ITerminalAction fire)
        {
            
                DateTime start = DateTime.Now;
                foreach (var launcher in launchers)
                {
                    while ((DateTime.Now - start).TotalMilliseconds < 100)
                    {
                    }
                    start = DateTime.Now;
                    
                    {
                        onoff.Apply(launcher);
                        fire.Apply(launcher);
                    }
                }
        }

        public void DisableWeapons()
        {
            try { 
                directionalWeapons = directionalWeapons.Where(x => x != null && x.IsFunctional).ToList();
                foreach (var weapon in directionalWeapons)
                {
                    if (poweroff == null || gunoff == null)
                    {
                        poweroff = weapon.GetActionWithName("Shoot_Off"); //.Apply(weapon);
                        gunoff = weapon.GetActionWithName("OnOff_Off"); //.Apply(weapon);
                    }
                    else
                    {
                        poweroff.Apply(weapon);
                        gunoff.Apply(weapon);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogException(e);
            }
        }

        public IMyTerminalBlock GetTargetKeyAttackPoint(IMyCubeGrid grid)
        {
            TargetDetails details;
            IMyTerminalBlock block = null;
            if (targets.TryGetValue(grid, out details))
            {
                block = details.GetBestHardPointTarget(Ship.GetPosition());
            }
            return block;
        }

        public WeaponControls(IMyEntity Ship, long ownerID, IMyGridTerminalSystem grid, String fleetname)
        {
            this.fleetname = fleetname;
            this.Ship = Ship;
            this._ownerId = ownerID;
            this._gridTerminalSystem = grid;
            DateTime start = new DateTime(1,1,1,1,1,1);
            DateTime end = new DateTime(1, 1, 1, 1, 1, 2);
            oneSecond = start - end;
            DetectWeapons();
        }

        public bool IsOperational()
        {
            Logger.Debug("number of weapons: " + GetWeaponsCount()+"turrets/other => "+turrets.Count+":"+directionalWeapons.Count);
            return GetWeaponsCount() > 0;
        }

        public int GetWeaponsCount()
        {
            allWeapons = allWeapons.Where(x => x != null).ToList();
            turrets = turrets.Where(x => x != null).ToList();

            allWeapons = allWeapons.Where(x => x.IsFunctional).ToList();
            turrets = turrets.Where(x => x.IsFunctional).ToList();
            return allWeapons.Count+turrets.Count;
        }

        private void DetectWeapons()
        {
            _gridTerminalSystem.GetBlocksOfType<IMyUserControllableGun>(allWeapons);
            _gridTerminalSystem.GetBlocksOfType<Sandbox.ModAPI.IMyLargeTurretBase>(turrets);
            directionalWeapons = allWeapons.Where(x=> !turrets.Contains(x)).ToList();
        }

        public void AddNearbyFloatingItem(IMyCubeGrid entity)
        {
            if (!_nearbyFloatingObjects.Contains(entity) && Ship != entity)
            {
                Logger.Debug("checking grid "+ entity.Name);
                _nearbyFloatingObjects.Add(entity);
                ScanTarget(entity);
            }
        }

        public void AddNearbyAsteroid(IMyVoxelBase entity)
        {
            if (!_nearbyAsteroids.Contains(entity) && Ship != entity)
            {
                _nearbyAsteroids.Add(entity);
            }
        }

        private void ScanTarget(IMyCubeGrid grid)
        {
            //save logic time, no need to rescan targets
            if (targets.ContainsKey(grid))
                return;
            
            List<Sandbox.ModAPI.IMyTerminalBlock> terminalBlocks = new List<IMyTerminalBlock>();
            _gridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(terminalBlocks);

            //powerproducers
            List<IMyTerminalBlock> reactorBlocks = terminalBlocks.Where(x => x is IMyReactor).ToList();
            List<IMyTerminalBlock> batteryBlocks = terminalBlocks.Where(x => x is IMyBatteryBlock).ToList();

            bool isOnline = (reactorBlocks.Exists(x => (x.IsWorking)) || batteryBlocks.Exists(x => (x.IsWorking)));

            bool isownerId = grid.SmallOwners.Contains(_ownerId);
            bool isInFaction = grid.SmallOwners.Count(IsPartOfFaction) > 0;


            if (!isOnline) return;
            
            Logger.Debug("is Owner: "+isownerId);

            var isEnemy = //!GridFriendly(terminalBlocks) && 
                !isownerId && !isInFaction;
            //Util.NotifyHud("shared ownership " + isownerId+" isEnemy:"+isEnemy);

            List<IMyTerminalBlock> remoteControls = terminalBlocks.Where(x => x is IMyRemoteControl).ToList();

            bool isDrone = remoteControls.Exists(x => ((IMyRemoteControl)x).CustomName.Contains("Drone#"));

            Logger.Debug("is Enemy: " + isEnemy);
            if (isEnemy)
                targets.Add(grid, new TargetDetails(grid, isDrone));
        }

       

        private bool GridFriendly(List<IMyTerminalBlock> gridblocks)
        {
            bool isFriendly = false;
            //Dictionary<MyRelationsBetweenPlayerAndBlock, int> uniqueIds = new Dictionary<MyRelationsBetweenPlayerAndBlock, int>();
            //var myfaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(_ownerId);
            //int numUnknownFactions = 0;
            //bool hasFriendlyBlock = false;
            //bool isownerId = gridblocks.SmallOwners.Contains(_ownerId);
            int fs = 0;
            int n = 0; int no = 0; int o = 0; int e = 0;
            foreach (var block in gridblocks)
            {
                switch (block.GetUserRelationToOwner(_ownerId))
                {
                    
                    case MyRelationsBetweenPlayerAndBlock.Owner:
                        //isFriendly = true;
                        break;
                    case MyRelationsBetweenPlayerAndBlock.FactionShare:
                        isFriendly = true;
                        break;
                    case MyRelationsBetweenPlayerAndBlock.Neutral:
                        break;
                    case MyRelationsBetweenPlayerAndBlock.NoOwnership:
                        break;
                    case MyRelationsBetweenPlayerAndBlock.Enemies:
                        break;
                }
                if (isFriendly)
                    break;
            }
            //Util.NotifyHud("fs:"+fs+ " n:" + n + " no:" + no + " o:" + o + " e:" + e);


            return isFriendly;

            ////this shit doesnt work even though it should, maybe i just did it wrong.

        }

        private bool IsPartOfFaction(long id)
        {
            Logger.Debug("looking for faction name: "+fleetname);
            var faction = MyAPIGateway.Session.Factions.TryGetFactionByName(fleetname);
            
            try
            {
                if (faction != null)
                {
                    var myfaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(id);

                    if (myfaction != null)
                    {
                        if (myfaction.FactionId == faction.FactionId)
                        {
                            Logger.Debug("Factions Match: " + myfaction.Name+":"+faction.Name);
                            Logger.Debug("Same faction");
                            return true;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogException(e);
            }

            return false;
        }

        public void ClearNearbyObjects()
        {
            _nearbyFloatingObjects.Clear();
            _nearbyAsteroids.Clear();
        }
        TargetDetails targetDetails = null;

        DateTime _targetAquiredTime = DateTime.Now;
        //always gets the most current nearest target (causes switching alot in fleet fights)
        public TargetDetails GetEnemyTarget()
        {
            Logger.Debug("Number of Targets: "+targets.Count);
            //Util.NotifyHud("targets count: "+targets.Count);
            targets = targets.Where(x=>x.Value.IsOperational()).OrderBy(x => (x.Key.GetPosition() - Ship.GetPosition()).Length()).ToDictionary(x => x.Key, x => x.Value);

            if ((DateTime.Now - _targetAquiredTime).Seconds > 10 || (targetDetails!=null && !targetDetails.IsOperational()))
            {
                _targetAquiredTime = DateTime.Now;
                targetDetails = null;
            }
            //first case is caught first to avoid null exception
            if (targetDetails ==null || !targetDetails.IsOperational())
                targetDetails = null;

            if (targetDetails == null && targets.Count > 0)
            {
                var orderedTargets = targets.OrderBy(x => (x.Key.GetPosition() - Ship.GetPosition()).Length());
                targetDetails = orderedTargets.First().Value;
            }
            if (targetDetails != null && !targetDetails.IsOperational())
                targetDetails = null;

            return targetDetails;
        }

        public List<IMyCubeGrid> GetObjectsInRange(int range)
        {
            List<IMyCubeGrid> keyValuePairs = _nearbyFloatingObjects.Where(x => (x.GetPosition() - Ship.GetPosition()).Length()<=range).ToList();
            //Util.NotifyHud(keyValuePairs.Count+" count");
            return keyValuePairs;
        }

        public IMyPlayer GetEnemyPlayer()
        {
            return _targetPlayer;
        }

        public float GetTargetSize(IMyCubeGrid enemyTarget)
            {
                float radiusOfTarget = 1000;//default
                if (targets.ContainsKey(enemyTarget))
                {
                    radiusOfTarget = (float)targets[enemyTarget].ShipSize;
                }

                return radiusOfTarget;
            }

        int count = 0;
        public void DebugMarkAllTrackedObjects()
        {
            long id = MyAPIGateway.Session.Player.IdentityId;
            count = 0;
            Logger.Debug(targets.Count+"");
            foreach (var obj in _nearbyFloatingObjects)
            {
                count++;
                IMyGps mygps = MyAPIGateway.Session.GPS.Create("X", "", obj.GetPosition(), true, true);
                MyAPIGateway.Session.GPS.AddGps(id, mygps);
            }
        }
        public void DebugMarkAllTrackedTargets()
        {
            long id = MyAPIGateway.Session.Player.IdentityId;
            count = 0;
            Logger.Debug(targets.Count + "");
            foreach (var obj in targets)
            {
                count++;
                IMyGps mygps = MyAPIGateway.Session.GPS.Create("X", "", obj.Key.GetPosition(), true, true);
                MyAPIGateway.Session.GPS.AddGps(id, mygps);
            }
        }

        public void DebugMarkTargetAndKeyPoint()
        {
            TargetDetails target = GetEnemyTarget();
            if (target != null)
            {
                var targetBlock = target.GetBestHardPointTarget(Ship.GetPosition());

                long id = MyAPIGateway.Session.Player.IdentityId;
                IMyGps mygps = MyAPIGateway.Session.GPS.Create("X", "", target.Ship.GetPosition(), true, true);
                MyAPIGateway.Session.GPS.AddGps(id, mygps);

                if (targetBlock != null)
                {
                    IMyGps mygps2 = MyAPIGateway.Session.GPS.Create("X", "", targetBlock.GetPosition(), true, true);
                    MyAPIGateway.Session.GPS.AddGps(id, mygps2);
                }
            }
        }

        internal void ClearTargets()
        {
            targets.Clear();
        }

        internal List<IMyVoxelBase> GetAsteroids(int range)
        {
            return _nearbyAsteroids.Where(x => (x.GetPosition() - Ship.GetPosition()).Length() <= range).ToList();
        }

        internal int GetNumberTargets()
        {
            return targets.Count;
        }
    }
}