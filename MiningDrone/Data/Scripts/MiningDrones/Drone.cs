using System;
using System.Collections.Generic;
using System.Linq;
using DroneConquest;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Interfaces;
using VRage.ModAPI;
using VRageMath;

using IMyControllableEntity = VRage.Game.ModAPI.Interfaces.IMyControllableEntity;
using IMyGyro = Sandbox.ModAPI.IMyGyro;
using IMyReactor = Sandbox.ModAPI.Ingame.IMyReactor;
using IMyTerminalBlock = Sandbox.ModAPI.Ingame.IMyTerminalBlock;
using IMyThrust = Sandbox.ModAPI.IMyThrust;

namespace MiningDrones
{
    public class Drone
    {
        private static string logPath = "Drone.txt";
        internal int broadcastingType;
        #region Shipvariables
        public bool InSquad = false;
        internal ThrusterGyroControls navigation;
        internal TargetingControls tc;
        public IMyCubeGrid Ship;
        internal IMyControllableEntity ShipControls;
        internal long _ownerId;

        internal double HealthBlockBase = 0;
        internal string _healthPercent = 100 + "%";
        internal static ITerminalAction _fireGun;
        internal static ITerminalAction _fireRocket;
        internal static ITerminalAction _blockOn;
        internal static ITerminalAction _blockOff;

        private DroneWeaponActions _currentWeaponAction = DroneWeaponActions.Standby;
        private DroneNavigationActions _currentNavigationAction = DroneNavigationActions.Stationary;

        internal string _beaconName = "CombatDrone";

        internal List<IMyTerminalBlock> beacons = new List<IMyTerminalBlock>();
        internal List<IMyTerminalBlock> antennas = new List<IMyTerminalBlock>();

        //Weapon Controls
        internal bool _isFiringManually;
        internal List<IMySlimBlock> _allWeapons = new List<IMySlimBlock>();
        internal List<IMySlimBlock> _allReactors = new List<IMySlimBlock>();
        internal List<IMySlimBlock> _manualGuns = new List<IMySlimBlock>();
        internal List<IMySlimBlock> _manualRockets = new List<IMySlimBlock>();


        internal double _maxAttackRange = 1000;
        private long _bulletSpeed = 200; //m/s
        private long _defaultOrbitRange = 700; //m/s
        private long _maxFiringRange = 700;
        private int _radiusOrbitmultiplier = 12;
        private int _saftyOrbitmultiplier = 8;

        internal DateTime _createdAt = DateTime.Now;
        internal int _minTargetSize = 10;
        public Sandbox.ModAPI.IMyGridTerminalSystem GridTerminalSystem;

        int missileStaggeredFireIndex = 0;
        DateTime _lastRocketFired = DateTime.Now;
        #endregion

        private static int numDrones = 0;
        internal int myNumber;
        public Type Type = typeof(Drone);

        public long GetOwnerId()
        {
            return _ownerId;
        }

