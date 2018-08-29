using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game.ModAPI;
using VRage.Library.Collections;
using VRage.ModAPI;
using VRageMath;
using IMyGyro = Sandbox.ModAPI.IMyGyro;
using IMyTerminalBlock = Sandbox.ModAPI.IMyTerminalBlock;

namespace SEMod
{
    class NavigationControls
    {
        private string _logPath = "NavigationControls";
        private IMyEntity _ship;
        private IMyCubeGrid _grid;
        private VRage.Game.ModAPI.Interfaces.IMyControllableEntity _remoteControl;
        private Sandbox.ModAPI.IMyGridTerminalSystem _gridTerminalSystem;
        private OrbitTypes orbitType = OrbitTypes.Default;


        //alignment
        private Base6Directions.Direction _shipUp = 0;
        private Base6Directions.Direction _shipLeft = 0;
        private Base6Directions.Direction _shipForward = 0;
        private double _degreesToVectorYaw = 0;
        private double _degreesToVectorPitch = 0;
        private float _alignSpeedMod = .075f;
        private float _rollSpeed = 2f;
        private List<string> _gyroYaw = new List<string>();
        private List<string> _gyroPitch = new List<string>();
        private List<string> _gyroRoll = new List<string>();
        private List<int> _gyroRollReverse = new List<int>();
        private List<int> _gyroYawReverse = new List<int>();
        private List<int> _gyroPitchReverse = new List<int>();
        private List<IMyTerminalBlock> _gyros = new List<IMyTerminalBlock>();
        private List<IMyTerminalBlock> _thrusters = new List<IMyTerminalBlock>();
        private List<Vector3D> _coords = new List<Vector3D>();
        private int _currentCoord;
        private int _previousCoord = 7;
        private int _orbitRange = 1000;
        public const int ApproachSpeedMod = 6;
        public double MaxSpeed = 50;
        private bool needsHydrogen = false;

        public NavigationControls(IMyEntity entity, VRage.Game.ModAPI.Interfaces.IMyControllableEntity cont)
        {
            _remoteControl = cont;
            _grid = entity as IMyCubeGrid;
            _ship = entity;

            if (_grid != null)
                _gridTerminalSystem = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(_grid);

            ShipOrientation();
            RefreshGyros();
            RefreshThrusters();
            Logger.Debug("Navigation Controls Online: " +IsOperational());

        }

        public int GetWorkingThrusterCount()
        {
            _thrusters = _thrusters.Where(x => x != null).ToList();

            _thrusters = _thrusters.Where(x => x.IsWorking).ToList();
            
            return _thrusters.Count;
        }

        public int GetWorkingGyroCount()
        {
            _gyros = _gyros.Where(x => x != null).ToList();

            _gyros = _gyros.Where(x => x.IsWorking).ToList();
            return _gyros.Count;
        }

        public bool IsOperational()
        {
            int numGyros = GetWorkingGyroCount();
            int numThrusters = GetWorkingThrusterCount();
            int numThrustDirections = GetNumberOfValidThrusterDirections();

            bool hasSufficientGyros = _gyros.Count > 0 && (_gyros.Count == _gyroYaw.Count) &&  (_gyros.Count == _gyroPitch.Count);

            bool operational = (numGyros > 0 && numThrusters > 0) && numThrustDirections >= 2;
            Logger.Debug("Navigation is Operational: " + operational+" gyros:thrusters => "+numGyros+":"+numThrusters);

            var atleasthalfWorking = _thrusters.Count(x => ((MyThrust) x).IsPowered) >= numThrusters/2;


            return operational && atleasthalfWorking && hasSufficientGyros;
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
                            if(_degreesToVectorYaw > 0)
                                gyro.SetValueFloat(_gyroYaw[i], (float)(_degreesToVectorYaw * _alignSpeedMod) * (_gyroYawReverse[i]));
                            if (_degreesToVectorYaw < 0)
                                gyro.SetValueFloat(_gyroYaw[i], (float)(_degreesToVectorYaw * _alignSpeedMod) * (_gyroYawReverse[i]));
                        }
                        else
                        {
                            gyro.SetValueFloat(_gyroYaw[i], 0);
                        }


