using System;
using System.Collections.Generic;
using System.Linq;
using MiningDrones;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;
using IMyControllableEntity = VRage.Game.ModAPI.Interfaces.IMyControllableEntity;
using IMyGyro = Sandbox.ModAPI.IMyGyro;
using IMyTerminalBlock = Sandbox.ModAPI.Ingame.IMyTerminalBlock;


namespace DroneConquest
{
    class DroneNavigation
    {

        private static string logPath = "DroneNavigation.txt";
        //need these set
        public readonly Sandbox.ModAPI.IMyGridTerminalSystem GridTerminalSystem;
        public IMyCubeGrid Ship;
        private IMyControllableEntity _shipControls;

        //for orbiting
        private int _currentCoord;
        private int _previousCoord = 7;
        private List<Vector3D> _coords = new List<Vector3D>();
        private int _avoidanceMod = 20;

        #region NavigationVariables

        private int _avoidNumTargets = 5;
        private float bigOrbitRange = 5000;
        public float FollowRange;
        private List<AvoidedTarget> _recentlyAvoided = new List<AvoidedTarget>();
        private List<IMyEntity> _nearbyFloatingObjects;
        private List<IMyVoxelBase> _nearbyAsteroids = new List<IMyVoxelBase>();

        //when drones are approaching a target this is the distance used to calculate weather or not
        //they need to use the special ApproachSpeedMod devisor for calculating max speed
        private const int TargetApproachModRange = 300;
        public const int ApproachSpeedMod = 6;
        public double MaxSpeed = 100;

        public bool Avoiding;
        private double _avoidanceRange = 200;

        #endregion

        #region AlignmentNavigation variables

        //alignment 
        private bool _initialized = false;
        private bool _operational = true;
        private int alignCount = 10;
        private float alignSpeedMod = .05f;

        //Things needed to align to things
        private List<IMyTerminalBlock> _gyros = new List<IMyTerminalBlock>();

        private List<string> _gyroYaw;
        private List<string> _gyroPitch;
        private List<int> _gyroYawReverse;
        private List<int> _gyroPitchReverse;

        private double _degreesToVectorYaw = 0;
        private double _degreesToVectorPitch = 0;

        private Base6Directions.Direction _shipUp = 0;
        private Base6Directions.Direction _shipLeft = 0;

        #endregion

        public DroneNavigation(IMyCubeGrid ship, IMyControllableEntity shipControls,
            List<IMyEntity> nearbyFloatingObjects)
        {
            //stuff passed from sip
            _shipControls = shipControls;
            Ship = ship;
            GridTerminalSystem = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(ship);
            _nearbyFloatingObjects = nearbyFloatingObjects;

            var value = (ship.LocalAABB.Max - ship.LocalAABB.Center).Length();

            if (ship.Physics.Mass > 100000)
                FollowRange = 600 + value;
            else
                FollowRange = 400 + value;

            ShipOrientation();
            FindGyros();

            _initialized = true;

        }

        Vector3D previousAlign = Vector3D.Zero;
        //Working. The vector passed here must be a mean of all avoidance vectors devided by number of vectors
        //to get this
        //Take the positions of each nearby object and subtract your position from that enemy position. this will give you a Vector pointing away from target
        // to avoid multipule targets at once, Add all vectors together and devide by number of vectors
        public void AvoidTarget(Vector3D direction)
        {

            _shipControls.MoveAndRotate((direction / 4 * 3 + (Ship.Physics.LinearVelocity / 4 * 1)), Vector2.Zero, 0);
            AlignTo(previousAlign);
        }

        //yup
        public void TurnOffGyros(bool off)
        {
            for (int i = 0; i < _gyros.Count; i++)
            {
                if (((IMyGyro) _gyros[i]).GyroOverride != off)
                {
                    TerminalBlockExtentions.ApplyAction(_gyros[i], "Override");
                }
            }
        }

