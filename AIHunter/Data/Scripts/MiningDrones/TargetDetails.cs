using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Game.Entities;
using VRage.Game.ModAPI;
using IMyTerminalBlock = Sandbox.ModAPI.Ingame.IMyTerminalBlock;

namespace MiningDrones
{
    class TargetDetails
    {
        private static string logPath = "targetDetails.txt";
        public DateTime LastScannedTime = DateTime.Now;
        public IMyCubeGrid Ship;
        private List<IMyTerminalBlock> _keyPoints = new List<IMyTerminalBlock>();
        private static int ShipRescanRate = 30;
        public double ShipSize = 0;

        public TargetDetails(IMyCubeGrid ship)
        {
            Ship = ship;
            FindTargetKeyPoint();
        }

        public IMyTerminalBlock GetTargetKeyAttackPoint()
        {
            if ((DateTime.Now - LastScannedTime).TotalSeconds > ShipRescanRate && Ship != null)
            {
                FindTargetKeyPoint();
                LastScannedTime = DateTime.Now;
            }

            _keyPoints = _keyPoints.Where(x => x.IsFunctional).ToList();
            return _keyPoints.FirstOrDefault();
        }

        public void FindTargetKeyPoint()
        {
            IMyCubeGrid grid = Ship;
            var centerPosition = Ship.GetPosition();
            _keyPoints.Clear();
            //get position, get lenier velocity in each direction
            //add them like 10 times and add that to current coord
            if (grid != null)
            {
                Sandbox.ModAPI.IMyGridTerminalSystem gridTerminal =
                    Sandbox.ModAPI.MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(grid);

                List<IMyTerminalBlock> reactorBlocks = new List<IMyTerminalBlock>();
                gridTerminal.GetBlocksOfType<Sandbox.ModAPI.IMyReactor>(reactorBlocks);

                //List<IMyTerminalBlock> solarBlocks = new List<IMyTerminalBlock>();
                //gridTerminal.GetBlocksOfType<IMySolarPanel>(solarBlocks);

                List<IMyTerminalBlock> batteryBlocks = new List<IMyTerminalBlock>();
                gridTerminal.GetBlocksOfType<Sandbox.ModAPI.IMyBatteryBlock>(batteryBlocks);

                List<IMyTerminalBlock> cockpitsTarget =
                    new List<IMyTerminalBlock>();
                gridTerminal.GetBlocksOfType<Sandbox.ModAPI.IMyCockpit>(cockpitsTarget);

                List<IMyTerminalBlock> allBlocksTarget =
                    new List<IMyTerminalBlock>();
                gridTerminal.GetBlocksOfType<Sandbox.ModAPI.IMyTerminalBlock>(allBlocksTarget);

                List<IMyTerminalBlock> missileLuanchersTarget =
                    new List<IMyTerminalBlock>();
                gridTerminal.GetBlocksOfType<IMyMissileGunObject>(missileLuanchersTarget);

                List<IMyTerminalBlock> batteriesTarget =
                    new List<IMyTerminalBlock>();
                gridTerminal.GetBlocksOfType<Sandbox.ModAPI.IMyBatteryBlock>(batteriesTarget);


                List<IMySlimBlock> weaponsTarget = new List<IMySlimBlock>();

                grid.GetBlocks(weaponsTarget, (x) => x.FatBlock is Sandbox.ModAPI.IMyUserControllableGun);
                //now that we have a list of reactors and guns lets primary one.
                //try to find a working gun, if none are found then find a reactor to attack

                foreach (var weapon in weaponsTarget.OrderBy(x => (x.FatBlock.GetPosition() - Ship.GetPosition()).Length()))
                {
                    if (weapon != null)
                    {

                        if (weapon.FatBlock.IsFunctional)
                        {
                            _keyPoints.Add(weapon.FatBlock as IMyTerminalBlock);
                            var distFromCenter = (centerPosition - weapon.FatBlock.GetPosition()).Length();
                            if (distFromCenter > ShipSize)
                                ShipSize = distFromCenter;
                        }
                    }
                }
                foreach (var missile in missileLuanchersTarget.OrderBy(x => (x.GetPosition() - Ship.GetPosition()).Length()))
                {
                    if (missile != null)
                    {
                        if (missile.IsFunctional)
                        {
                            _keyPoints.Add(missile);
                            var distFromCenter = (centerPosition - missile.GetPosition()).Length();
                            if (distFromCenter > ShipSize)
                                ShipSize = distFromCenter;
                        }
                    }
                }
                foreach (var reactor in reactorBlocks.OrderBy(x => (x.GetPosition() - Ship.GetPosition()).Length()))
                {
                    if (reactor != null)
                    {
                        if (reactor.IsFunctional)
                        {
                            _keyPoints.Add(reactor);
                            var distFromCenter = (centerPosition - reactor.GetPosition()).Length();
                            if (distFromCenter > ShipSize)
                                ShipSize = distFromCenter;
                        }
                    }
                }
                foreach (var reactor in batteryBlocks.OrderBy(x => (x.GetPosition() - Ship.GetPosition()).Length()))
                {
                    if (reactor != null)
                    {
                        if (reactor.IsFunctional)
                        {
                            _keyPoints.Add(reactor);
                            var distFromCenter = (centerPosition - reactor.GetPosition()).Length();
                            if (distFromCenter > ShipSize)
                                ShipSize = distFromCenter;
                        }
                    }
                }
                //foreach (var reactor in solarBlocks.OrderBy(x => (x.GetPosition() - Ship.GetPosition()).Length()))
                //{
                //    if (reactor != null)
                //    {
                //        if (reactor.IsFunctional)
                //        {
                //            _keyPoints.Add(reactor);
                //            var distFromCenter = (centerPosition - reactor.GetPosition()).Length();
                //            if (distFromCenter > ShipSize)
                //                ShipSize = distFromCenter;
                //        }
                //    }
                //}
                foreach (var battery in batteriesTarget.OrderBy(x => (x.GetPosition() - Ship.GetPosition()).Length()))
                {
                    if (battery != null)
                    {
                        if (battery.IsFunctional)
                        {
                            _keyPoints.Add(battery);
                            var distFromCenter = (centerPosition - battery.GetPosition()).Length();
                            if (distFromCenter > ShipSize)
                                ShipSize = distFromCenter;
                        }
                    }
                }
                foreach (var cok in cockpitsTarget.OrderBy(x => (x.GetPosition() - Ship.GetPosition()).Length()))
                {
                    if (cok != null)
                    {
                        if (cok.IsFunctional)
                        {
                            _keyPoints.Add(cok);
                            var distFromCenter = (centerPosition - cok.GetPosition()).Length();
                            if (distFromCenter > ShipSize)
                                ShipSize = distFromCenter;
                        }
                    }
                }
            }
        }
    }
}