                        if (Math.Abs(_degreesToVectorPitch) > precision)
                        {
                            gyro.SetValueFloat(_gyroPitch[i], (float)(_degreesToVectorPitch * _alignSpeedMod) * (_gyroPitchReverse[i]));
                        }
                        else
                        {
                            gyro.SetValueFloat(_gyroPitch[i], 0);
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.LogException(e);
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
                _shipForward = Base6Directions.GetDirection(forwardVector);
            }
        }


        private void RefreshGyros()
        {
            _gyros.Clear();
            _gyroYaw.Clear();
            _gyroPitch.Clear();
            _gyroYawReverse.Clear();
            _gyroPitchReverse.Clear();
            _gridTerminalSystem.GetBlocksOfType<MyGyro>(_gyros);
            for (int i = 0; i < _gyros.Count; i++)
            {
                if ((_gyros[i]).IsFunctional)
                {
                    Base6Directions.Direction gyroUp = _gyros[i].Orientation.TransformDirectionInverse(_shipUp);
                    Base6Directions.Direction gyroLeft = _gyros[i].Orientation.TransformDirectionInverse(_shipLeft);
                    Base6Directions.Direction gyroForward = _gyros[i].Orientation.TransformDirectionInverse(_shipForward);

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

                    if (gyroForward == Base6Directions.Direction.Up)
                    {
                        _gyroRoll.Add("Yaw");
                        _gyroRollReverse.Add(1);
                    }
                    else if (gyroForward == Base6Directions.Direction.Down)
                    {
                        _gyroRoll.Add("Yaw");
                        _gyroRollReverse.Add(-1);
                    }
                    else if (gyroForward == Base6Directions.Direction.Left)
                    {
                        _gyroRoll.Add("Pitch");
                        _gyroRollReverse.Add(1);
                    }
                    else if (gyroForward == Base6Directions.Direction.Right)
                    {
                        _gyroRoll.Add("Pitch");
                        _gyroRollReverse.Add(-1);
                    }
                    else if (gyroForward == Base6Directions.Direction.Forward)
                    {
                        _gyroRoll.Add("Roll");
                        _gyroRollReverse.Add(-1);
                    }
                    else if (gyroForward == Base6Directions.Direction.Backward)
                    {
                        _gyroRoll.Add("Roll");
                        _gyroRollReverse.Add(1);
                    }

                }
            }
        }

        private int GetNumberOfValidThrusterDirections()
        {
            int up = 0;
            int down = 0;
            int left = 0;
            int right = 0;
            int forward = 0;
            int backward = 0;

            for (int i = 0; i < _thrusters.Count(x=>x.IsWorking); i++)
            {
                if ((_thrusters[i]).IsFunctional)
                {
                    Base6Directions.Direction thrusterForward = _thrusters[i].Orientation.TransformDirectionInverse(_shipForward);

                    if (thrusterForward == Base6Directions.Direction.Up)
                    {
                        up++;
                    }
                    else if (thrusterForward == Base6Directions.Direction.Down)
                    {
                        down++;
                    }
                    else if (thrusterForward == Base6Directions.Direction.Left)
                    {
                        left++;
                    }
                    else if (thrusterForward == Base6Directions.Direction.Right)
                    {
                        right++;
                    }
                    else if (thrusterForward == Base6Directions.Direction.Forward)
                    {
                        forward++;
                    }
                    else if (thrusterForward == Base6Directions.Direction.Backward)
                    {
                        backward++;
                    }
                }
            }
            int sum = (up > 0 ? 1 : 0)
                + (down > 0 ? 1 : 0)
                + (left > 0 ? 1 : 0)
                + (right > 0 ? 1 : 0)
                + (forward > 0 ? 1 : 0)
                + (backward > 0 ? 1 : 0)
                ;

           // Util.NotifyHud(sum + " dirs ");// +up+"+ up "+down+ "+ down" + left+ "+ left" + right+ "+ right" + forward+""+backward+ "+ forward");
            return sum;



        }