        public int AlignTo(Vector3D position)
        {
            for (int i = 0; i < alignCount && GyrosWork(); i++)
            {
                double realDistance = (position - Ship.GetPosition()).Length();
                DegreesToVector(position);
                PointToVector(0);
            }
            previousAlign = position;
            Util.GetInstance().Log("[DroneNavigation.AlignTo] returning pitch:" + Math.Abs(_degreesToVectorPitch) + " yaw:" + Math.Abs(_degreesToVectorYaw), logPath);
            return (int)(Math.Abs(_degreesToVectorPitch) + Math.Abs(_degreesToVectorYaw));

        }

        public void PointToVector(double precision)
        {
            if (_gyros != null)
            {
                for (int i = 0; i < _gyros.Count; i++)
                {
                    try
                    {
                        var gyro = _gyros[i] as IMyGyro;
                        if (!gyro.GyroOverride)
                        {
                            gyro.GetActionWithName("Override").Apply(gyro);
                        }
                        if (Math.Abs(_degreesToVectorYaw) > precision)
                        {
                            gyro.SetValueFloat(_gyroYaw[i],
                                (float) (_degreesToVectorYaw*alignSpeedMod)*(_gyroYawReverse[i]));
                        }
                        else
                        {
                            gyro.SetValueFloat(_gyroYaw[i], 0);
                        }
                        if (Math.Abs(_degreesToVectorPitch) > precision)
                        {
                            gyro.SetValueFloat(_gyroPitch[i],
                                (float) (_degreesToVectorPitch*alignSpeedMod)*(_gyroPitchReverse[i]));
                        }
                        else
                        {
                            gyro.SetValueFloat(_gyroPitch[i], 0);
                        }
                    }
                    catch (Exception e)
                    {
                        //Util.Notify(e.ToString());
                        //This is only to catch the occasional situation where the ship tried to align to something but has between the time the method started and now has lost a gyro or whatever
                    }
                }
            }
        }

        public void ShipOrientation()
        {
            if (_shipControls != null)
            {
                var Origin = ((IMyCubeBlock) _shipControls).GetPosition();
                var Up = Origin + (((IMyCubeBlock) _shipControls).LocalMatrix.Up);
                var Forward = Origin + (((IMyCubeBlock) _shipControls).LocalMatrix.Forward);
                var Left = Origin + (((IMyCubeBlock) _shipControls).LocalMatrix.Left);

                Vector3D forwardVector = Forward - Origin;
                Vector3D upVector = Up - Origin;
                Vector3D leftVector = Left - Origin;

                leftVector.Normalize();
                forwardVector.Normalize();
                upVector.Normalize();

                _shipUp = Base6Directions.GetDirection(upVector);
                _shipLeft = Base6Directions.GetDirection(leftVector);
            }
        }

        public void CompleteStop()
        {
            StopSpin();
            _shipControls.MoveAndRotateStopped();
            
        }

        private void StopSpin()
        {
            FindGyros();
            if (_gyros != null)
            {
                for (int i = 0; i < _gyros.Count; i++)
                {
                    try
                    {
                        if (((IMyGyro)_gyros[i]).GyroOverride)
                            _gyros[i].GetActionWithName("Override").Apply(_gyros[i]);
                    }
                    catch (Exception e)
                    {
                    }
                }
            }
        }

        //recalculate orbit vectors based on the position passed in
        //also configures mothership flight path by generating 50 random points
        public void ResetOrbitCoords(Vector3D pos, float range)
        {
            if (range > 0)
            {
                var x = pos.X;
                var y = pos.Y;
                var z = pos.Z;
                Random r = new Random();
                int val = r.Next(2);
                _coords.Clear();
                var cornerRange = range*.8;

                
                _coords.Add(new Vector3D(range + x, 0 + y, 0 + z));
                _coords.Add(new Vector3D(cornerRange + x, cornerRange + y, 0 + z));
                _coords.Add(new Vector3D(0 + x, range + y, 0 + z));
                _coords.Add(new Vector3D(-cornerRange + x, cornerRange + y, 0 + z));
                _coords.Add(new Vector3D(-range + x, 0 + y, 0 + z));
                _coords.Add(new Vector3D(-cornerRange + x, -cornerRange + y, 0 + z));
                _coords.Add(new Vector3D(0 + x, -range + y, 0 + z));
                _coords.Add(new Vector3D(cornerRange + x, -cornerRange + y, 0 + z));
            }
            else
            {
                if (_coords.Count < 25)
                {
                    Random r = new Random();
                    for (int i = 0; i < 50; i++)
                    {
                        _coords.Add(new Vector3D(r.Next(100000), r.Next(100000), r.Next(100000)));
                    }
                }
            }
        }




