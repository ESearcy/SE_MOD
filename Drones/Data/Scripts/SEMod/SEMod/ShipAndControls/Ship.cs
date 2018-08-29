using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;
using Sandbox.Game.Entities.Cube;

namespace SEMod
{
    class Ship
    {
          
        private static string _logPath = "Ship";
        internal WeaponControls weaponControls;
        public IMyCubeGrid _cubeGrid;
        internal VRage.Game.ModAPI.Interfaces.IMyControllableEntity ShipControls;
        internal long _ownerId;
        internal NavigationControls navigation;
        public static double DETECTIONRANGE = 2000;
        public Sandbox.ModAPI.IMyGridTerminalSystem GridTerminalSystem;
        Random _r = new Random();
        
        private double HealthBlockBase = 0;
        private int functionalBlockCount = 0;
        private int healthPercent = 0;
        private TimeSpan rescanDelayTimespan;
        private DroneWeaponActions _currentWeaponAction = DroneWeaponActions.Standby;
        private DroneNavigationActions _currentNavigationAction = DroneNavigationActions.Stationary;
        private List<IMyCubeBlock> AllBlocks;
        private String fleetname;
        private bool needsOxygen = false;
        private Ship commandShip = null;
        public Vector3D defaultOrbitPoint = new Vector3D(0,0,0);

        public long GetOwnerId()
        {
            return _ownerId;
        }

        public Ship(IMyCubeGrid ent, long id, String fleetName)
        {
            this.fleetname = fleetName;
            _cubeGrid = ent;
            GridTerminalSystem = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(_cubeGrid);

            SetOwner(id);
            List<Sandbox.ModAPI.IMyTerminalBlock> remoteControls = new List<Sandbox.ModAPI.IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyRemoteControl>(remoteControls);
            GridTerminalSystem.GetBlocksOfType<IMyCubeBlock>(AllBlocks);
            ShipControls = remoteControls.FirstOrDefault() != null ? remoteControls.First() as Sandbox.Game.Entities.IMyControllableEntity : null;
            _ownerId = id;

            if (ShipControls == null)
                return;

            DetectReactors();
            LocateShipComponets();
            ResetHealthMax();
            SetupRescanDelay();
            FindAntennasAndBeacons();
            ConfigureAntennas();
        }


        private int one = 1;
        private int rescanDelay = 10;
        private void SetupRescanDelay()
        {
            DateTime start = new DateTime(one, one, one, one, one, one);
            DateTime end = new DateTime(one, one, one, one, one, one * rescanDelay);
            rescanDelayTimespan = (end - start);
        }

        public void SetCommandShip(Ship s)
        {
            commandShip = s;
        }

        List<IMyTerminalBlock> reactors = new List<IMyTerminalBlock>();

        private void ResetHealthMax()
        {
            GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(allTerminalBlocks);
            _cubeGrid.OnBlockAdded += _cubeGrid_OnBlockAdded;
            HealthBlockBase = allTerminalBlocks.Count;
        }

        private void _cubeGrid_OnBlockAdded(IMySlimBlock obj)
        {
            ResetHealthMax();
        }

        List<IMyTerminalBlock> allTerminalBlocks = new List<IMyTerminalBlock>();
        private void UpdateHealthPercent()
        {
            allTerminalBlocks = allTerminalBlocks.Where(x => x != null).ToList();
            functionalBlockCount = allTerminalBlocks.Count(x=> x.IsFunctional);
            healthPercent = (int)((functionalBlockCount/HealthBlockBase)*100);
        }

        private void DetectReactors()
        {
            reactors.Clear();
            GridTerminalSystem.GetBlocksOfType<IMyReactor>(reactors);
        }

        private void LocateShipComponets()
        {
            
            navigation = new NavigationControls(_cubeGrid, ShipControls);
            weaponControls = new WeaponControls(_cubeGrid, _ownerId, GridTerminalSystem, fleetname);
        }