        Random _r = new Random();
        public Drone(IMyEntity ent)
        {

            var ship = (IMyCubeGrid)ent;

            Ship = ship;
            var lstSlimBlock = new List<IMySlimBlock>();

            GridTerminalSystem = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(ship);

            //If it has any type of cockipt
            ship.GetBlocks(lstSlimBlock, (x) => x.FatBlock is Sandbox.ModAPI.IMyRemoteControl);
            FindWeapons();
            SetupActions();

            //If no cockpit the ship is either no ship or is broken.
            if (lstSlimBlock.Count != 0)
            {
                //Make the controls be the cockpit
                ShipControls = lstSlimBlock[0].FatBlock as IMyControllableEntity;

                #region Activate Beacons && Antennas


                //Maximise radius on antennas and beacons.
                lstSlimBlock.Clear();
                ship.GetBlocks(lstSlimBlock, (x) => x.FatBlock is Sandbox.ModAPI.IMyRadioAntenna);
                foreach (var block in lstSlimBlock)
                {
                    Sandbox.ModAPI.IMyRadioAntenna antenna =
                        (Sandbox.ModAPI.IMyRadioAntenna)block.FatBlock;
                    if (antenna != null)
                    {
                        //antenna.GetActionWithName("SetCustomName").Apply(antenna, new ListReader<TerminalActionParameter>(new List<TerminalActionParameter>() { TerminalActionParameter.Get("Combat Drone " + _manualGats.Count) }));
                        antenna.SetValueFloat("Radius", 10000);//antenna.GetMaximum<float>("Radius"));
                        _blockOn.Apply(antenna);
                    }
                }

                lstSlimBlock = new List<IMySlimBlock>();
                ship.GetBlocks(lstSlimBlock, (x) => x.FatBlock is Sandbox.ModAPI.IMyBeacon);
                foreach (var block in lstSlimBlock)
                {
                    Sandbox.ModAPI.IMyBeacon beacon = (Sandbox.ModAPI.IMyBeacon)block.FatBlock;
                    if (beacon != null)
                    {
                        beacon.SetValueFloat("Radius", 10000);//beacon.GetMaximum<float>("Radius"));
                        _blockOn.Apply(beacon);
                    }
                }

                #endregion

                //SetWeaponPower(true);
                //AmmoManager.ReloadReactors(_allReactors);
                //AmmoManager.ReloadGuns(_manualGats);
                ship.GetBlocks(lstSlimBlock, x => x is IMyEntity);


                List<IMyTerminalBlock> allTerminalBlocks = new List<IMyTerminalBlock>();
                GridTerminalSystem.GetBlocksOfType<IMyCubeBlock>(allTerminalBlocks);
                HealthBlockBase = allTerminalBlocks.Count;


                if (ShipControls != null)
                {
                    navigation = new ThrusterGyroControls(ship, ShipControls);
                    _ownerId = ((Sandbox.ModAPI.IMyTerminalBlock)ShipControls).OwnerId;
                    tc = new TargetingControls(Ship, _ownerId);
                }

            }

            Ship.OnBlockAdded += RecalcMaxHp;
            myNumber = numDrones;
            numDrones++;
        }

        protected void Hover()
        {
            navigation.Hover();
        }
        //this percent is based on IMyTerminalBlocks so it does not take into account the status of armor blocks
        //any blocks not functional decrease the overall %
        //having less blocks than when the drone was built will also result in less hp (parts destoried)
        private void CalculateDamagePercent()
        {
            try
            {
                List<IMyTerminalBlock> allTerminalBlocks =
                    new List<IMyTerminalBlock>();


                GridTerminalSystem.GetBlocksOfType<IMyCubeBlock>(allTerminalBlocks);


                double runningPercent = 0;
                foreach (var block in allTerminalBlocks)
                {
                    runningPercent += block.IsWorking || block.IsFunctional ? 100d : 0d;
                }
                runningPercent = runningPercent / allTerminalBlocks.Count;

                _healthPercent = ((int)((allTerminalBlocks.Count / HealthBlockBase) * (runningPercent)) + "%");//*(runningPercent);
            }
            catch (Exception e)
            {
                //this is to catch the exception where the block blows up mid read bexcause its under attack or whatever
            }
        }

        private void RecalcMaxHp(IMySlimBlock obj)
        {
            List<IMyTerminalBlock> allTerminalBlocks =
                    new List<IMyTerminalBlock>();

            GridTerminalSystem.GetBlocksOfType<IMyCubeBlock>(allTerminalBlocks);

            double count = 0;
            foreach (var block in allTerminalBlocks)
            {
                count += block.IsWorking || block.IsFunctional ? 100d : 0d;
            }

            HealthBlockBase = allTerminalBlocks.Count;//*(runningPercent);
        }

        //add objects to this ships local known objects collection (within detection range - 2km by defualt)


        //Turn weapons on and off SetWeaponPower(true) turns weapons online: vice versa
        public void SetWeaponPower(bool isOn)
        {
            foreach (var w in _allWeapons)
            {
                if (isOn)
                    _blockOn.Apply(w.FatBlock);
                else
                    _blockOff.Apply(w.FatBlock);
            }
        }