        private void RefreshThrusters()
        {
            
            _thrusters.Clear();
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

        //public void WeightedThrustTwordsDirection(Vector3D thrustVector, bool secondTry = false, bool usingHalfPower = false)
        //{

        //    RefreshThrusters();
        //    int fullpower = usingHalfPower ? 75 : 150;
        //    int lowpower = 50;


        //    //generalize the thruster direction to 1,0,-1
        //    int xPos = thrustVector.X > 0 ? 1 : thrustVector.X < 0 ? -1 : 0;
        //    int yPos = thrustVector.Y > 0 ? 1 : thrustVector.Y < 0 ? -1 : 0;
        //    int zPos = thrustVector.Z > 0 ? 1 : thrustVector.Z < 0 ? -1 : 0;
        //    var desiredVector = new Vector3D(xPos, yPos, zPos);

        //    int xv = _ship.Physics.LinearVelocity.X > 0 ? 1 : _ship.Physics.LinearVelocity.X < 0 ? -1 : 0;
        //    int yv = _ship.Physics.LinearVelocity.Y > 0 ? 1 : _ship.Physics.LinearVelocity.Y < 0 ? -1 : 0;
        //    int zv = _ship.Physics.LinearVelocity.Z > 0 ? 1 : _ship.Physics.LinearVelocity.Z < 0 ? -1 : 0;

        //    //if vector does not match, get the non matching vectors so we can match some thrusters against the new vector ("counterDriftVector" that stops drift, but does not go against the desired thruster direction)
        //    int xm = xPos == xv ? 0 : xv;
        //    int ym = yPos == yv ? 0 : yv;
        //    int zm = zPos == zv ? 0 : zv;
        //    var counterDriftVector = new Vector3D(xm, ym, zm);

        //    bool successfullyMoved = false;
        //    foreach (var thruster in _thrusters)
        //    {
        //        Vector3D fow = thruster.WorldMatrix.Forward;
        //        int xt = fow.X > 0 ? 1 : fow.X < 0 ? -1 : 0;
        //        int yt = fow.Y > 0 ? 1 : fow.Y < 0 ? -1 : 0;
        //        int zt = fow.Z > 0 ? 1 : fow.Z < 0 ? -1 : 0;
        //        var thrusterVector = new Vector3D(xt, yt, zt);

        //        bool notDrifting = counterDriftVector == Vector3D.Zero;
        //        bool thrusterPointsDesiredDirection = thrusterVector.Equals(desiredVector);
        //        bool thrusterCountersDrift = thrusterVector.Equals(counterDriftVector) && !notDrifting;
        //        bool thrusterNeedsPower = thrusterCountersDrift || thrusterPointsDesiredDirection;

        //        double angle = AngleBetween(thrustVector, desiredVector, true);
        //        //Logger.Debug("Angle " + angle);
        //        if (angle > 80)
        //        {
        //            int power = thrusterPointsDesiredDirection ? fullpower : lowpower;
        //            if (notDrifting)
        //                power = fullpower;

        //            thruster.GetActionWithName("OnOff_On").Apply(thruster);
        //            thruster.SetValueFloat("Override", power);
        //            successfullyMoved = true;
        //        }
        //        //just attempt to move the ship
        //        else if (secondTry && angle > 89)
        //        {
        //            thruster.GetActionWithName("OnOff_On").Apply(thruster);
        //            thruster.SetValueFloat("Override", fullpower);
        //            successfullyMoved = true;
        //        }
        //        else
        //        {
        //            thruster.SetValueFloat("Override", 0);
        //        }
        //    }
        //    if (!successfullyMoved && !secondTry)
        //        ThrustTwordsDirection(thrustVector, true, usingHalfPower);
        //}

        public bool ThrustTwordsDirection(Vector3D thrustVector, bool secondTry = false, bool usingHalfPower = false)
        {
            RefreshThrusters();
            if (secondTry)
            {
                //Roll();
                StopSpin();
            }
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
            int xm = xPos == xv ? 0 : -xv;
            int ym = yPos == yv ? 0 : -yv;
            int zm = zPos == zv ? 0 : -zv;
            var counterDriftVector = new Vector3D(xm, ym, zm);

            bool successfullyMoved = false;
            int numThrustersActivated = 0;
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
                //Logger.Debug("Angle " + angle);
                if (thrusterNeedsPower)
                {
                    int power = thrusterPointsDesiredDirection ? fullpower : lowpower;
                    if (notDrifting)
                        power = fullpower;

                    thruster.GetActionWithName("OnOff_On").Apply(thruster);
                    thruster.SetValueFloat("Override", power);
                    successfullyMoved = true;
                    numThrustersActivated++;
                }
                //just attempt to move the ship
                else if (secondTry && angle < 91)
                {
                    thruster.GetActionWithName("OnOff_On").Apply(thruster);
                    thruster.SetValueFloat("Override", fullpower);
                    successfullyMoved = true;
                    numThrustersActivated++;
                }
                else
                {
                    thruster.SetValueFloat("Override", 0);
                }
            }
            
            if (!successfullyMoved && !secondTry)
                successfullyMoved = ThrustTwordsDirection(thrustVector, true, usingHalfPower);

            if (numThrustersActivated<2)
                Roll();
            else
                StopRoll();

            return successfullyMoved;
        }