        private void ConfigureAntennas()
        {

            foreach (var block in antennas)
            {
                if (block != null)
                {
                    //antenna.GetActionWithName("SetCustomName").Apply(antenna, new ListReader<TerminalActionParameter>(new List<TerminalActionParameter>() { TerminalActionParameter.Get("Combat Drone " + _manualGats.Count) }));
                    block.SetValueFloat("Radius", 2000);//antenna.GetMaximum<float>("Radius"));
                }
            }

            //lstSlimBlock = new List<IMySlimBlock>();
            //_cubeGrid.GetBlocks(lstSlimBlock, (x) => x.FatBlock is Sandbox.ModAPI.IMyBeacon);
            //foreach (var block in lstSlimBlock)
            //{
            //    Sandbox.ModAPI.IMyBeacon beacon = (Sandbox.ModAPI.IMyBeacon)block.FatBlock;
            //    if (beacon != null)
            //    {
            //        beacon.SetValueFloat("Radius", 10000);//beacon.GetMaximum<float>("Radius"));
            //        _blockOn.Apply(beacon);
            //    }
            //}
        }

        //this percent is based on IMyTerminalBlocks so it does not take into account the status of armor blocks
        //any blocks not functional decrease the overall %
        //having less blocks than when the drone was built will also result in less hp (parts destoried)


        //All three must be true
        //_cubeGrid is not trash
        //_cubeGrid Controlls are functional
        //Weapons Exist on ship
        //There have been a few added restrictions that must be true for a ship[ to be alive
        public bool IsOperational()
        {
            bool isAlive = false;
            try
            {
                if (_cubeGrid == null)
                    return false;

                bool shipAndControlAlive = _cubeGrid != null && //ship
                                           ShipControls != null && (ShipControls as IMyTerminalBlock).IsFunctional;
                //shipcontrols
                
                var numberantennas = antennas.Count(x=>x.IsWorking) + beacons.Count(x => x.IsWorking);
                isAlive = navigation.IsOperational() && weaponControls.IsOperational() && shipAndControlAlive && (numberantennas>0);
                Logger.Debug("Is Alive: " + isAlive);
            }
            catch (Exception e)
            {
               isAlive = false;
                Logger.LogException(e);
            }
            return isAlive;
        }

        //Disables all beacons and antennas and deletes the ship.
        public void DeleteShip()
        {
            var lstSlimBlock = new List<IMySlimBlock>();
            _cubeGrid.GetBlocks(lstSlimBlock, (x) => x.FatBlock is Sandbox.ModAPI.IMyRadioAntenna);
            foreach (var block in lstSlimBlock)
            {
                Sandbox.ModAPI.IMyRadioAntenna antenna = (Sandbox.ModAPI.IMyRadioAntenna)block.FatBlock;
                ITerminalAction act = antenna.GetActionWithName("OnOff_Off");
                act.Apply(antenna);
            }

            lstSlimBlock = new List<IMySlimBlock>();
            _cubeGrid.GetBlocks(lstSlimBlock, (x) => x.FatBlock is Sandbox.ModAPI.IMyBeacon);
            foreach (var block in lstSlimBlock)
            {
                Sandbox.ModAPI.IMyBeacon beacon = (Sandbox.ModAPI.IMyBeacon)block.FatBlock;
                ITerminalAction act = beacon.GetActionWithName("OnOff_Off");
                act.Apply(beacon);
            }

            _cubeGrid.SyncObject.SendCloseRequest();
            MyAPIGateway.Entities.RemoveEntity(_cubeGrid as IMyEntity);

            //_cubeGrid = null;
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
            return _cubeGrid.GetPosition();
        }

        //Changes grid ownership of the drone
        public void SetOwner(long id)
        {
            _ownerId = id;
            _cubeGrid.ChangeGridOwnership(id,MyOwnershipShareModeEnum.Faction);
            _cubeGrid.UpdateOwnership(id, true);
        }