        /*
         * let me explain this stupid method....... nope, not much to explain because this is what I Had to do to get it to work.
         */
        private void FindWeapons()
        {
            if (Ship == null)
                return;


            _allWeapons.Clear();
            _allReactors.Clear();
            _manualGuns.Clear();
            _manualRockets.Clear();


            Ship.GetBlocks(_manualRockets, (x) => x.FatBlock != null && (x.FatBlock is Sandbox.ModAPI.IMySmallMissileLauncher));
            Ship.GetBlocks(_manualGuns, (x) => x.FatBlock != null && (x.FatBlock is Sandbox.ModAPI.IMySmallGatlingGun));

            Ship.GetBlocks(_allReactors, (x) => x.FatBlock != null && x.FatBlock is IMyReactor);
            Ship.GetBlocks(_allWeapons, (x) => x.FatBlock != null && (x.FatBlock is Sandbox.ModAPI.IMyUserControllableGun));
        }

        private void SetupActions()
        {
            if (_fireGun == null && _allWeapons.Count > 0 && _allWeapons[0] != null)
            {
                var actions = new List<ITerminalAction>();

                ((Sandbox.ModAPI.IMyUserControllableGun)_allWeapons[0].FatBlock).GetActions(actions);
                if (_fireRocket == null)
                {
                    foreach (var act in actions)
                    {
                        Util.GetInstance().Log("[Drone.IsAlive] Action Name " + act.Name.Replace(" ", "_"), "weapons.txt");
                        switch (act.Name.ToString())
                        {
                            case "Shoot_once":
                                _fireRocket = act;
                                break;
                            case "Shoot_On":
                                _fireGun = act;
                                break;
                            case "Toggle_block_Off":
                                _blockOff = act;
                                break;
                            case "Toggle_block_On":
                                _blockOn = act;
                                break;
                        }
                    }

                    Util.GetInstance()
                        .Log(
                            "[Drone.IsAlive] Has Missile attack -> " + (_fireRocket != null) + " Has Gun Attack " +
                            (_fireRocket != null) + " off " + (_blockOff != null) + " on " + (_blockOn != null),
                            "weapons.txt");
                }
            }
        }
        //All three must be true
        //Ship is not trash
        //Ship Controlls are functional
        //Weapons Exist on ship
        //There have been a few added restrictions that must be true for a ship[ to be alive
        public bool IsAlive()
        {
            string errors = "";
            
            bool shipWorking = true;
            try
            {
                if (ShipControls != null &&
                    (!((IMyCubeBlock)ShipControls).IsWorking))// ||
                //!((Sandbox.ModAPI.Ingame.IMyCubeBlock) ShipControls).IsWorking))
                {
                    errors += "Ship Controlls are down: ";
                    shipWorking = false;
                }
                if (ShipControls == null)
                {
                    errors += "Ship Controlls are down: ";
                    shipWorking = false;
                }


                List<IMyTerminalBlock> allBlocks = new List<IMyTerminalBlock>();
                GridTerminalSystem.GetBlocks(allBlocks);
                if (Ship != null && allBlocks.Count < 20)
                {
                    errors += "Ship Too Small: ";
                    shipWorking = false;
                }

                if (Ship != null && Ship.Physics.Mass < 1000)
                {
                    errors += "Ship Too Small: ";
                    shipWorking = false;
                }

                if (Ship != null && Ship.IsTrash())
                {
                    errors += "The ship is trashed: ";
                    shipWorking = false;
                }
                if (Ship == null)
                {
                    errors += "The ship is trashed: ";
                    shipWorking = false;
                }

                if (Ship != null && !Ship.InScene)
                {
                    errors += "The ship is trashed: ";
                    shipWorking = false;
                }

                if (!shipWorking && navigation != null)
                {

                    navigation.StopSpin();
                    ManualFire(false);
                    _beaconName = "Disabled Drone: " + errors;
                    NameBeacon();
                }
                if (!shipWorking)
                    Util.GetInstance().Log("[Drone.IsAlive] A Drone Has Died -> ", "droneDeaths.txt");
            }

            catch
            {
                shipWorking = false;
            }
            return shipWorking;
        }