        public bool Orbit(Vector3D target)
        {
            ResetOrbitCoords(target, FollowRange);
            Util.GetInstance().Log("Starting orbit", logPath);
            if (!Avoiding && _shipControls != null)
            {
                Util.GetInstance().Log("o3" + target, logPath);
            Util.GetInstance().Log("o2", logPath);
                //MyAPIGateway.Session.Factions.TryGetPlayerFaction();
               

                Util.GetInstance().Log("o3" + _shipControls.Entity, logPath);
                Util.GetInstance().Log("o3" + _shipControls.Entity.GetPosition(), logPath);
                var distance = (target - _shipControls.Entity.GetPosition()).Length();
                Util.GetInstance().Log("distance/ApproachMaxSpeed "+distance+"/"+ApproachSpeedMod, logPath);
                MaxSpeed = distance / ApproachSpeedMod;
                Util.GetInstance().Log("maxSpeed "+MaxSpeed, logPath);
                if (MaxSpeed > 20)
                    MaxSpeed = 20;

                Util.GetInstance().Log("currentcoord " + _coords[_currentCoord], logPath);
                var dir = _coords[_currentCoord] - Ship.GetPosition();
                Util.GetInstance().Log("dir " + dir, logPath);
                if (Ship.Physics.LinearVelocity.Normalize() > MaxSpeed || !FlyingTwords(dir))
                {
                    Util.GetInstance().Log("Over Max speed, not flying correct direction", logPath);
                    _shipControls.MoveAndRotateStopped();
                    //((IMyRemoteControl)_shipControls).
                }
                else
                {
                    //validateTarget(); //This is just to make sure our owner/target was not destoried
                    ResetOrbitCoords(target, FollowRange);
                    Util.GetInstance().Log("reset orbit coords", logPath);

                    if (((_coords[_currentCoord + 1 >= _coords.Count ? 0 : _currentCoord + 1] - Ship.GetPosition()).Length()
                        < (_coords[_currentCoord] - Ship.GetPosition()).Length()) && (_coords[_currentCoord] - Ship.GetPosition()).Length() < FollowRange / 4)
                    {
                        
                        _currentCoord = _currentCoord + 1;
                    }


                    if (_currentCoord >= _coords.Count)
                        _currentCoord = 0;

                    //AlignTo(_coords[_currentCoord]);
                    
                    AlignTo(_coords[_currentCoord]);
                    //_toPosition - _fromPosition
                    if (dir.Length() < (_coords[_currentCoord] - _coords[_previousCoord]).Length() * .66)
                    {
                        _previousCoord = _currentCoord;
                        _currentCoord++;

                        if (_currentCoord >= _coords.Count)
                            _currentCoord = 0;
                    }

                    if (Math.Abs(dir.LengthSquared()) > 0)
                    {
                        _shipControls.MoveAndRotate(dir, new Vector2(), 0);
                        
                        //this calculates max speed based on distance
                        if (Ship.Physics.LinearVelocity.Normalize() > MaxSpeed)
                        {
                            _shipControls.MoveAndRotateStopped();
                        }
                    }
                    else
                        _shipControls.MoveAndRotateStopped();
                }
                Util.GetInstance().Notify("methodreturn");
                return true;
            }

            //indicates the drone was avoiding rather than orbiting
            Util.GetInstance().Notify("methodreturn");
            return false;
        }