        //usses ammo manager to Reload the inventories of the reactors and guns (does not use cargo blcks)
        public void ReloadWeaponsAndReactors()
        {
            Logger.Debug("Number of weapons reloading");
            ItemManager im = new ItemManager();
            List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
            List<IMyTerminalBlock> blocks2 = new List<IMyTerminalBlock>();
            List<IMyTerminalBlock> reactors = new List<IMyTerminalBlock>();
            List<IMyTerminalBlock> gastanks = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyGasGenerator>(gastanks);
            GridTerminalSystem.GetBlocksOfType<IMyUserControllableGun>(blocks);
            GridTerminalSystem.GetBlocksOfType<IMySmallGatlingGun>(blocks2);
            GridTerminalSystem.GetBlocksOfType<IMyReactor>(reactors);

            im.ReloadHydrogenTanks(gastanks);
            im.Reload(blocks);
            im.Reload(blocks2);
            im.ReloadReactors(reactors);
        }

        public void DisableThrusterGyroOverrides()
        {
            navigation.DisableThrusterGyroOverrides();
        }

        public bool ControlledByPlayer()
        {
            return navigation.PlayerHasControl();
        }

        DateTime lastScan = DateTime.Now;
        private int _rescanRate = 3;//seconds
        private int numRescans = 0;
        public void ScanLocalArea()
        {
            if (_cubeGrid == null || weaponControls==null)
                return;
            try
            {
                if (numRescans > 10)
                {
                    weaponControls.ClearTargets();
                    numRescans = 0;
                }
                numRescans++;

                lastScan = DateTime.Now;
                var bs = new BoundingSphereD(_cubeGrid.GetPosition(), DETECTIONRANGE);
                var nearbyEntities = MyAPIGateway.Entities.GetEntitiesInSphere(ref bs);

                //var closeAsteroids = asteroids.Where(z => (z.GetPosition() - drone.GetPosition()).Length() < MaxEngagementRange).ToList();

                weaponControls.ClearNearbyObjects();
                foreach (var closeItem in nearbyEntities)
                {
                    if (closeItem is IMyCubeGrid && !closeItem.Transparent && closeItem.Physics!=null && closeItem.Physics.Mass > 5000)
                        weaponControls.AddNearbyFloatingItem(closeItem as IMyCubeGrid);
                    if (closeItem is IMyVoxelBase)
                        weaponControls.AddNearbyAsteroid((IMyVoxelBase)closeItem);
                }
            }
            catch (Exception e)
            {
                Logger.LogException(e);
            }
        }

        internal void ReportDiagnostics()
        {
            Logger.Debug("Report Diagnostics.");
            FindAntennasAndBeacons();
            Broadcast();
            //    SetBroadcasting(true);
        }