        //Disables all beacons and antennas and deletes the ship.
        public void DeleteShip()
        {
            var lstSlimBlock = new List<IMySlimBlock>();
            Ship.GetBlocks(lstSlimBlock, (x) => x.FatBlock is Sandbox.ModAPI.IMyRadioAntenna);
            foreach (var block in lstSlimBlock)
            {
                Sandbox.ModAPI.IMyRadioAntenna antenna = (Sandbox.ModAPI.IMyRadioAntenna)block.FatBlock;
                ITerminalAction act = antenna.GetActionWithName("OnOff_Off");
                act.Apply(antenna);
            }

            lstSlimBlock = new List<IMySlimBlock>();
            Ship.GetBlocks(lstSlimBlock, (x) => x.FatBlock is Sandbox.ModAPI.IMyBeacon);
            foreach (var block in lstSlimBlock)
            {
                Sandbox.ModAPI.IMyBeacon beacon = (Sandbox.ModAPI.IMyBeacon)block.FatBlock;
                ITerminalAction act = beacon.GetActionWithName("OnOff_Off");
                act.Apply(beacon);
            }

            MyAPIGateway.Entities.RemoveEntity(Ship as IMyEntity);
            Ship = null;
        }

        private void TurnOnShip()
        {
            List<IMyTerminalBlock> thrusters = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyThrust>(thrusters);

            foreach (var thruster in thrusters)
            {
                thruster.GetActionWithName("OnOff_On").Apply(thruster);
            }

            List<IMyTerminalBlock> gyro = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyGyro>(gyro);

            foreach (var g in gyro)
            {
                g.GetActionWithName("OnOff_On").Apply(g);
            }
        }

        //ship location
        public Vector3D GetPosition()
        {
            return Ship.GetPosition();
        }

        //Changes grid ownership of the drone
        public void SetOwner(long id)
        {
            _ownerId = id;
            Ship.ChangeGridOwnership(id, MyOwnershipShareModeEnum.Faction);
            Ship.UpdateOwnership(id, true);
        }

        //usses ammo manager to Reload the inventories of the reactors and guns (does not use cargo blcks)
        public void ReloadWeaponsAndReactors()
        {
            FindWeapons();
            Util.GetInstance().Log("Number of weapons reloading", logPath);
            ItemManager im = new ItemManager();
            im.Reload(_allWeapons);
            im.ReloadReactors(_allReactors);
        }

       //turn on all weapons
        public void ManualFire(bool doFire)
        {
            FindWeapons();
            SetWeaponPower(doFire);
            if (doFire)
            {
                Util.GetInstance()
                    .Log(_fireGun + "[Drone.ManualFire] Number of guns -> " + _manualGuns.Count, "weapons.txt");
                Util.GetInstance()
                    .Log(_fireGun + "[Drone.ManualFire] number of all weapons -> " + _allWeapons.Count, "weapons.txt");
                foreach (var gun in _manualGuns)
                {

                    gun.ApplyAccumulatedDamage();
                    _fireGun.Apply(gun.FatBlock);
                }

                if (Math.Abs((DateTime.Now - _lastRocketFired).TotalMilliseconds) > 500 && _fireRocket != null &&
                    _manualRockets.Count > 0)
                {
                    var launcher = _manualRockets[missileStaggeredFireIndex];
                    _fireGun.Apply(launcher.FatBlock);
                    if (missileStaggeredFireIndex + 1 < _manualRockets.Count())
                    {
                        missileStaggeredFireIndex++;
                    }
                    else
                        missileStaggeredFireIndex = 0;
                    _lastRocketFired = DateTime.Now;
                }
            }

            _isFiringManually = doFire;

        }
        
        public void DisableThrusterGyroOverrides()
        {
            navigation.DisableThrusterGyroOverrides();

        }

        public bool ControlledByPlayer()
        {

            return navigation.PlayerHasControl();
        }

