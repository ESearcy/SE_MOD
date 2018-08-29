using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;
using IMyReactor = Sandbox.ModAPI.IMyReactor;
using IMyTerminalBlock = Sandbox.ModAPI.Ingame.IMyTerminalBlock;

namespace MiningDrones
{
    

    class TargetingControls
    {
        internal IMyPlayer _targetPlayer = null;
        internal IMyCubeGrid _target = null;
        private IMyEntity Ship;
        private static string logPath = "TargetingControls.txt";
        private long _ownerId;
        internal int _minTargetSize = 10;

        
        Dictionary<IMyCubeGrid, TargetDetails> targets = new Dictionary<IMyCubeGrid, TargetDetails>();

        List<IMyEntity> _nearbyFloatingObjects = new List<IMyEntity>();

        public IMyTerminalBlock GetTargetKeyAttackPoint(IMyCubeGrid grid)
        {
            TargetDetails details;
            IMyTerminalBlock block = null;
            if (targets.TryGetValue(grid, out details))
            {
                block = details.GetTargetKeyAttackPoint();
            }
            return block;
        }

        public TargetingControls(IMyEntity Ship, long ownerID)
        {
            this.Ship = Ship;
            this._ownerId = ownerID;
        }

        public void AddNearbyFloatingItem(IMyEntity entity)
        {
            if(!_nearbyFloatingObjects.Contains(entity))
                _nearbyFloatingObjects.Add(entity);
        }

        public IMyCubeGrid FindEnemyTarget()
        {
            _target = null;
            Dictionary<IMyEntity, IMyEntity> nearbyOnlineShips = new Dictionary<IMyEntity, IMyEntity>();
            Dictionary<IMyEntity, IMyEntity> nearbyDrones = new Dictionary<IMyEntity, IMyEntity>();
            bool targetSet = false;
            for (int i = 0; i < _nearbyFloatingObjects.Count; i++)
            {
                if ((_nearbyFloatingObjects.ToList()[i].GetPosition() - Ship.GetPosition()).Length() > 10)
                {
                    var entity = _nearbyFloatingObjects.ToList()[i];

                    var grid = entity as IMyCubeGrid;
                    if (grid != null)
                    {
                        var gridTerminal = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(grid);

                        List<Sandbox.ModAPI.Ingame.IMyTerminalBlock> val = new List<IMyTerminalBlock>();
                        gridTerminal.GetBlocks(val);
                        var isFriendly = GridFriendly(val);

                        List<IMyTerminalBlock> T = new List<IMyTerminalBlock>();
                        gridTerminal.GetBlocksOfType<IMyRemoteControl>(T);

                        List<IMyTerminalBlock> reactorBlocks = new List<IMyTerminalBlock>();
                        gridTerminal.GetBlocksOfType<IMyReactor>(reactorBlocks);

                        List<IMyTerminalBlock> batteryBlocks = new List<IMyTerminalBlock>();
                        gridTerminal.GetBlocksOfType<IMyBatteryBlock>(batteryBlocks);


                        bool isOnline =
                            (reactorBlocks.Exists(x => (x.IsWorking)) ||
                             batteryBlocks.Exists(x => (x.IsWorking))) && !isFriendly;

                        bool isDrone =
                            T.Exists(
                                x =>
                                    (((IMyRemoteControl) x).CustomName.Contains("Drone#") && x.IsWorking &&
                                     !isFriendly));


                        var droneControl =
                            (IMyEntity)
                                T.FirstOrDefault(
                                    x => ((IMyRemoteControl) x).CustomName.Contains("Drone#") && x.IsWorking &&
                                         !isFriendly);

                        var shipPower =
                            (IMyEntity)
                                reactorBlocks.FirstOrDefault(
                                    x => x.IsWorking && !isFriendly);

                        if (isDrone && isOnline)
                        {
                            nearbyDrones.Add(grid, droneControl);
                        }
                        else if (isOnline)
                        {
                            if (!nearbyOnlineShips.ContainsKey(grid))
                                nearbyOnlineShips.Add(grid, shipPower ?? droneControl);
                        }
                    }
                }
            }

            if (nearbyDrones.Count > 0)
            {
                var myTarget =
                    nearbyDrones
                        .OrderBy(x => (x.Key.GetPosition() - Ship.GetPosition()).Length())
                        .ToList();

                if (myTarget.Count > 0)
                {
                    var target = myTarget[0];


                    IMyGridTerminalSystem gridTerminal =
                        MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid((IMyCubeGrid) target.Key);
                    List<IMyTerminalBlock> T = new List<IMyTerminalBlock>();
                    gridTerminal.GetBlocks(T);

                    if (T.Count >= _minTargetSize)
                    {
                        if (!targetSet)
                        {
                            _target = (IMyCubeGrid)target.Key;
                            _targetPlayer = null;
                            targetSet = true;
                            
                        }
                        if (!targets.ContainsKey(_target))
                            targets.Add(_target, new TargetDetails(_target));

                    }
                }
            }

            if (nearbyOnlineShips.Count > 0)
            {
                var myTargets =
                    nearbyOnlineShips
                        .OrderBy(x => (x.Key.GetPosition() - Ship.GetPosition()).Length())
                        .ToList();

                foreach (var target in myTargets)
                {

                    IMyGridTerminalSystem gridTerminal =
                        MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid((IMyCubeGrid) target.Key);
                    List<IMyTerminalBlock> T = new List<IMyTerminalBlock>();
                    gridTerminal.GetBlocks(T);

                    if (T.Count >= _minTargetSize)
                    {
                        if (!targetSet)
                        {
                            _target = (IMyCubeGrid) target.Key;
                            _targetPlayer = null;
                            targetSet = true;
                        }
                        if (!targets.ContainsKey(_target))
                            targets.Add(_target, new TargetDetails(_target));

                    }
                }
            }
            return _target;
        }

        private bool GridFriendly(List<IMyTerminalBlock> gridblocks)
        {
            bool isFriendly = false;
            //Dictionary<MyRelationsBetweenPlayerAndBlock, int> uniqueIds = new Dictionary<MyRelationsBetweenPlayerAndBlock, int>();
            //var myfaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(_ownerId);
            //int numUnknownFactions = 0;
            //bool hasFriendlyBlock = false;
            foreach (var block in gridblocks)
            {
                switch (((IMyCubeBlock)block).GetUserRelationToOwner(_ownerId))

                {
                    case MyRelationsBetweenPlayerAndBlock.FactionShare:
                        isFriendly = true;
                        break;
                    case MyRelationsBetweenPlayerAndBlock.Neutral:
                        break;
                    case MyRelationsBetweenPlayerAndBlock.NoOwnership:
                        break;
                    case MyRelationsBetweenPlayerAndBlock.Owner:
                        isFriendly = true;
                        break;
                    case MyRelationsBetweenPlayerAndBlock.Enemies:
                        break;
                }
                if (isFriendly)
                    break;
            }



            return isFriendly;

            ////this shit doesnt work even though it should, maybe i just did it wrong.
            
        }

        public void ClearNearbyObjects()
        {
            _nearbyFloatingObjects.Clear();
        }

        

        public IMyCubeGrid GetEnemyTarget()
        {
            return _target;
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
    }

}