        public bool CombatOrbit(Vector3D target)
        {
            if (!Avoiding && _shipControls != null)
            {
                var distance = (target - Ship.GetPosition()).Length();
                //MaxSpeed = ;
                //MaxSpeed = (target - Ship.GetPosition()).Length() < FollowRange*.8 ? 20 : MaxSpeed;

                //oh no... a turnary inside a turnary... someone burn me on a stick... nah its okay if I do it this one time
                MaxSpeed = MaxSpeed < 10 ? 10 :
                                distance > FollowRange ? distance / ApproachSpeedMod : 20;

                var dir = _coords[_currentCoord] - Ship.GetPosition();

                if (Ship.Physics.LinearVelocity.Normalize() > MaxSpeed || !FlyingTwords(dir))
                {
                    _shipControls.MoveAndRotateStopped();
                }
                else
                {
                    //Util.Notify("cp1");
                    //validateTarget(); //This is just to make sure our owner/target was not destoried
                    ResetOrbitCoords(target, FollowRange);


                    //NavInfo nav = new NavInfo(Ship.GetPosition(), _coords[_currentCoord], (IMyEntity)_shipControls);
                    
                    //_toPosition - _fromPosition for distance or to generate a vector twords target
                    if (dir.Length() < (_coords[_currentCoord] - _coords[_previousCoord]).Length() * .66)
                    {
                        _previousCoord = _currentCoord;
                        _currentCoord++;

                        if (_currentCoord >= _coords.Count)
                            _currentCoord = 0;
                    }

                    if (Math.Abs(dir.LengthSquared()) > 0)
                    {
                        _shipControls.MoveAndRotate(dir, Vector2.Zero, 0);

                        //this calculates max speed based on distance
                        if (Ship.Physics.LinearVelocity.Normalize() > MaxSpeed)
                        {
                            _shipControls.MoveAndRotateStopped();
                        }
                    }
                    else
                        _shipControls.MoveAndRotateStopped();
                }
                return true;
            }

            //indicates the drone was avoiding rather than orbiting
            return false;
        }

        private bool FlyingTwords(Vector3D dir)
        {
            
            int x = Math.Sign(dir.X) + Math.Sign(Ship.Physics.LinearVelocity.X) > 0 ? 1 : -1;
            int y = Math.Sign(dir.Y) + Math.Sign(Ship.Physics.LinearVelocity.Y) > 0 ? 1 : -1;
            int z = Math.Sign(dir.Z) + Math.Sign(Ship.Physics.LinearVelocity.Z) > 0 ? 1 : -1;

            if (Ship.Physics.LinearVelocity.X < 10)
                x = 1;
            if (Ship.Physics.LinearVelocity.Y < 10)
                y = 1;
            if (Ship.Physics.LinearVelocity.Z < 10)
                z = 1;

            bool samedirection = (x + y + z) > 0;
            Util.GetInstance().Log(dir.X + " : " + dir.Y + " : " + dir.Z + " : dir", "directionalVelocity.txt");
            Util.GetInstance()
                .Log(
                    Ship.Physics.LinearVelocity.X + " : " + Ship.Physics.LinearVelocity.Y + " : " +
                    Ship.Physics.LinearVelocity.Z + " : phy", "directionalVelocity.txt");
            Util.GetInstance().Log(x + " : " + y + " : " + z + " : " + samedirection, "directionalVelocity.txt");
            return samedirection;
            
        }

        //Working
        public bool Follow(Vector3D position)
        {
            if (Ship != null && !Avoiding && _shipControls != null)
            {
                //NavInfo nav = new NavInfo(Ship.GetPosition(), position, (IMyEntity)_shipControls);
                var dir = position - Ship.GetPosition();
                var distance = (position - Ship.GetPosition()).Length();
                MaxSpeed = distance > FollowRange ? distance / ApproachSpeedMod : 40;


                if (Ship.Physics.LinearVelocity.Normalize() > MaxSpeed || !FlyingTwords(dir))
                {
                    _shipControls.MoveAndRotateStopped();
                }else
                {
                    AlignTo(position);

                    if (dir.Length() < FollowRange)
                    {
                        if (Ship.Physics.LinearVelocity.Normalize() > 0)
                        {
                            //Ship.Physics.AddForce();
                            _shipControls.MoveAndRotateStopped();
                        }
                    }
                    else
                    {
                        if (Math.Abs(dir.Length()) > FollowRange)
                        {

                            _shipControls.MoveAndRotate(dir, new Vector2(), 0);
                            AlignTo(position);
                        }
                    }

                    if (Ship.Physics.LinearVelocity.Normalize() > MaxSpeed)
                    {
                        _shipControls.MoveAndRotateStopped();
                    }
                }
                return true;
            }

            //indicates the drone was avoiding rather than following
            return false;
        }