        private String _beaconName = "";
        public void Broadcast()
        {

            Logger.Debug(_beaconName +
                                              "\nHP: " + healthPercent +
                                              " \nMS: " + (int)_cubeGrid.Physics.LinearVelocity.Normalize() +
                                              " \nNA: " + _currentNavigationAction +
                                              " \nWA: " + _currentWeaponAction +
                                              " \nWC: " + weaponControls.GetWeaponsCount() +
                                              " \nGC: " + navigation.GetWorkingGyroCount() +
                                              " \nTC: " + navigation.GetWorkingThrusterCount());
            try
            {
                if (Logger.DebugEnabled)
                {
                    if (beacons != null && beacons.Count > 0)
                    {
                        var antenna = beacons[0] as Sandbox.ModAPI.IMyBeacon;
                        if(antenna!=null)
                            antenna.CustomName = (_beaconName +
                                              "\nHP: " + healthPercent +
                                              "\nMS: " + (int)_cubeGrid.Physics.LinearVelocity.Normalize() +
                                              "\nNA: " + _currentNavigationAction +
                                              "\nWA: " + _currentWeaponAction);

                    }
                    else
                    {
                        if (antennas != null && antennas.Count > 0)
                        {
                            var antenna = antennas[0] as Sandbox.ModAPI.IMyRadioAntenna;
                            if (antenna != null)
                                antenna.CustomName = (_beaconName +
                                                  "\nHP: " + healthPercent +
                                                  "\nMS: " + (int)_cubeGrid.Physics.LinearVelocity.Normalize() +
                                                  "\nNA: " + _currentNavigationAction +
                                                  "\nWA: " + _currentWeaponAction);

                        }
                    }
                }
                else
                {
                    if (beacons != null && beacons.Count > 0)
                    {
                        var antenna = beacons[0] as Sandbox.ModAPI.IMyBeacon;
                        if (antenna != null)
                            antenna.CustomName = (_beaconName +
                                                  "\nHP: " + healthPercent +
                                                  "\nMS: " + (int) _cubeGrid.Physics.LinearVelocity.Normalize());
                    }
                    else
                    {
                        if (antennas != null && antennas.Count > 0)
                        {
                            var antenna = antennas[0] as Sandbox.ModAPI.IMyRadioAntenna;
                            if (antenna != null)
                                antenna.CustomName = (_beaconName +
                                                  "\nHP: " + healthPercent +
                                                  "\nMS: " + (int)_cubeGrid.Physics.LinearVelocity.Normalize());
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogException(e);
            }
        }

        List<IMyTerminalBlock> antennas = new List<IMyTerminalBlock>();
        List<IMyTerminalBlock> beacons = new List<IMyTerminalBlock>();

        public void FindAntennasAndBeacons()
        {
            beacons = allTerminalBlocks.Where(x => x is IMyBeacon).ToList();
            antennas = allTerminalBlocks.Where(x => x is IMyRadioAntenna).ToList();
        }

        // for thoes pesky drones that just dont care about the safty of others
        private bool activated = false;
        public void Detonate()
        {
            ShipControls.MoveAndRotateStopped();
            List<IMyTerminalBlock> warHeads = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<Sandbox.ModAPI.IMyWarhead>(warHeads);

            foreach (var warhead  in warHeads)
            {
                warhead.SetValueFloat("DetonationTime", 1200);
                if (activated)
                {
                    warhead.GetActionWithName("StopCountdown").Apply(warhead);
                    activated = false;
                }
                else
                {
                    warhead.GetActionWithName("StartCountdown").Apply(warhead);
                    activated = false;
                }

            }
        }

        public double AlignTo(Vector3D target)
        {
            return navigation.AlignTo(target);
        }

        public void FullThrust(Vector3D target)
        {
            var distance = (_cubeGrid.GetPosition() - target).Length();

            var maxSpeed = distance > 1000 ? 150 : distance / 10 > 20 ? distance / 10 : 20;

            navigation.ThrustTwordsDirection(_cubeGrid.GetPosition() - target);
        }

        public void Approach(Vector3D target)
        {

            var distance = (_cubeGrid.GetPosition() - target).Length();

            var maxSpeed = distance > 1000 ? 150 : distance / 10 > 20 ? distance / 10 : 20;
            AlignTo(target);

            Vector3D avoidanceVector = navigation.AvoidNearbyGrids();
            if (avoidanceVector != Vector3D.Zero)
            {
                navigation.ThrustTwordsDirection(avoidanceVector, false, true);
            }
            else if (_cubeGrid.Physics.LinearVelocity.Normalize() > maxSpeed)
            {
                navigation.SlowDown();
            }
            else if (distance > 50)
            {
                navigation.ThrustTwordsDirection(_cubeGrid.GetPosition() - target);
            }
            //else if (distance < 50)
            //{
            //    navigation.ThrustTwordsDirection(target - _cubeGrid.GetPosition());
            //}

        }

        private void KeepAtCombatRange(Vector3D target, Vector3D velocity)
        {
            var distance = (_cubeGrid.GetPosition() - target).Length();

            if (_cubeGrid.Physics.LinearVelocity.Normalize() > velocity.Normalize() * 1.2)
            {
                navigation.SlowDown();
            }
            else if (distance > 500)
            {
                navigation.ThrustTwordsDirection(_cubeGrid.GetPosition() - target);
            }
            else if (distance < 300)
            {
                navigation.ThrustTwordsDirection(target - _cubeGrid.GetPosition());
            }
            else
            {
                navigation.EvasiveManeuvering(velocity);
            }
        }

        public void Orbit(Vector3D lastTargetPosition)
        {
            navigation.Orbit(lastTargetPosition);
        }

        public void FighterOrbit(Vector3D lastTargetPosition)
        {
            navigation.OrbitAtRange(lastTargetPosition, avoidanceRange*2);
        }

        public void AimFreeOrbit(Vector3D lastTargetPosition)
        {
            navigation.CombatOrbit(lastTargetPosition);
        }


        private int breakawayDistance = 75;
        private bool attacking = false;

        public void Update()
        {try
                {
            Logger.Debug("==== fighter Update:");
            UpdateHealthPercent();
            weaponControls.DisableWeapons();

            var isOperational = IsOperational();
            Logger.Debug("operational = "+isOperational);
            if (isOperational)
            {
                Logger.Debug("operational");
                var hasAvoidanceVectors = CalculateAvoidanceVectors();
                //var AnyTargetsInfront = CheckForHeadonCollision();
                avoidanceVector.Normalize();
                
                    var target = weaponControls.GetEnemyTarget();
                    

                    var targetLocked = target != null;
                    if (targetLocked)
                    {
                        Logger.Debug("Has Target " + target);

                            IMyTerminalBlock targetblock = null;
                            targetblock = weaponControls.GetTargetKeyAttackPoint(target.Ship);

                            Logger.Debug("Has TargetBlock " + targetblock);
                            var targetPoition = targetblock != null
                                ? targetblock.GetPosition()
                                : target.Ship.GetPosition();
                            Logger.Debug("Target Position " + targetPoition);

                            var awayDir = _cubeGrid.GetPosition() - target.Ship.GetPosition();
                            var dirTotarget = targetPoition - _cubeGrid.GetPosition();
                            var distance = dirTotarget.Length() - target.ShipSize;
                            dirTotarget.Normalize();
                            var avoidTargetVector = avoidanceVector*100;//*dirTotarget;
                            if (distance > breakawayDistance)
                            {
                                //Util.NotifyHud(hasAvoidanceVectors + " has vectors " + avoidanceVector);
                                if (hasAvoidanceVectors) { 
                                    navigation.ThrustTwordsDirection(avoidTargetVector);
                                    AlignTo(_cubeGrid.GetPosition() - avoidTargetVector);
                                    _currentNavigationAction = DroneNavigationActions.Avoiding;
                                    if (_cubeGrid.Physics.LinearVelocity.Normalize() > 40)
                                    {
                                        navigation.SlowDown();
                                    }
                                
                                }
                                else
                                {
                                    //if(distance > breakawayDistance*3)
                                        navigation.CombatApproach(targetPoition);
                                 //KeepAtCombatRange(targetPoition, target.Ship.Physics.LinearVelocity);
                                _currentWeaponAction = DroneWeaponActions.LockedOn;
                                _currentNavigationAction = DroneNavigationActions.AttackRun;

                                var falloff = target.Ship.Physics.LinearVelocity - _cubeGrid.Physics.LinearVelocity;
                                    var alignment = AlignTo(targetPoition + falloff);
                                    if (alignment <= 2)
                                    {
                                        if(distance < 900)
                                            weaponControls.EnableWeapons();

                                        if (alignment <= .5)
                                            navigation.StopSpin();
                                    }
                                }
                                breakawayDistance = 75;
                                attacking = true;
                            }
                            else
                            {
                                _currentNavigationAction = DroneNavigationActions.BreakAway;
                                navigation.ThrustTwordsDirection(avoidTargetVector);
                                AlignTo(_cubeGrid.GetPosition() - avoidTargetVector);
                                
                                breakawayDistance = 600;
                                attacking = false;
                            }

                    }
                    else
                    {
                        _currentWeaponAction = DroneWeaponActions.Standby;
                        if (!hasAvoidanceVectors)
                        {
                            Orbit(defaultOrbitPoint);
                            _currentNavigationAction = DroneNavigationActions.Orbiting;
                        }
                        else
                        {
                            navigation.ThrustTwordsDirection(avoidanceVector);
                            AlignTo(_cubeGrid.GetPosition() - avoidanceVector);
                            _currentNavigationAction = DroneNavigationActions.Avoiding;
                            breakawayDistance = 150;
                            attacking = false;
                        }

                    }
                Broadcast();
            }
            }
            catch (Exception e)
            {
                Logger.LogException(e);
            }

        }

        public void UpdateFighter()
        {
            try
            {
                bool followingCommandShip = commandShip.IsOperational();
                if (commandShip != null)
                {
                    if (!followingCommandShip)
                        commandShip = null;
                    else
                    {
                        defaultOrbitPoint = commandShip.GetPosition();
                    }
                }

                Logger.Debug("==== fighter Update:");
                UpdateHealthPercent();
                weaponControls.DisableWeapons();

                var isOperational = IsOperational();
                Logger.Debug("operational = " + isOperational);
                if (isOperational)
                {
                    Logger.Debug("operational");
                    var hasAvoidanceVectors = CalculateAvoidanceVectors();
                    //var AnyTargetsInfront = CheckForHeadonCollision();
                    avoidanceVector.Normalize();

                    var target = weaponControls.GetEnemyTarget();
                    
                    var targetLocked = target != null;
                    if (targetLocked)
                    {
                        Logger.Debug("Has Target " + target);

                        IMyTerminalBlock targetblock = null;
                        targetblock = weaponControls.GetTargetKeyAttackPoint(target.Ship);

                        Logger.Debug("Has TargetBlock " + targetblock);
                        var targetPoition = targetblock != null
                            ? targetblock.GetPosition()
                            : target.Ship.GetPosition();
                        Logger.Debug("Target Position " + targetPoition);

                        var awayDir = _cubeGrid.GetPosition() - target.Ship.GetPosition();
                        var dirTotarget = targetPoition - _cubeGrid.GetPosition();
                        var distance = dirTotarget.Length() - target.ShipSize;
                        dirTotarget.Normalize();
                        var avoidTargetVector = avoidanceVector * 100;//*dirTotarget;
                        if (distance > breakawayDistance)
                        {
                            //Util.NotifyHud(hasAvoidanceVectors + " has vectors " + avoidanceVector);
                            if (hasAvoidanceVectors)
                            {
                                navigation.ThrustTwordsDirection(avoidTargetVector);
                                AlignTo(_cubeGrid.GetPosition() - avoidTargetVector);
                                _currentNavigationAction = DroneNavigationActions.Avoiding;
                                
                            }
                            else
                            {
                                //if(distance > breakawayDistance*3)
                                navigation.CombatApproach(targetPoition);
                                _currentWeaponAction = DroneWeaponActions.LockedOn;
                                _currentNavigationAction = DroneNavigationActions.AttackRun;

                                var falloff = target.Ship.Physics.LinearVelocity - _cubeGrid.Physics.LinearVelocity;
                                var alignment = AlignTo(targetPoition + falloff);
                                if (alignment <= 1)
                                {
                                    if (distance < 900)
                                        weaponControls.EnableWeapons();

                                    if (alignment <= 1)
                                        navigation.StopSpin();
                                }
                            }
                            breakawayDistance = 75;
                            attacking = true;
                        }
                        else
                        {
                            _currentNavigationAction = DroneNavigationActions.BreakAway;
                            navigation.ThrustTwordsDirection(avoidTargetVector);
                            AlignTo(_cubeGrid.GetPosition() - avoidTargetVector);

                            breakawayDistance = 600;
                            attacking = false;
                        }

                    }
                    else
                    {
                        _currentWeaponAction = DroneWeaponActions.Standby;
                        if (!hasAvoidanceVectors)
                        {
                            FighterOrbit(defaultOrbitPoint);
                            _currentNavigationAction = DroneNavigationActions.Orbiting;
                        }
                        else
                        {
                            navigation.ThrustTwordsDirection(avoidanceVector);
                            AlignTo(_cubeGrid.GetPosition() - avoidanceVector);
                            _currentNavigationAction = DroneNavigationActions.Avoiding;
                            breakawayDistance = 150;
                            attacking = false;
                        }

                    }
                    Broadcast();
                    if (_cubeGrid.Physics.LinearVelocity.Normalize() > 60)
                    {
                        navigation.SlowDown();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogException(e);
            }
        }

        private void Follow(Vector3D vector3D)
        {
            var distance = (_cubeGrid.GetPosition() - vector3D).Length();

            var maxSpeed = distance > 1000 ? 150 : distance / 10 > 20 ? distance / 10 : 20;
            AlignTo(vector3D);

            if (_cubeGrid.Physics.LinearVelocity.Normalize() > maxSpeed)
            {
                navigation.SlowDown();
            }
            else if (distance > 500)
            {
                navigation.ThrustTwordsDirection(_cubeGrid.GetPosition() - vector3D);
            }
        }


        private bool CheckForHeadonCollision()
        {
            List<IMyCubeGrid> closiest = weaponControls.GetObjectsInRange(avoidanceRange);
            List<IMyVoxelBase> asteroids = weaponControls.GetAsteroids(avoidanceRange * 4);

            int count = 0;
            avoidanceVector = Vector3D.Zero;

            foreach (var asteroid in asteroids)
            {
                count++;
                Vector3D vector = (_cubeGrid.GetPosition() - asteroid.GetPosition());
                vector.Normalize();
                //ShowVectorOnHud(target.Ship.GetPosition(), vector);
                avoidanceVector = avoidanceVector + vector;
            }
            avoidanceVector = avoidanceVector / count;
            //ShowVectorOnHud(_cubeGrid.GetPosition(), avoidanceVector);
            ;

            return !double.IsNaN(avoidanceVector.Normalize());
        }

        private void ShowLocationOnHud(Vector3D position)
        {

            long id = MyAPIGateway.Session.Player.IdentityId;

            IMyGps mygps = MyAPIGateway.Session.GPS.Create("=", "", position, true, true);
            MyAPIGateway.Session.GPS.AddGps(id, mygps);
        }

        //private void ShowVectorOnHud(Vector3D position, Vector3D direction)
        //{
        //    var color = Color.Red.ToVector4();
        //    MySimpleObjectDraw.DrawLine(position, position + direction, "null or transparent material", ref color, .1f);
        //}

        private int collisionDistance = 200;
        private int avoidanceRange = 200;
        private Vector3D avoidanceVector = Vector3D.Zero;
        private bool avoidedLastTurn = false;
        private DateTime timeSinceLastAvoid = DateTime.Now;
        public DateTime lastUpdate = DateTime.Now.AddMinutes(-5);
        internal DateTime lastReload = DateTime.Now.AddMinutes(-5);

        private bool CalculateAvoidanceVectors()
        {
            if (avoidedLastTurn)
            {
                if ((DateTime.Now - timeSinceLastAvoid).TotalSeconds > 3)
                {
                    avoidedLastTurn = false;
                }
                else return true;
            }
            List<IMyCubeGrid> closiest = weaponControls.GetObjectsInRange(avoidanceRange);
            List<IMyVoxelBase> asteroids = weaponControls.GetAsteroids(avoidanceRange);

            int count = 0;
            var avoidanceVectorToBe = Vector3D.Zero;
            foreach (var target in closiest)
            {
                count++;
                Vector3D vector = (target.GetPosition() - _cubeGrid.GetPosition());
                var length = avoidanceRange - vector.Length();
                vector.Normalize();
                //ShowVectorOnHud(target.Ship.GetPosition(), vector);
                avoidanceVectorToBe = avoidanceVectorToBe + (vector*length);
            }

            foreach (var asteroid in asteroids)
            {
                count++;
                Vector3D vector = (asteroid.GetPosition() - _cubeGrid.GetPosition());
                var length = avoidanceRange - vector.Length();
                vector.Normalize();
                //ShowVectorOnHud(target.Ship.GetPosition(), vector);
                avoidanceVectorToBe = avoidanceVectorToBe + (vector * length);
            }
            avoidanceVector = avoidanceVectorToBe / count;
            //ShowVectorOnHud(_cubeGrid.GetPosition(), avoidanceVector);

            avoidedLastTurn = !double.IsNaN(avoidanceVector.Normalize());
            if (avoidedLastTurn)
                timeSinceLastAvoid = DateTime.Now;

            return !double.IsNaN(avoidanceVector.Normalize());
        }

        public void SetFleetZone(Vector3D pos)
        {
            defaultOrbitPoint = pos;
        }

        public bool HasCommandShip()
        {
            return commandShip != null;
        }

        public Ship getCommandShip()
        {
            return commandShip;
        }
    }
}
