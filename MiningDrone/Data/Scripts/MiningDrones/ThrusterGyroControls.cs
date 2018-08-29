using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using DroneConquest;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.GameSystems;
using Sandbox.Game.Gui;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;
using IMyControllableEntity = VRage.Game.ModAPI.Interfaces.IMyControllableEntity;
using IMyGyro = Sandbox.ModAPI.IMyGyro;
using IMyTerminalBlock = Sandbox.ModAPI.Ingame.IMyTerminalBlock;
using IMyThrust = Sandbox.ModAPI.IMyThrust;
using ITerminalAction = Sandbox.ModAPI.Interfaces.ITerminalAction;
using Sandbox.ModAPI.Ingame;

namespace MiningDrones
{
    class ThrusterGyroControls
    {
        private string _logPath = "ThrusterGyroControls.txt";
        private IMyEntity _ship;
        private IMyCubeGrid _grid;
        private IMyControllableEntity _remoteControl;
        private Sandbox.ModAPI.IMyGridTerminalSystem _gridTerminalSystem;
        private OrbitTypes orbitType = OrbitTypes.Default;


        //alignment
        private Base6Directions.Direction _shipUp = 0;
        private Base6Directions.Direction _shipLeft = 0;
        private double _degreesToVectorYaw = 0;
        private double _degreesToVectorPitch = 0;
        private float _alignSpeedMod = .075f;
        private List<string> _gyroYaw;
        private List<string> _gyroPitch;
        private List<int> _gyroYawReverse;
        private List<int> _gyroPitchReverse;
        private List<IMyTerminalBlock> _gyros;
        private List<IMyTerminalBlock> _thrusters;
        private List<Vector3D> _coords;
        private int _currentCoord;
        private int _previousCoord = 7;
        private int _orbitRange = 500;
        public const int ApproachSpeedMod = 6;
        public double MaxSpeed = 50;

        public ThrusterGyroControls(IMyEntity entity, VRage.Game.ModAPI.Interfaces.IMyControllableEntity cont)
        {
            _remoteControl = cont;
            _grid = entity as IMyCubeGrid;
            _ship = entity;

            if (_grid != null)
                _gridTerminalSystem = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(_grid);

            ShipOrientation();
            RefreshGyros();
            RefreshThrusters();
            Util.GetInstance().Log("inside", _logPath);

        }

        public bool PlayerHasControl()
        {
            return (_remoteControl as Sandbox.ModAPI.IMyRemoteControl).IsUnderControl;
        }

        public double AlignTo(Vector3D position)
        {
            DegreesToVector(position);
            PointToVector(0);
            return Math.Abs((_degreesToVectorPitch + _degreesToVectorYaw) / 2);
            //Util.GetInstance().Log("[DroneNavigation.AlignTo] returning pitch:" + Math.Abs(_degreesToVectorPitch) + " yaw:" + Math.Abs(_degreesToVectorYaw), logPath);
            //return (int)(Math.Abs(_degreesToVectorPitch) + Math.Abs(_degreesToVectorYaw));
        }

        public void StopSpin()
        {
            RefreshGyros();
            TurnOffGyros(false);
        }

        private void TurnOffGyros(bool off)
        {
            RefreshGyros();
            for (int i = 0; i < _gyros.Count; i++)
            {
                if (((IMyGyro)_gyros[i]).GyroOverride != off)
                {
                    TerminalBlockExtentions.ApplyAction(_gyros[i], "Override");
                }
            }
        }