        public bool Follow(Vector3D position, int followDistance)
        {
            if (Ship != null && !Avoiding && _shipControls != null)
            {
                //NavInfo nav = new NavInfo(Ship.GetPosition(), position, (IMyEntity)_shipControls);
                var dir = position - Ship.GetPosition();
                var distance = (position - Ship.GetPosition()).Length();
                MaxSpeed = distance > followDistance ? distance / ApproachSpeedMod : 40;
                if (distance > 100)
                    MaxSpeed = distance > followDistance ? distance / ApproachSpeedMod : 100;

                if (Ship.Physics.LinearVelocity.Normalize() > MaxSpeed)
                {
                    _shipControls.MoveAndRotateStopped();
                }
                else
                {
                    AlignTo(position);

                    if (dir.Length() < followDistance)
                    {
                        if (Ship.Physics.LinearVelocity.Normalize() > 0)
                        {
                            _shipControls.MoveAndRotateStopped();
                        }
                    }
                    else
                    {
                        if (Math.Abs(dir.Length()) > followDistance)
                        {
                            _shipControls.MoveAndRotate(dir, new Vector2(), 0);
                            AlignTo(position);
                        }
                    }

                    if (Ship.Physics.LinearVelocity.Normalize() > MaxSpeed)
                    {
                        _shipControls.MoveAndRotateStopped();
                    }
                }
                return true;
            }

            //indicates the drone was avoiding rather than following
            return false;
        }


        //calculated the pitch and roll needed to aim at the target
        public void DegreesToVector(Vector3D TV)
        {
            if (_shipControls != null)
            {
                var Origin = ((IMyCubeBlock) _shipControls).GetPosition();
                var Up = (((IMyCubeBlock) _shipControls).WorldMatrix.Up);
                var Forward = (((IMyCubeBlock) _shipControls).WorldMatrix.Forward);
                var Right = (((IMyCubeBlock) _shipControls).WorldMatrix.Right);
                // ExplainVector(Origin, "Origin");
                // ExplainVector(Up, "up");

                Vector3D OV = Origin; //Get positions of reference blocks.    
                Vector3D FV = Origin + Forward;
                Vector3D UV = Origin + Up;
                Vector3D RV = Origin + Right;

                //Get magnitudes of vectors.
                double TVOV = (OV - TV).Length();

                double TVFV = (FV - TV).Length();
                double TVUV = (UV - TV).Length();
                double TVRV = (RV - TV).Length();

                double OVUV = (UV - OV).Length();
                double OVRV = (RV - OV).Length();

                double ThetaP = Math.Acos((TVUV*TVUV - OVUV*OVUV - TVOV*TVOV)/(-2*OVUV*TVOV));
                //Use law of cosines to determine angles.    
                double ThetaY = Math.Acos((TVRV*TVRV - OVRV*OVRV - TVOV*TVOV)/(-2*OVRV*TVOV));

                double RPitch = 90 - (ThetaP*180/Math.PI); //Convert from radians to degrees.    
                double RYaw = 90 - (ThetaY*180/Math.PI);

                if (TVOV < TVFV) RPitch = 180 - RPitch; //Normalize angles to -180 to 180 degrees.    
                if (RPitch > 180) RPitch = -1*(360 - RPitch);

                if (TVOV < TVFV) RYaw = 180 - RYaw;
                if (RYaw > 180) RYaw = -1*(360 - RYaw);

                _degreesToVectorYaw = RYaw;
                _degreesToVectorPitch = RPitch;
            }

        }

        private void ExplainVector(Vector3D point, string name)
        {
            Util.GetInstance().Notify(name + ": (x) " + point.X + " (y)" + point.Y + " (z) " + point.Z);
        }

        public bool NavigationWorking()
        {
            List<string> errors = new List<string>();

            bool shipWorking = true;

            if (Ship == null)
            {
                errors.Add("Ship Grid Missing: ");
                shipWorking = false;
            }
            if (_shipControls == null)
            {
                errors.Add("Remote Control Missing: ");
                shipWorking = false;
            }
            if (!GyrosWork())
            {
                errors.Add("No Gyros Found: ");
                shipWorking = false;
            }

            return shipWorking;
        }