        public void Roll()
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

                    gyro.SetValueFloat(_gyroRoll[i], (_rollSpeed) * (_gyroRollReverse[i]));

                }
                catch (Exception e)
                {
                    Logger.LogException(e);
                    //Util.Notify(e.ToString());
                    //This is only to catch the occasional situation where the ship tried to align to something but has between the time the method started and now has lost a gyro or whatever
                }
            }
        }
        public void StopRoll()
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

                    gyro.SetValueFloat(_gyroRoll[i], 0);

                }
                catch (Exception e)
                {
                    Logger.LogException(e);
                    //Util.Notify(e.ToString());
                    //This is only to catch the occasional situation where the ship tried to align to something but has between the time the method started and now has lost a gyro or whatever
                }
            }
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

        public void OrbitAtRange(Vector3D target, int range)
        {
            if (_remoteControl != null)
            {
                ResetOrbitCoords(target, range);
                var dir = _ship.GetPosition() - _coords[_currentCoord];
                double distanceFromPlayer = (target - _ship.GetPosition()).Length();
                var orbitPoint = _coords[_currentCoord];
                //
                // HighlightDestinationVector(orbitPoint);
                // Util.Log(_logPath, "distance " + distanceFromPlayer);

                if (_ship.Physics.LinearVelocity.Normalize() > MaxSpeed)
                {
                    SlowDown();
                }
                else
                {

                    ThrustTwordsDirection(dir);
                }
                AlignTo(orbitPoint);

            }
        }

        public void Orbit(Vector3D target)
        {
            if (_remoteControl != null)
            {
                ResetOrbitCoords(target, _orbitRange);
                var dir = _ship.GetPosition() - _coords[_currentCoord];
                double distanceFromPlayer = (target - _ship.GetPosition()).Length();
                MaxSpeed = 40;
                var orbitPoint = _coords[_currentCoord];
                //
               // HighlightDestinationVector(orbitPoint);
               // Util.Log(_logPath, "distance " + distanceFromPlayer);

                if (_ship.Physics.LinearVelocity.Normalize() > MaxSpeed)
                {
                    SlowDown();
                }
                else
                {
                    
                    ThrustTwordsDirection(dir);
                }
                AlignTo(orbitPoint);

            }
        }

        private void HighlightDestinationVector(Vector3D location)
        {

            IMyGps gpsx = MyAPIGateway.Session.GPS.Create("Destination", "drone destination", location + new Vector3D(10, 0, 0), true);
            IMyGps gpsy = MyAPIGateway.Session.GPS.Create("Destination", "drone destination", location + new Vector3D(0, 10, 0), true);
            IMyGps gpsz = MyAPIGateway.Session.GPS.Create("Destination", "drone destination", location + new Vector3D(0, 0, 10), true);

            MyAPIGateway.Session.GPS.AddLocalGps(gpsx);
            MyAPIGateway.Session.GPS.AddLocalGps(gpsy);
            MyAPIGateway.Session.GPS.AddLocalGps(gpsz);
        }

        private int _combatRange = 50;

        public bool CombatOrbit(Vector3D target)
        {
            if (_remoteControl != null)
            {
                ResetOrbitCoords(target, _combatRange);

                var dir = _ship.GetPosition() - _coords[_currentCoord];
                
                MaxSpeed = 40;
                HighlightDestinationVector(_coords[_currentCoord]);

                if (_ship.Physics.LinearVelocity.Normalize() > MaxSpeed)
                {
                    Logger.Debug("slowing ");
                    SlowDown();
                }
                else
                {
                    ThrustTwordsDirection(dir);
                }
                AlignTo(_coords[_currentCoord]);

            }
            return false;
        }
        private static Random r = new Random();

        private Vector3D GetOrbitAlignmentVector(int orbitrange)
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
                    vector = new Vector3D(orbitrange * 1, orbitrange * 0, orbitrange * 0);
                    break;
                case OrbitTypes.XY:
                    vector = new Vector3D(orbitrange * 1, orbitrange * 1, orbitrange * 0);
                    break;
                case OrbitTypes.Y:
                    vector = new Vector3D(orbitrange * 0, orbitrange * 1, orbitrange * 0);
                    break;
                case OrbitTypes.YZ:
                    vector = new Vector3D(orbitrange * 0, orbitrange * 1, orbitrange * 1);
                    break;
                case OrbitTypes.Z:
                    vector = new Vector3D(orbitrange * 0, orbitrange * 0, orbitrange * 1);
                    break;
                case OrbitTypes.XZ:
                    vector = new Vector3D(orbitrange * 1, orbitrange * 0, orbitrange * 1);
                    break;
            }

            return vector;

        }

        public void ResetOrbitCoords(Vector3D pos, int range)
        {
            _coords = new List<Vector3D>();
            int n = 22; //the number of points
            float radius = _orbitRange;
            Vector3D vector = GetOrbitAlignmentVector(range);
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
                double distance = ((vector + pos) - _ship.GetPosition()).Length();
                if (distance < closestDistance)
                {
                    closestCoord = i;
                    closestDistance = distance;
                }
                _coords.Add(vector + pos);

            }
            closestCoord++;
            _currentCoord = closestCoord < n ? closestCoord : 0;
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
                .Where(x => x != _ship)
                .OfType<IMyCubeGrid>()
                .OrderBy(x => (x.GetPosition() - _ship.GetPosition()).Length())
                .Take(5);

            var generalizedAvoidanceVector = Vector3D.Zero;
            foreach (var entity in entities)
            {

                var ent = (entity as MyCubeGrid);
                var avoidanceVector = entity.GetPosition() - _ship.GetPosition();

                if (ent != null)
                {
                    float gridsize = ent.GridSizeR;
                    var minRange = (gridsize * _gridRadiusMultiplier);
                    minRange = minRange < 200 ? 200 : minRange;
                    gridsize = gridsize < 40 ? 40 : gridsize;
                    var distanceFromTarget = (entity.GetPosition() - _ship.GetPosition()).Length() - gridsize;

                    int vectorDistanceMultiplier = (int)(detectionRange - distanceFromTarget) / 100;

                    if (distanceFromTarget < minRange)
                    {
                        Vector3D generalizedVelocity = avoidanceVector / Math.Abs(avoidanceVector.Max());
                        generalizedAvoidanceVector += (generalizedVelocity * vectorDistanceMultiplier);
                    }
                }
            }
            //Util.GetInstance().Notify(generalizedAvoidanceVector+"");
            return generalizedAvoidanceVector;
        }

        internal void CombatApproach(Vector3D vector3D)
        {
            var distance = (_grid.GetPosition() - vector3D).Length();

            var maxSpeed = Math.Sqrt(distance)*2;

            if(_grid.Physics.LinearVelocity.Normalize() <= maxSpeed)
                ThrustTwordsDirection(_grid.GetPosition() - vector3D);
            else
                SlowDown();

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

                    var distanceFromSurface = (entity.GetPosition() - _ship.GetPosition()).Length() - radius;
                    var avoidanceVector = entity.GetPosition() - _ship.GetPosition();
                    int vectorDistanceMultiplier = (int)(_asteriodDetectionRange - distanceFromSurface) / 100;
                    if (distanceFromSurface < _minRangeToAsteriodSurface)
                    {
                        Vector3D generalizedVelocity = avoidanceVector / Math.Abs(avoidanceVector.Max());
                        generalizedAvoidanceVector += (generalizedVelocity * vectorDistanceMultiplier);
                    }
                }
            }
            //Util.GetInstance().Notify(generalizedAvoidanceVector+"");
            return generalizedAvoidanceVector;
        }


    }
}