        private int _maxAvoidanceSpeed = 30;
        //Working - and damn good I might add
        //returns status means -1 = not activated, 0 = notEngaged, 1 = InCombat
        public int Guard(Vector3D position)
        {
            if (_bulletSpeed < 400)
                _bulletSpeed += 100;
            else
                _bulletSpeed = 100;

            var targetVector = Vector3D.Zero;
            var target = Vector3D.Zero;


            ManualFire(false);
            float enemyShipRadius = _defaultOrbitRange;
            var enemyTarget = tc.FindEnemyTarget();
            var avoidanceVector = navigation.GetWeightedCollisionAvoidanceVectorForNearbyStructures();

            if (enemyTarget != null)
            {
                target = enemyTarget.GetPosition();

                var keyPoint = tc.GetTargetKeyAttackPoint(enemyTarget);
                enemyShipRadius = tc.GetTargetSize(enemyTarget);
                if (keyPoint != null)
                {
                    target = keyPoint.GetPosition();
                    targetVector = enemyTarget.Physics.LinearVelocity;
                }
            }
            else if (tc.GetEnemyPlayer() != null)
            {
                target = tc.GetEnemyPlayer().GetPosition();
            }

            if (target != Vector3D.Zero)
            {

                var distance = (position - Ship.GetPosition()).Length();
                var distanceFromTarget = (target - Ship.GetPosition()).Length();

                double distanceVect = (target - Ship.GetPosition()).Length() / _bulletSpeed;
                Vector3D compAmount = targetVector - Ship.Physics.LinearVelocity;
                Vector3D compVector = new Vector3D(compAmount.X * distanceVect, compAmount.Y * distanceVect, compAmount.Z * distanceVect);
                
                if (distance > _maxAttackRange)
                {
                    _currentNavigationAction = DroneNavigationActions.Approaching;
                    Approach(position);
                }
                else
                {
                    _currentNavigationAction = DroneNavigationActions.Orbiting;

                    
                    if (avoidanceVector != Vector3D.Zero)
                    {
                        if (Ship.Physics.LinearVelocity.Normalize() > _maxAvoidanceSpeed)
                        {
                            navigation.SlowDown();
                        }
                        else
                        {
                            navigation.WeightedThrustTwordsDirection(avoidanceVector);
                        }
                        _currentNavigationAction = DroneNavigationActions.Avoiding;
                    }
                    else
                        AimFreeOrbit(target, enemyShipRadius * _radiusOrbitmultiplier);
                    //KeepAtCombatRange(target, targetVector);
                    double alignment = AlignTo(target + compVector);


                    if (alignment < 1)
                    {
                        _currentWeaponAction = DroneWeaponActions.Attacking;
                        ManualFire(true);
                    }
                    else
                    {
                        _currentWeaponAction = DroneWeaponActions.LockedOn;
                        ManualFire(false);
                    }
                }
            }
            else if (avoidanceVector != Vector3D.Zero)
            {
                if (Ship.Physics.LinearVelocity.Normalize() > _maxAvoidanceSpeed)
                {
                    navigation.SlowDown();
                }
                else
                {
                    AlignTo(Ship.GetPosition() + avoidanceVector);
                    navigation.WeightedThrustTwordsDirection(avoidanceVector);
                }
                _currentNavigationAction = DroneNavigationActions.Avoiding;
                _currentWeaponAction = DroneWeaponActions.Standby;
            }
            else
            {
                _currentWeaponAction = DroneWeaponActions.Standby;
                _currentNavigationAction = DroneNavigationActions.Orbiting;
                Orbit(position);
                //ManualFire(false);
            }

            return 0;
        }

        //this sets the status of the ship in its beacon name or antenna name - this is user settable within in drone name
        //if drone name includes :antenna then the drone will display information on the antenna rather than the beacon
        public void NameBeacon()
        {
            try
            {

                if (broadcastingType == 1)
                {
                    if (Util.GetInstance().DebuggingOn)
                    {
                        FindBeacons();
                        if (beacons != null && beacons.Count > 0)
                        {
                            CalculateDamagePercent();
                            var beacon = beacons[0] as Sandbox.ModAPI.IMyBeacon;
                            beacon.SetCustomName(_beaconName +
                                                 " HP: " + _healthPercent +
                                                 " MS: " + (int)Ship.Physics.LinearVelocity.Normalize());
                        }
                    }
                    else
                    {
                        FindBeacons();
                        if (beacons != null && beacons.Count > 0)
                        {
                            CalculateDamagePercent();
                            var beacon = beacons[0] as Sandbox.ModAPI.IMyBeacon;
                            beacon.SetCustomName("HP: " + _healthPercent);
                        }
                    }
                }
                else
                    NameAntenna();
            }
            catch
            {
            }
        }