        //chwecking that there is atleast 1 gyro
        private bool GyrosWork()
        {
            FindGyros();
            if (_gyros.Count == 0)
                return false;
            return true;
        }

        public void AddNearbyAsteroid(IMyVoxelBase asteroid)
        {
            if (!_nearbyAsteroids.Contains(asteroid))
                _nearbyAsteroids.Add(asteroid);
        }

        internal class AvoidedTarget
        {
            public IMyEntity Entity = null;
            public int TimesAvoided = 0;
        }

        public void FindGyros()
        {
            _gyros = new List<IMyTerminalBlock>();
            _gyroYaw = new List<string>();
            _gyroPitch = new List<string>();
            _gyroYawReverse = new List<int>();
            _gyroPitchReverse = new List<int>();
            GridTerminalSystem.GetBlocksOfType<IMyGyro>(_gyros);
            for (int i = 0; i < _gyros.Count; i++)
            {
                if ((_gyros[i]).IsFunctional)
                {
                    Base6Directions.Direction gyroUp = _gyros[i].Orientation.TransformDirectionInverse(_shipUp);
                    Base6Directions.Direction gyroLeft = _gyros[i].Orientation.TransformDirectionInverse(_shipLeft);


                    if (gyroUp == Base6Directions.Direction.Up)
                    {
                        _gyroYaw.Add("Yaw");
                        _gyroYawReverse.Add(1);
                    }
                    else if (gyroUp == Base6Directions.Direction.Down)
                    {
                        _gyroYaw.Add("Yaw");
                        _gyroYawReverse.Add(-1);
                    }
                    else if (gyroUp == Base6Directions.Direction.Left)
                    {
                        _gyroYaw.Add("Pitch");
                        _gyroYawReverse.Add(1);
                    }
                    else if (gyroUp == Base6Directions.Direction.Right)
                    {
                        _gyroYaw.Add("Pitch");
                        _gyroYawReverse.Add(-1);
                    }
                    else if (gyroUp == Base6Directions.Direction.Forward)
                    {
                        _gyroYaw.Add("Roll");
                        _gyroYawReverse.Add(-1);
                    }
                    else if (gyroUp == Base6Directions.Direction.Backward)
                    {
                        _gyroYaw.Add("Roll");
                        _gyroYawReverse.Add(1);
                    }

                    if (gyroLeft == Base6Directions.Direction.Up)
                    {
                        _gyroPitch.Add("Yaw");
                        _gyroPitchReverse.Add(1);
                    }
                    else if (gyroLeft == Base6Directions.Direction.Down)
                    {
                        _gyroPitch.Add("Yaw");
                        _gyroPitchReverse.Add(-1);
                    }
                    else if (gyroLeft == Base6Directions.Direction.Left)
                    {
                        _gyroPitch.Add("Pitch");
                        _gyroPitchReverse.Add(1);
                    }
                    else if (gyroLeft == Base6Directions.Direction.Right)
                    {
                        _gyroPitch.Add("Pitch");
                        _gyroPitchReverse.Add(-1);
                    }
                    else if (gyroLeft == Base6Directions.Direction.Forward)
                    {
                        _gyroPitch.Add("Roll");
                        _gyroPitchReverse.Add(-1);
                    }
                    else if (gyroLeft == Base6Directions.Direction.Backward)
                    {
                        _gyroPitch.Add("Roll");
                        _gyroPitchReverse.Add(1);
                    }
                }
            }
        }

        //Working. this sorts through the list of nearbyentities and calculates a single avoidance vector
        