        private void DegreesToVector(Vector3D TV)
        {
            if (_remoteControl != null)
            {
                var Origin = ((IMyCubeBlock)_remoteControl).GetPosition();
                var Up = (((IMyCubeBlock)_remoteControl).WorldMatrix.Up);
                var Forward = (((IMyCubeBlock)_remoteControl).WorldMatrix.Forward);
                var Right = (((IMyCubeBlock)_remoteControl).WorldMatrix.Right);
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

                double ThetaP = Math.Acos((TVUV * TVUV - OVUV * OVUV - TVOV * TVOV) / (-2 * OVUV * TVOV));
                //Use law of cosines to determine angles.    
                double ThetaY = Math.Acos((TVRV * TVRV - OVRV * OVRV - TVOV * TVOV) / (-2 * OVRV * TVOV));

                double RPitch = 90 - (ThetaP * 180 / Math.PI); //Convert from radians to degrees.    
                double RYaw = 90 - (ThetaY * 180 / Math.PI);

                if (TVOV < TVFV) RPitch = 180 - RPitch; //Normalize angles to -180 to 180 degrees.    
                if (RPitch > 180) RPitch = -1 * (360 - RPitch);

                if (TVOV < TVFV) RYaw = 180 - RYaw;
                if (RYaw > 180) RYaw = -1 * (360 - RYaw);

                _degreesToVectorYaw = RYaw;
                _degreesToVectorPitch = RPitch;
            }

        }
        public void PointToVector(double precision)
        {
            RefreshGyros();
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
                                (float)(_degreesToVectorYaw * _alignSpeedMod) * (_gyroYawReverse[i]));
                        }
                        else
                        {
                            gyro.SetValueFloat(_gyroYaw[i], 0);
                        }
                        if (Math.Abs(_degreesToVectorPitch) > precision)
                        {
                            gyro.SetValueFloat(_gyroPitch[i],
                                (float)(_degreesToVectorPitch * _alignSpeedMod) * (_gyroPitchReverse[i]));
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
            if (_remoteControl != null)
            {
                var Origin = ((IMyCubeBlock)_remoteControl).GetPosition();
                var Up = Origin + (((IMyCubeBlock)_remoteControl).LocalMatrix.Up);
                var Forward = Origin + (((IMyCubeBlock)_remoteControl).LocalMatrix.Forward);
                var Left = Origin + (((IMyCubeBlock)_remoteControl).LocalMatrix.Left);

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


        private void RefreshGyros()
        {
            _gyros = new List<IMyTerminalBlock>();
            _gyroYaw = new List<string>();
            _gyroPitch = new List<string>();
            _gyroYawReverse = new List<int>();
            _gyroPitchReverse = new List<int>();
            _gridTerminalSystem.GetBlocksOfType<MyGyro>(_gyros);
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

        private void RefreshThrusters()
        {
            _thrusters = new List<IMyTerminalBlock>();
            _gridTerminalSystem.GetBlocksOfType<MyThrust>(_thrusters);
        }

        public void SlowDown()
        {
            foreach (var thru in _thrusters)
            {
                MyThrust thr = thru as MyThrust;
                thr.SetValueFloat("Override", 0);
            }

            if (!_remoteControl.EnabledDamping)
                _remoteControl.SwitchDamping();

            _remoteControl.MoveAndRotateStopped();
        }

        public void WeightedThrustTwordsDirection(Vector3D thrustVector, bool secondTry = false, bool usingHalfPower = false)
        {

            RefreshThrusters();
            int fullpower = usingHalfPower ? 75 : 150;
            int lowpower = 50;
            

            //generalize the thruster direction to 1,0,-1
            int xPos = thrustVector.X > 0 ? 1 : thrustVector.X < 0 ? -1 : 0;
            int yPos = thrustVector.Y > 0 ? 1 : thrustVector.Y < 0 ? -1 : 0;
            int zPos = thrustVector.Z > 0 ? 1 : thrustVector.Z < 0 ? -1 : 0;
            var desiredVector = new Vector3D(xPos, yPos, zPos);

            int xv = _ship.Physics.LinearVelocity.X > 0 ? 1 : _ship.Physics.LinearVelocity.X < 0 ? -1 : 0;
            int yv = _ship.Physics.LinearVelocity.Y > 0 ? 1 : _ship.Physics.LinearVelocity.Y < 0 ? -1 : 0;
            int zv = _ship.Physics.LinearVelocity.Z > 0 ? 1 : _ship.Physics.LinearVelocity.Z < 0 ? -1 : 0;

            //if vector does not match, get the non matching vectors so we can match some thrusters against the new vector ("counterDriftVector" that stops drift, but does not go against the desired thruster direction)
            int xm = xPos == xv ? 0 : xv;
            int ym = yPos == yv ? 0 : yv;
            int zm = zPos == zv ? 0 : zv;
            var counterDriftVector = new Vector3D(xm, ym, zm);

            bool successfullyMoved = false;
            foreach (var thruster in _thrusters)
            {
                Vector3D fow = thruster.WorldMatrix.Forward;
                int xt = fow.X > 0 ? 1 : fow.X < 0 ? -1 : 0;
                int yt = fow.Y > 0 ? 1 : fow.Y < 0 ? -1 : 0;
                int zt = fow.Z > 0 ? 1 : fow.Z < 0 ? -1 : 0;
                var thrusterVector = new Vector3D(xt, yt, zt);

                bool notDrifting = counterDriftVector == Vector3D.Zero;
                bool thrusterPointsDesiredDirection = thrusterVector.Equals(desiredVector);
                bool thrusterCountersDrift = thrusterVector.Equals(counterDriftVector) && !notDrifting;
                bool thrusterNeedsPower = thrusterCountersDrift || thrusterPointsDesiredDirection;

                double angle = AngleBetween(thrustVector, desiredVector, true);
                Util.GetInstance().Log("Angle " + angle, _logPath);
                if (angle > 80)
                {
                    int power = thrusterPointsDesiredDirection ? fullpower : lowpower;
                    if (notDrifting)
                        power = fullpower;

                    thruster.GetActionWithName("OnOff_On").Apply(thruster);
                    thruster.SetValueFloat("Override", power);
                    successfullyMoved = true;
                }
                //just attempt to move the ship
                else if (secondTry && angle > 89)
                {
                    thruster.GetActionWithName("OnOff_On").Apply(thruster);
                    thruster.SetValueFloat("Override", fullpower);
                    successfullyMoved = true;
                }
                else
                {
                    thruster.SetValueFloat("Override", 0);
                }
            }
            if (!successfullyMoved && !secondTry)
                ThrustTwordsDirection(thrustVector, true, usingHalfPower);
        }

        public bool ThrustTwordsDirection(Vector3D thrustVector, bool secondTry = false, bool usingHalfPower = false)
        {
            RefreshThrusters();
            int fullpower = usingHalfPower ? 75 : 150;
            int lowpower = 50;

            var thrustVector2 = thrustVector - _ship.Physics.LinearVelocity;

            //generalize the thruster direction to 1,0,-1
            int xPos = thrustVector2.X > 0 ? 1 : thrustVector2.X < 0 ? -1 : 0;
            int yPos = thrustVector2.Y > 0 ? 1 : thrustVector2.Y < 0 ? -1 : 0;
            int zPos = thrustVector2.Z > 0 ? 1 : thrustVector2.Z < 0 ? -1 : 0;
            var desiredVector = new Vector3D(xPos, yPos, zPos);

            int xv = _ship.Physics.LinearVelocity.X > 0 ? 1 : _ship.Physics.LinearVelocity.X < 0 ? -1 : 0;
            int yv = _ship.Physics.LinearVelocity.Y > 0 ? 1 : _ship.Physics.LinearVelocity.Y < 0 ? -1 : 0;
            int zv = _ship.Physics.LinearVelocity.Z > 0 ? 1 : _ship.Physics.LinearVelocity.Z < 0 ? -1 : 0;

            //if vector does not match, get the non matching vectors so we can match some thrusters against the new vector ("counterDriftVector" that stops drift, but does not go against the desired thruster direction)
            int xm = xPos == xv ? 0 : xv;
            int ym = yPos == yv ? 0 : yv;
            int zm = zPos == zv ? 0 : zv;
            var counterDriftVector = new Vector3D(xm, ym, zm);

            bool successfullyMoved = false;
            foreach (var thruster in _thrusters)
            {
                Vector3D fow = thruster.WorldMatrix.Forward;
                int xt = fow.X > 0 ? 1 : fow.X < 0 ? -1 : 0;
                int yt = fow.Y > 0 ? 1 : fow.Y < 0 ? -1 : 0;
                int zt = fow.Z > 0 ? 1 : fow.Z < 0 ? -1 : 0;
                var thrusterVector = new Vector3D(xt, yt, zt);

                bool notDrifting = counterDriftVector == Vector3D.Zero;
                bool thrusterPointsDesiredDirection = thrusterVector.Equals(desiredVector);
                bool thrusterCountersDrift = thrusterVector.Equals(counterDriftVector) && !notDrifting;
                bool thrusterNeedsPower = thrusterCountersDrift || thrusterPointsDesiredDirection;

                double angle = AngleBetween(thrusterVector, desiredVector, true);
                Util.GetInstance().Log("Angle "+angle,_logPath);
                if (thrusterNeedsPower)
                {
                    int power = thrusterPointsDesiredDirection ? fullpower : lowpower;
                    if (notDrifting)
                        power = fullpower;

                    thruster.GetActionWithName("OnOff_On").Apply(thruster);
                    thruster.SetValueFloat("Override", power);
                    successfullyMoved = true;
                }
                    //just attempt to move the ship
                else if (secondTry && angle<80)
                {
                    thruster.GetActionWithName("OnOff_On").Apply(thruster);
                    thruster.SetValueFloat("Override", fullpower);
                    successfullyMoved = true;
                }
                else{
                    thruster.SetValueFloat("Override", 0);
                }
            }
            if (!successfullyMoved && !secondTry)
                ThrustTwordsDirection(thrustVector, true, usingHalfPower);
            
            return successfullyMoved;
        }

        double AngleBetween(Vector3D u, Vector3D v, bool returndegrees)
        {
            double toppart = 0;
            toppart += u.X * v.X;
            toppart += u.Y * v.Y;
            toppart += u.Z * v.Z;

            double u2 = 0; //u squared
            double v2 = 0; //v squared
            
            u2 += u.X * u.X;
            v2 += v.X * v.X;
            u2 += u.Y * u.Y;
            v2 += v.Y * v.Y;
            u2 += u.Z * u.Z;
            v2 += v.Z * v.Z;
            
            double bottompart = 0;
            bottompart = Math.Sqrt(u2 * v2);

            double rtnval = Math.Acos(toppart / bottompart);
            if (returndegrees) rtnval *= 360.0 / (2 * Math.PI);
            return rtnval;
        }

        public bool Orbit(Vector3D target)
        {
            if (_remoteControl != null)
            {
                ResetOrbitCoords(target, _orbitRange);
                Util.GetInstance().Log("ck1 ",_logPath);
                var dir = _ship.GetPosition() - _coords[_currentCoord];
                Util.GetInstance().Log("ck2 ", _logPath);
                double distanceFromPlayer = (target - _ship.GetPosition()).Length();
                MaxSpeed = 40;
                var orbitPoint = _coords[_currentCoord];
                Util.GetInstance().Log((orbitPoint - _ship.GetPosition()).Length() + " ck3", _logPath);
                HighlightDestinationVector(orbitPoint);
                Util.GetInstance().Log("distance " + distanceFromPlayer, _logPath);

                if (_ship.Physics.LinearVelocity.Normalize() > MaxSpeed)
                {
                    Util.GetInstance().Log("slowing in normal orbit", _logPath);
                    AlignTo(orbitPoint);
                    SlowDown();
                }
                else
                {
                    Util.GetInstance().Log("Thrusting in normal orbit", _logPath);
                    AlignTo(orbitPoint);
                    ThrustTwordsDirection(dir);
                }

            }
            return false;
        }
        private List<IMyGps> _destinationMarkers = new List<IMyGps>();

        private void HighlightDestinationVector(Vector3D location)
        {
            //foreach (var marker in _destinationMarkers)
            //{
            //    MyAPIGateway.Session.GPS.RemoveLocalGps(marker);
            //}
            //IMyGps gpsx = MyAPIGateway.Session.GPS.Create("Destination", "drone destination", location + new Vector3D(10, 0, 0), true);
            //IMyGps gpsy = MyAPIGateway.Session.GPS.Create("Destination", "drone destination", location + new Vector3D(0, 10, 0), true);
            //IMyGps gpsz = MyAPIGateway.Session.GPS.Create("Destination", "drone destination", location + new Vector3D(0, 0, 10), true);
            //_destinationMarkers.Add(gpsx);
            //_destinationMarkers.Add(gpsy);
            //_destinationMarkers.Add(gpsz);

            //MyAPIGateway.Session.GPS.AddLocalGps(gpsx);
            //MyAPIGateway.Session.GPS.AddLocalGps(gpsy);
            //MyAPIGateway.Session.GPS.AddLocalGps(gpsz);
        }

        public bool AimFreeOrbit(Vector3D target, float range)
        {
            if (_remoteControl != null)
            {
                ResetOrbitCoords(target, range);

                var dir = _ship.GetPosition() - _coords[_currentCoord];

                double distanceFromPlayer = (target - _ship.GetPosition()).Length();

                MaxSpeed = 40;
                HighlightDestinationVector(_coords[_currentCoord]);

                if (_ship.Physics.LinearVelocity.Normalize() > MaxSpeed)
                {
                    Util.GetInstance().Log("slowing ", _logPath);
                    SlowDown();
                }

                else
                {
                    ThrustTwordsDirection(dir);
                }

            }
            return false;
        }
        private static Random r = new Random();

        private Vector3D GetOrbitAlignmentVector()
        {
            var val = 0;
            Vector3D vector = Vector3D.Zero;
            if (orbitType == OrbitTypes.Default)
            {
                
                val = r.Next(0, 5);
                switch (val)
                {
                    case 0:
                        orbitType = OrbitTypes.X;
                        break;
                    case 1:
                        orbitType = OrbitTypes.XY;
                        break;
                    case 2:
                        orbitType = OrbitTypes.Y;
                        break;
                    case 3:
                        orbitType = OrbitTypes.YZ;
                        break;
                    case 4:
                        orbitType = OrbitTypes.Z;
                        break;
                    case 5:
                        orbitType = OrbitTypes.XZ;
                        break;
                }
            }

            switch (orbitType)
            {
                case OrbitTypes.X:
                    vector = new Vector3D(_orbitRange*1 , _orbitRange * 0, _orbitRange * 0);
                    break;
                case OrbitTypes.XY:
                    vector = new Vector3D(_orbitRange * 1, _orbitRange * 1, _orbitRange * 0);
                    break;
                case OrbitTypes.Y:
                    vector = new Vector3D(_orbitRange * 0, _orbitRange * 1, _orbitRange * 0);
                    break;
                case OrbitTypes.YZ:
                    vector = new Vector3D(_orbitRange * 0, _orbitRange * 1, _orbitRange * 1);
                    break;
                case OrbitTypes.Z:
                    vector = new Vector3D(_orbitRange * 0, _orbitRange * 0, _orbitRange * 1);
                    break;
                case OrbitTypes.XZ:
                    vector = new Vector3D(_orbitRange * 1, _orbitRange * 0, _orbitRange * 1);
                    break;
            }

            return vector;

        }

        public void ResetOrbitCoords(Vector3D pos, float range)
        {
            _coords = new List<Vector3D>();
            if (range > 0)
            {
                int n = 22; //the number of points
                float radius = _orbitRange;
                Vector3D vector = GetOrbitAlignmentVector();
                float angle = (float)Math.PI * 2 / (float)n;

                Vector3D[] points = new Vector3D[n];
                //easily change how the orbits are setup by having a static angle for ships to orbit on
                Matrix rotation = Matrix.CreateRotationZ(angle);

                int closestCoord = 0;
                double closestDistance = Double.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    points[i] = vector + pos;
                    vector = Vector3D.TransformNormal(vector, rotation);
                    double distance = ((vector+pos) - _ship.GetPosition()).Length();
                    if (distance < closestDistance)
                    {
                        closestCoord = i;
                        closestDistance = distance;
                    }
                    _coords.Add(vector+pos);
                    
                }
                closestCoord++;
                _currentCoord = closestCoord<n?closestCoord:0;
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

        //vector pointing tword enemy
        public void EvasiveManeuvering(Vector3D enemysVector)
        {
            enemysVector.CalculatePerpendicularVector(out enemysVector);
            if (enemysVector.Normalize() > 10)
                ThrustTwordsDirection(enemysVector, false, true);
            else
                ThrustTwordsDirection(enemysVector, false, true);
        }

        public void DisableThrusterGyroOverrides()
        {
            foreach (var gyro in _gyros)
            {
                if ((gyro as IMyGyro).GyroOverride)
                    gyro.GetActionWithName("Override").Apply(gyro);
            }
            foreach (var thruster in _thrusters)
            {
                if ((thruster as MyThrust).ThrustOverride > 0)
                    thruster.SetValueFloat("Override", 0);
            }
        }

        private int _gridRadiusMultiplier = 1;

        private int _asteriodWeightedImportance = 2;
        public Vector3D GetWeightedCollisionAvoidanceVectorForNearbyStructures()
        {

            //overall needed ratios to avoid everything
            var nearbyGridsAvoidanceVector = AvoidNearbyGrids();

            //these ratios will have a higher weight because avoiding asteriods is more important than ships tbh
            var nearbyAsteriodAvoidanceVector = AvoidNearbyAsteriods() * _asteriodWeightedImportance;

            return nearbyAsteriodAvoidanceVector + 
                nearbyGridsAvoidanceVector;
        }


        public Vector3D AvoidNearbyGrids()
        {
            double detectionRange = 1000;
            var bs = new BoundingSphereD(_ship.GetPosition(), detectionRange);
            var entities = MyAPIGateway.Entities//get all entities within range
                .GetEntitiesInSphere(ref bs)
                .Where(x=> x != _ship)
                .OfType<IMyCubeGrid>()
                .OrderBy(x=> (x.GetPosition() - _ship.GetPosition()).Length())
                .Take(5);

            var generalizedAvoidanceVector = Vector3D.Zero;
            foreach (var entity in entities)
            {
                
                var ent = (entity as MyCubeGrid);
                var avoidanceVector = entity.GetPosition()-_ship.GetPosition();

                if (ent != null)
                {
                    float gridsize = ent.GridSizeR;
                    var minRange = (gridsize*_gridRadiusMultiplier);
                    minRange = minRange < 200 ? 200 : minRange;
                    gridsize = gridsize < 40 ? 40 : gridsize;
                    var distanceFromTarget = (entity.GetPosition() - _ship.GetPosition()).Length() - gridsize;

                    int vectorDistanceMultiplier = (int)(detectionRange - distanceFromTarget)/100;

                    if (distanceFromTarget < minRange)
                    {
                        Vector3D generalizedVelocity = avoidanceVector / Math.Abs(avoidanceVector.Max());
                        generalizedAvoidanceVector += (generalizedVelocity*vectorDistanceMultiplier);
                    }
                }
            }
            //Util.GetInstance().Notify(generalizedAvoidanceVector+"");
            return generalizedAvoidanceVector;
        }

        private int _minRangeToAsteriodSurface = 200;
        private double _asteriodDetectionRange = 1000;
        public Vector3D AvoidNearbyAsteriods()
        {
            var bs = new BoundingSphereD(_ship.GetPosition(), _asteriodDetectionRange);
            List<IMyVoxelBase> asteroids = new List<IMyVoxelBase>();
            MyAPIGateway.Session.VoxelMaps.GetInstances(asteroids);
            asteroids = asteroids
                .OrderBy(x => (x.GetPosition() - _ship.GetPosition()).Length())
                .Take(5).ToList();

            var generalizedAvoidanceVector = Vector3D.Zero;
            foreach (var entity in asteroids)
            {
                MyVoxelBase asteriod = entity as MyVoxelBase;
                if (asteriod != null)
                {
                    double radius = (asteriod.PositionLeftBottomCorner - entity.GetPosition()).Length();

                    var distanceFromSurface = (entity.GetPosition() - _ship.GetPosition()).Length()-radius;
                    var avoidanceVector = entity.GetPosition() - _ship.GetPosition();
                    int vectorDistanceMultiplier = (int)(_asteriodDetectionRange - distanceFromSurface) / 100;
                    if (distanceFromSurface < _minRangeToAsteriodSurface)
                    {
                        Vector3D generalizedVelocity = avoidanceVector / Math.Abs(avoidanceVector.Max());
                        generalizedAvoidanceVector += (generalizedVelocity* vectorDistanceMultiplier);
                    }
                }
            }
            //Util.GetInstance().Notify(generalizedAvoidanceVector+"");
            return generalizedAvoidanceVector;
        }

        const float collisionCheckDistance = 5000f;

        // This is the additional radius off the ships radius for collision checks
        const float checkRadius = 10000;

        // This is the "ships radius" for the calculation
        const float shipRadius = 1f;

        // Below this natural gravity value the script wont compute anything.  
        const double gravityStrengthThreshold = 0.01;

        public void Hover()
        {
            Util.GetInstance().Notify("CK 0");
            if (_remoteControl == null)
            {
                Util.GetInstance().Notify("CK 1");
                return;
            };
            var remote = _remoteControl as Sandbox.ModAPI.IMyRemoteControl;
            if (remote == null)
            {
                Util.GetInstance().Log("Drone missing control unit", _logPath);
                return;
            }
            
            Vector3D gravityVector = remote.GetNaturalGravity();
            double gravityStrength = gravityVector.Normalize();
            if (gravityStrength < gravityStrengthThreshold)
            {
                Util.GetInstance().Log("No suitable gravity field detected", _logPath);
                return;
            }
            Vector3D scanDirection = collisionCheckDistance * gravityVector;
            Vector3D endPoint = remote.GetFreeDestination(scanDirection, checkRadius, shipRadius);

            double distance = (endPoint - remote.GetPosition()).Length();

            Util.GetInstance().Log(string.Format("Distance to the ground: {0:0.00} m", distance), _logPath);
        }
    }
}






//foreach (var item in _nearbyAsteroids)
//                        {
//                            if (item != null)
//                            {
//                                var distance = Math.Abs((item.GetPosition() - Ship.GetPosition()).Length());
//var enemyBoundingBoxSize = (item.GetPosition() - item.LocalAABB.Max).Length();

//MaxSpeed = MaxSpeed > distance / ApproachSpeedMod? distance / ApproachSpeedMod : MaxSpeed;
//                                var detectRange = _avoidanceRange + enemyBoundingBoxSize;
//                                if (distance<detectRange)
//                                {
//                                    //avoidanceRot += NavInfo.CalculateRotation(item.GetPosition(), _shipControls as IMyEntity);
//                                    var temp = (Ship.GetPosition() - item.GetPosition());
//var rx = (detectRange - Math.Abs(temp.X)) * (temp.X / temp.X);
//var y = (detectRange - Math.Abs(temp.Y)) * (temp.Y / temp.Y);
//var z = (detectRange - Math.Abs(temp.Z)) * (temp.Z / temp.Z);

//double[] vals = new[] { rx, y, z };
//double max = vals.Max(x => Math.Abs(x));
//                                    if (max == 0)
//                                        max = vals.Average() != 0 ? vals.Average() : 1;

//                                    var SpeedPowerBoostBasedOnRange = distance / 100;
//temp = new Vector3D(rx, y, z) / max* SpeedPowerBoostBasedOnRange * 20;
//                                    Util.GetInstance().Log("[DroneNavigation.AvoidNearbyEntities] " + Ship.DisplayName + " avoiding asteroid -> Distance: " + (distance - detectRange), logPath);
//                                    Util.GetInstance().Log("^^avoiding power x:" + temp.X + " y:" + temp.Y + " z:" + temp.Z, logPath);
//avoidanceVectors.Add(temp);
//                                }
//                            }
//                        }