        public void NameAntenna()
        {
            if (Util.GetInstance().DebuggingOn)
            {
                FindAntennas();
                if (antennas != null && antennas.Count > 0)
                {
                    CalculateDamagePercent();
                    var antenna = antennas[0] as Sandbox.ModAPI.IMyRadioAntenna;
                    antenna.SetCustomName(_beaconName +
                                          " HP: " + _healthPercent +
                                          " MS: " + (int)Ship.Physics.LinearVelocity.Normalize());
                }
            }
            else
            {
                FindAntennas();
                if (antennas != null && antennas.Count > 0)
                {
                    CalculateDamagePercent();
                    var antenna = antennas[0] as Sandbox.ModAPI.IMyRadioAntenna;
                    antenna.SetCustomName("HP: " + _healthPercent +"\n"+
                                          "MS: " + (int)Ship.Physics.LinearVelocity.Normalize() + "/" + navigation.MaxSpeed+"\n"+
                                           _currentNavigationAction + "/" + _currentWeaponAction);
                }
            }
        }

        internal void SetBroadcasting(bool broadcastingEnabled)
        {
            FindBeacons();
            ITerminalAction power = broadcastingEnabled ? _blockOn : _blockOff;
            foreach (var v in beacons)
            {
                power.Apply(v);
            }
            FindAntennas();
            foreach (var v in antennas)
            {
                power.Apply(v);
            }
            
        }
        public void FindBeacons()
        {
            GridTerminalSystem.GetBlocksOfType<Sandbox.ModAPI.IMyBeacon>(beacons);
        }

        public void FindAntennas()
        {
            GridTerminalSystem.GetBlocksOfType<Sandbox.ModAPI.IMyRadioAntenna>(antennas);
        }


        // for thoes pesky drones that just dont care about the safty of others
        public void Detonate()
        {
            ShipControls.MoveAndRotateStopped();
            List<IMyTerminalBlock> warHeads = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<Sandbox.ModAPI.IMyWarhead>(warHeads);

            foreach (var warhead in warHeads)
                warhead.GetActionWithName("StartCountdown").Apply(warhead);
        }




        public double AlignTo(Vector3D target)
        {
            return navigation.AlignTo(target);
        }

        public void FullThrust(Vector3D target)
        {
            var distance = (Ship.GetPosition() - target).Length();

            var maxSpeed = distance > 1000 ? 150 : distance / 10 > 20 ? distance / 10 : 20;

            navigation.ThrustTwordsDirection(Ship.GetPosition() - target);
        }

        public void Approach(Vector3D target)
        {
            
            var distance = (Ship.GetPosition() - target).Length();

            var maxSpeed = distance > 1000 ? 150 : distance/10 > 20? distance/10 : 20;
            AlignTo(target);

            var avoidanceVector = navigation.AvoidNearbyGrids();
            if (avoidanceVector != Vector3D.Zero)
            {
                navigation.ThrustTwordsDirection(avoidanceVector, false, true);
            }
            else if (Ship.Physics.LinearVelocity.Normalize() > maxSpeed)
            {
                navigation.SlowDown();
            }
            else if (distance > 50)
            {
                navigation.ThrustTwordsDirection(Ship.GetPosition() - target);
            }
            //else if (distance < 50)
            //{
            //    navigation.ThrustTwordsDirection(target - Ship.GetPosition());
            //}

        }

        private void KeepAtCombatRange(Vector3D target, Vector3D velocity)
        {
            var distance = (Ship.GetPosition() - target).Length();

            if (Ship.Physics.LinearVelocity.Normalize() > velocity.Normalize()*1.2)
            {
                navigation.SlowDown();
            }
            else if (distance > 700)
            {
                navigation.ThrustTwordsDirection(Ship.GetPosition() - target);
            }
            else if (distance < 500)
            {
                navigation.ThrustTwordsDirection(target - Ship.GetPosition());
            }
            else
            {
                navigation.EvasiveManeuvering(velocity);
            }
        }

        public void Orbit(Vector3D lastTargetPosition)
        {
            
            navigation.Orbit(lastTargetPosition);
            // navigation.CombatOrbit(lastTargetPosition);

        }

        public void AimFreeOrbit(Vector3D lastTargetPosition, float range = 700)
        {
            navigation.AimFreeOrbit(lastTargetPosition, range);
        }

        public void Stop()
        {
            //navigation.CompleteStop();
        }
    }
}