        public void ApproachLocation(Vector3D position)
        {
            Util.GetInstance().Log("Inside Approach Location Method", logPath);
            if (Ship != null && _shipControls != null)
            {
                Util.GetInstance().Log("ship not null, _controlls not nul", logPath);
                //NavInfo nav = new NavInfo(Ship.GetPosition(), position, (IMyEntity)_shipControls);
                var dir = position - Ship.GetPosition();
                var distance = (position - Ship.GetPosition()).Length();
                MaxSpeed = 5;//distance > FollowRange ? distance / ApproachSpeedMod : 40;
                bool shipMovingTooFast = Ship.Physics.LinearVelocity.Normalize() > MaxSpeed;
                bool isShipAlignedToLocation = FlyingTwords(dir);
                bool isShipWithinFollowRange = Math.Abs(dir.Length()) < FollowRange;
                bool isShipStopped = Math.Abs(Ship.Physics.LinearVelocity.Normalize()) < 0;
                Util.GetInstance().Log("distance "+distance, logPath);
                Util.GetInstance().Log("maxSpeed " + MaxSpeed, logPath);

                Util.GetInstance().Log("currentSpeed " + Ship.Physics.LinearVelocity.Normalize(), logPath);

                if (shipMovingTooFast)
                {
                     _shipControls.MoveAndRotateStopped();
                    
                }
                else
                {
                    Util.GetInstance().Log("dir/aligned/range/stopped/toofast " + dir + "/" + isShipAlignedToLocation + "/" + isShipWithinFollowRange + isShipStopped + "/" + shipMovingTooFast, logPath);
                    NavInfo navInfo = new NavInfo(Ship.GetPosition(), position, _shipControls.Entity);
                    _shipControls.MoveAndRotate(navInfo.Direction, navInfo.Rotation, navInfo.Roll);

                    //MyRemoteControl remote = _shipControls as MyRemoteControl;
                    //remote.PreviousControlledEntity.MoveAndRotate();
                    //_shipControls.MoveAndRotate(dir, new Vector2(), 0);
                    //MyAPIGateway.Session.GPS.AddLocalGps(MyAPIGateway.Session.GPS.Create("destination", "null", Ship.GetPosition() + dir, true, true));
                }   
            }
        }
    }
    internal class NavInfo
    {
        private Vector3D _fromPosition;
        private Vector3D _toPosition;
        private IMyEntity _shipControls;
        private Vector3D _dir;
        private Vector2 _rot;
        private float _roll;

        public NavInfo(Vector3D fromPosition, Vector3D toPosition, IMyEntity shipControls)
        {
            _fromPosition = fromPosition;
            _toPosition = toPosition;
            _shipControls = shipControls;
            Init();
        }

        public float Roll
        {
            get { return _roll; }
            set { _roll = value; }
        }

        public Vector2 Rotation
        {
            get { return _rot; }
            set { _rot = value; }
        }

        public Vector3D Direction
        {
            get { return _dir; }
            set { _dir = value; }
        }

        public static Vector2 CalculateRotation(Vector3D direction, IMyEntity _shipControls)
        {
            var _dir = direction - _shipControls.GetPosition();
            var dirNorm = Vector3D.Normalize(_dir);
            var x = -(_shipControls as IMyEntity).WorldMatrix.Up.Dot(dirNorm);
            var y = -(_shipControls as IMyEntity).WorldMatrix.Left.Dot(dirNorm);
            var forw = (_shipControls as IMyEntity).WorldMatrix.Forward.Dot(dirNorm);

            if (forw < 0)
                y = 0;
            if (Math.Abs(x) < 0.07f)
                x = 0;
            if (Math.Abs(y) < 0.07f)
                y = 0;

            return new Vector2((float)x, (float)y);
        }

        public void FlyInDirection()
        {
            
        }


        public void Init()
        {
            _dir = _toPosition - _fromPosition;

            var dirNorm = Vector3D.Normalize(_dir);
            var x = -_shipControls.WorldMatrix.Up.Dot(dirNorm);
            var y = -_shipControls.WorldMatrix.Left.Dot(dirNorm);
            var forw = _shipControls.WorldMatrix.Forward.Dot(dirNorm);

            //if (forw < 0)
            //    y = 0;
            //if (Math.Abs(x) < 0)
            //    x = 0;
            //if (Math.Abs(y) < 0)
            //    y = 0;


            //if (_dir.Length() < 30)
            //    _dir = Vector3D.Zero;
            //else
                //_dir = Vector3D.TransformNormal(_dir, _shipControls.WorldMatrixNormalizedInv);

            _rot = new Vector2((float)x, (float)y);
            _roll = 0;
        }
    }
}
