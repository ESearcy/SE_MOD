using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using DroneConquest;
using Sandbox.Common;
using Sandbox.Game.Entities;
using Sandbox.Game.Gui;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;
using IMyControllableEntity = VRage.Game.ModAPI.Interfaces.IMyControllableEntity;
using IMyGyro = Sandbox.ModAPI.IMyGyro;
using IMyReactor = Sandbox.ModAPI.Ingame.IMyReactor;
using IMyShipDrill = Sandbox.ModAPI.IMyShipDrill;
using IMyTerminalBlock = Sandbox.ModAPI.Ingame.IMyTerminalBlock;
using IMyThrust = Sandbox.ModAPI.IMyThrust;
using ITerminalAction = Sandbox.ModAPI.Interfaces.ITerminalAction;

namespace MiningDrones
{
    public class SpacePirateShip : Drone
    {

        private static string logPath = "SpacePirateShip.txt";
        List<IMyEntity> _nearbyStuff = new List<IMyEntity>();
        private Sandbox.ModAPI.IMyGridTerminalSystem _gridTerminal;
        AsteroidManager _aMananager = new AsteroidManager();

        public SpacePirateShip(IMyCubeGrid grid): base(grid)
        {
            _gridTerminal = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(grid);
            //tgControl = new ThrusterGyroControls(grid, ShipControls as IMyRemoteControl);
            //_navigation = new DroneNavigation(grid, ShipControls, _nearbyStuff);

            BootUp(grid, ShipControls);
            
        }

        private void BootUp(IMyCubeGrid grid, IMyControllableEntity control)
        {
            GetMiningDrillActions();
        }

        private ITerminalAction _blockOn;
        private ITerminalAction _blockOff;


        private void GetMiningDrillActions()
        {
            List<IMySlimBlock> blocks = new List<IMySlimBlock>();
            Ship.GetBlocks(blocks, (x) => x.FatBlock != null && x.FatBlock is IMyShipDrill);
            if ((_blockOn == null || _blockOff == null) && blocks.Count > 0)
            {
                List<ITerminalAction> actions = new List<ITerminalAction>();
                var block = (IMyShipDrill)blocks[0].FatBlock;
                block.GetActions(actions);

                string actionsString = "";
                actions.ForEach(x => actionsString += ", "+x.Name);


                //_fireRocket = block.GetActionWithName("Shoot_once");
                //_fireGun = block.GetActionWithName("Shoot");
                _blockOff = block.GetActionWithName("OnOff_Off");
                _blockOn = block.GetActionWithName("OnOff_On");

                
            }
            //DrillsOn();
        }

        private void DrillsOn()
        {
            List<IMySlimBlock> blocks = new List<IMySlimBlock>();
            Ship.GetBlocks(blocks, (x) => x.FatBlock != null && x.FatBlock is IMyShipDrill);
            var drills = blocks.Select(x => x.FatBlock).ToList();
            drills.ForEach(x=>_blockOn.Apply(x));
        }

        internal void AltitudeTest()
        {
            Util.GetInstance().Notify("MK 1");
            Hover();
            
        }

        private void DrillsOff()
        {
            List<IMySlimBlock> blocks = new List<IMySlimBlock>();
            Ship.GetBlocks(blocks, (x) => x.FatBlock != null && x.FatBlock is IMyShipDrill);
            var drills = blocks.Select(x => x.FatBlock).ToList();
            drills.ForEach(x => _blockOff.Apply(x));
        }

        private int MaxRange = 2000;

        private void FindNearbyStuff()
        {
            var bs = new BoundingSphereD(Ship.GetPosition(), MaxRange);
            var ents = MyAPIGateway.Entities.GetEntitiesInSphere(ref bs);
            var closeBy = ents;//entitiesFiltered.Where(z => (z.GetPosition() - drone.GetPosition()).Length() < MaxEngagementRange).ToList();

            //var closeAsteroids = asteroids.Where(z => (z.GetPosition() - drone.GetPosition()).Length() < MaxEngagementRange).ToList();

            tc.ClearNearbyObjects();
            foreach (var closeItem in closeBy)
            {
                if (closeItem is IMyCubeGrid && !closeItem.Transparent && closeItem.Physics.Mass > 2000)
                    tc.AddNearbyFloatingItem(closeItem);

                if (closeItem is IMyVoxelBase)
                {
                    //_aMananager.Scan((IMyVoxelBase)closeItem);
                }
            }
        }

        private int _ticks = 0;
        Vector3D origin = new Vector3D(0,0,0);
        public void Update(Vector3D location)
        {
            if (_ticks%10 == 0)
            {
                FindNearbyStuff();
                ReloadWeaponsAndReactors();
            }

            _ticks++;
            //AltitudeTest();
            if (!ControlledByPlayer())
                Guard(origin);
            else
            {
                DisableThrusterGyroOverrides();
            }

            SetBroadcasting(true);
            NameBeacon();
        }

        

        private void FindAndMineOre()
        {
            Util.GetInstance().Log("Find and Mine ore ", "mining.txt");
            var nearbyRock = _aMananager.NearestAsteriodWithOre(GetLocation());

            foreach(var ore in nearbyRock.Value.Ores)
            {
                Util.GetInstance().Log(ore.Key + " at " + ore.Value.Count, "mining.txt");
            }
        }

        public Vector3D GetLocation()
        {
            //throw new NotImplementedException();
            return GetPosition();
        }

        public void FlyToLocation(Vector3D location)
        {
            Util.GetInstance().Log("approaching location", logPath);
            //AlignTo(location);
            Approach(location);
            //navigation.ApproachLocation(location);
        }

        public void FlyToLocationAndFromLocationWhileAimingAtLocation(Vector3D to, Vector3D from, Vector3D target)
        {
            
        }
    }
}
