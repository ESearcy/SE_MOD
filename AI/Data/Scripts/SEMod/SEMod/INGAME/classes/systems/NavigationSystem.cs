using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using VRage.Game;
using VRage.Game.ModAPI.Ingame;
using VRageMath;
using SpaceEngineers.Game.ModAPI.Ingame;

namespace SEMod.INGAME.classes.systems
{
    //////
    public class NavigationSystem
    {
        private IMyCubeGrid _grid;
        private IMyRemoteControl _remoteControl;
        private IMyShipConnector _shipConnector;
        public Vector3D LinearVelocity = new Vector3D(0, 0, 0);
        DateTime lastSpeedCheck = DateTime.Now;
        Vector3D lastPosition = new Vector3D(0, 0, 0);
        bool docking = false;

        //alignment 
        private Base6Directions.Direction _shipUp = 0;
        private Base6Directions.Direction _shipLeft = 0;
        private Base6Directions.Direction _shipForward = 0;
        private Base6Directions.Direction _shipDown = 0;
        private Base6Directions.Direction _shipRight = 0;
        private Base6Directions.Direction _shipBackward = 0;
        public double _degreesToVectorYaw = 0;
        public double _degreesToVectorPitch = 0;
        private float _alignSpeedMod = .02f;
        private float _rollSpeed = 2f;
        private List<GyroOverride> _gyroOverrides = new List<GyroOverride>();
        Logger log;
        bool station = false;

        public double GetSpeed()
        {
            if (_remoteControl != null)
            {
                //LOG.Debug("Speed "+ _remoteControl.GetShipSpeed());
                return _remoteControl.GetShipSpeed();
            }
            else return 0;
        }
        double lastUpdateTime = 0;
        double lastUpdatedistance = 0;

        public void AlignAgainstGravity()
        {
            var align = _remoteControl.GetPosition()+ (_remoteControl.GetNaturalGravity()*100);

            //double altitude = _remoteControl.GetValue<float>("Altitude");
            //LOG.Debug("Plantary Alignment Vector: "+ altitude);
            AlignTo(align);
        }

        public void AlignAcrossGravity()
        {
            var align = _remoteControl.GetPosition().Cross(_remoteControl.GetNaturalGravity() * 100);

            //double altitude = _remoteControl.GetValue<float>("Altitude");
            //LOG.Debug("Plantary Alignment Vector: "+ altitude);
            AlignTo(align);
        }

        public void UpdateSpeed()
        {
            LinearVelocity = _remoteControl.GetShipVelocities().LinearVelocity;
        }

        public void Update()
        {
            UpdateSpeed();
            SetShipOrientation();

            //if (docked && !station)
            //{
            //    EnableDockedMode();
            //    return;
            //}
            //else
            //    EnableFlightMode();
        }

        public void EnableDockedMode()
        {
            foreach (var thruster in components.Thrusters)
            {
                thruster.SetValueFloat("Override", 0);
                thruster.GetActionWithName("OnOff_Off").Apply(thruster);
            }
            foreach (var gyro in components.Gyros)
            {
                gyro.GetActionWithName("OnOff_Off").Apply(gyro);
            }
        }

        public void EnableFlightMode()
        {
            foreach (var thruster in components.Thrusters)
            {
                thruster.GetActionWithName("OnOff_On").Apply(thruster);
            }
            foreach (var gyro in components.Gyros)
            {
                gyro.GetActionWithName("OnOff_On").Apply(gyro);
            }
        }

        internal void AlignUpWithGravity()
        {
            Vector3D up = -_remoteControl.GetNaturalGravity();
            AlignUp(_grid.GetPosition()+(up*100));
        }

        ShipComponents components;
        public NavigationSystem(Logger LOG, IMyCubeGrid entity, ShipComponents components, bool station = false)
        {
            this.station = station;
            this.log = LOG;
            this.components = components;
            _remoteControl = this.components.ControlUnits.FirstOrDefault();
            _grid = entity;
            
            SetShipOrientation();
        }

        public void SetShipOrientation()
        {
            if (_remoteControl != null)
            {
                _shipUp = _remoteControl.Orientation.Up;
                _shipLeft = _remoteControl.Orientation.Left;
                _shipForward = _remoteControl.Orientation.Forward;
            }
        }

        private IMyShipConnector _dockngConnector = null;

        public double AlignTo(Vector3D position, Vector3D directionUp)
        {
            var alignTo = AlignTo(position);
            double alignUp = 0;
            if (alignTo < 1)
                alignUp = AlignUp(directionUp);

            return alignTo + alignUp;
        }

        public double AlignTo(Vector3D position)
        {
            TurnOffGyros(true);
            DegreesToVector(position);
            PointToVector(0.00);
            var angoff = (_degreesToVectorPitch + _degreesToVectorYaw);
            //LOG.Debug(angoff + " AlignUp Angle"); 
            return Math.Abs(angoff);
        }

        public double AlignToWobble(Vector3D position)
        {
            TurnOffGyros(true);
            DegreesToVector(position);
            if (_degreesToVectorPitch > 0)
                _degreesToVectorPitch += .001;
            else
                _degreesToVectorPitch += -.001;

            if (_degreesToVectorYaw > 0)
                _degreesToVectorYaw += .001;
            else
                _degreesToVectorYaw += -.001;

            PointToVector(0.00);
            var angoff = (_degreesToVectorPitch + _degreesToVectorYaw);
            //LOG.Debug(angoff + " AlignUp Angle"); 
            return Math.Abs(angoff);
        }

        internal void MaintainAltitude(double altitude, double minAltitude)
        {
            var gravityDir = _remoteControl.GetNaturalGravity();
            gravityDir.Normalize();
            var HoverLocation = _remoteControl.GetPosition() -gravityDir *(minAltitude - altitude);
            Vector3D direction = _remoteControl.GetPosition() - HoverLocation;
            var difference =  minAltitude - altitude;
            var distance = direction.Length();
            if (distance > GetSpeed())
                ThrustInDirection(direction, distance);
            else
                SlowDown();
        }

        double lastUpAngle = 0;
        float RollSetting = .15f;
        int thrusterMaxPower = 12;
        public double AlignUp(Vector3D position)
        {
            var currentAlign = _remoteControl.WorldMatrix.Up;

            var anglebetween = AngleBetween(currentAlign, position, true);
            var anglefromleft = AngleBetween((docking ? (IMyTerminalBlock)_shipConnector : (IMyTerminalBlock)_remoteControl).WorldMatrix.Left, position, true);
            var anglefromright = AngleBetween((docking ? (IMyTerminalBlock)_shipConnector : (IMyTerminalBlock)_remoteControl).WorldMatrix.Right, position, true);
            TurnOffGyros(true);

            Roll(anglefromleft < anglefromright ? -RollSetting : anglefromleft < anglefromright ? RollSetting : 0);
            return anglebetween;
        }

        internal bool HoverApproach(Vector3D vector3D, double speed = 1)
        {
            _remoteControl.DampenersOverride = true;
            var successful = false;

            var dirToTarget = _remoteControl.GetPosition() - vector3D;
            var dist = dirToTarget.Length();
            dirToTarget.Normalize();

            if (GetSpeed() > speed ||  GetSpeed() > dist)
            {
                SlowDown();
            }
            else
            {
                successful =  ThrustInDirection(dirToTarget, dist);
            }
            return successful;
        }

        internal bool DockApproach(Vector3D droneConnector, Vector3D vector3D, double speed = 1)
        {
            _remoteControl.DampenersOverride = true;
            var successful = false;

            var dirToTarget = droneConnector - vector3D;
            var dist = dirToTarget.Length();
            dirToTarget.Normalize();
            if(dist<1)dist=1;
            if (GetSpeed() > speed)
            {
                SlowDown();
            }
            else
            {
                successful = ThrustInDirection(dirToTarget, dist);
            }
            return successful;
        }

        internal Vector3D GetGravityDirection()
        {
            return _remoteControl.GetNaturalGravity();
        }

        internal void CombatApproach(Vector3D vector3D,int Mass)
        {
            //var distance = (_grid.GetPosition() - vector3D).Length();

            //var maxSpeed = Math.Sqrt(distance) * 2;

            //if (distance > 300)
            //    ThrustTwordsDirection(_grid.GetPosition() - vector3D,Mass, distance);
            //else if (distance < 100)
            //    ThrustTwordsDirection(vector3D - _grid.GetPosition(),Mass, distance);
            //else
            //    SlowDown();

        }

        private void TurnOffGyros(bool off)
        {
            for (int i = 0; i < components.Gyros.Count; i++)
            {
                if ((components.Gyros[i]).GyroOverride != off)
                {
                    TerminalBlockExtentions.ApplyAction(components.Gyros[i], "Override");
                }
            }
        }

        private void DegreesToVector(Vector3D TV)
        {
            IMyTerminalBlock guideblock = docking ? (IMyTerminalBlock)_dockngConnector : (IMyTerminalBlock)_remoteControl;

            if (guideblock != null)
            {
                var Origin = guideblock.GetPosition();
                var Up = guideblock.WorldMatrix.Up;
                var Forward = guideblock.WorldMatrix.Forward;
                var Right = guideblock.WorldMatrix.Right;
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
            foreach (var gyro in _gyroOverrides)
            {
                try
                {
                    gyro.TurnOn();

                    if (Math.Abs(_degreesToVectorYaw) > precision)
                    {
                        gyro.OverrideYaw((float)_degreesToVectorYaw * _alignSpeedMod);
                    }
                    else
                    {
                        gyro.OverrideYaw(0);
                    }


                    if (Math.Abs(_degreesToVectorPitch) > precision)
                    {
                        gyro.OverridePitch((float)_degreesToVectorPitch * _alignSpeedMod);
                    }
                    else
                    {
                        gyro.OverridePitch(0);
                    }
                }
                catch (Exception e)
                {
                    log.Error(e.ToString());
                }
            }
        }

        private void ResetGyros()
        {
            _gyroOverrides.Clear();
            foreach (var gyro in components.Gyros)
            {
                _gyroOverrides.Add(new GyroOverride(gyro, _shipForward, _shipUp, _shipLeft, _shipDown, _shipRight, _shipBackward, log));
            }
        }

        public bool IsOperational()
        {
            int numGyros = GetWorkingGyroCount();
            int numThrusters = GetWorkingThrusterCount();
            int numThrustDirections = GetNumberOfValidThrusterDirections();

            bool hasSufficientGyros = components.Gyros.Count > 0 && _gyroOverrides.Count > 0;

            bool operational = (numGyros > 0 && numThrusters > 0);
            //LOG.Debug("Navigation is Operational: " + operational + " gyros:thrusters => " + numGyros + ":" + numThrusters);

            var atleasthalfWorking = numThrusters >= components.Thrusters.Count() / 2;


            return operational && atleasthalfWorking && hasSufficientGyros;
        }

        public int GetWorkingThrusterCount()
        {
            return components.Thrusters.Count;
        }

        int lastGyroCount = 0;
        public int GetWorkingGyroCount()
        {
            if (lastGyroCount != components.Gyros.Count)
            {
                ResetGyros();
                lastGyroCount = components.Gyros.Count;
            }
            return components.Gyros.Count;
        }

        private int GetNumberOfValidThrusterDirections()
        {
            int up = 0;
            int down = 0;
            int left = 0;
            int right = 0;
            int forward = 0;
            int backward = 0;

            for (int i = 0; i < components.Thrusters.Count(x => x.IsWorking); i++)
            {

                Base6Directions.Direction thrusterForward = components.Thrusters[i].Orientation.TransformDirectionInverse(_shipForward);

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

        public void Roll(float angle)
        {
            foreach (var gyro in _gyroOverrides)
            {
                try
                {
                    gyro.EnableOverride();
                    gyro.OverrideRoll(angle); 
                    //(_gyroRoll[i], (_rollSpeed) * (_gyroRollReverse[i]), gyro);
                }
                catch (Exception e)
                {
                    log.Error(e.ToString());
                    //Util.Notify(e.ToString()); 
                    //This is only to catch the occasional situation where the ship tried to align to something but has between the time the method started and now has lost a gyro or whatever 
                }
            }
        }

        public void StopRoll()
        {
            foreach (var gyro in _gyroOverrides)
            {
                try
                {

                    gyro.DisableOverride();

                    gyro.OverrideRoll(0);// GyroSetFloatValue(_gyroRoll[i], 0, gyro); 

                }
                catch (Exception e)
                {
                    log.Error(e.ToString());
                    //Util.Notify(e.ToString()); 
                    //This is only to catch the occasional situation where the ship tried to align to something but has between the time the method started and now has lost a gyro or whatever 
                }
            }
        }

        public void StopSpin()
        {
            TurnOffGyros(false);
        }

        public void SlowDown()
        {
            foreach (var thru in components.Thrusters)
            {
                if (thru.ThrustOverride > 0)
                    thru.ThrustOverride = 0;
            }

            _remoteControl.DampenersOverride = true;
            _remoteControl.IsMainCockpit = true;
        }

        public bool ThrustInDirection(Vector3D desiredVector, double distance)
        {
            var thrusted = false;
            //antidrift handled by Dampeners
            if (!_remoteControl.DampenersOverride)
                _remoteControl.DampenersOverride = true;

            foreach(var thruster in components.Thrusters)
            {
                //get current thrust (Dampeners)
                var currentThrust = thruster.CurrentThrust;
                var maxPossibleThrust = thruster.MaxEffectiveThrust;
                //get desired additional thrust based on distance. 104 is max speed usually
                var thrustPercent = 1d;
                if (distance < 1000) thrustPercent = Math.Sqrt(distance)/30;
                log.Debug(maxPossibleThrust * thrustPercent+" power applied");
                var desiredThrust = (currentThrust) + (maxPossibleThrust*thrustPercent);
                if(desiredThrust>maxPossibleThrust)desiredThrust=maxPossibleThrust;

                //apply the desired thrust to whichever thrusters need it.
                thruster.GetActionWithName("OnOff_On").Apply(thruster);

                var thrusterVector = thruster.WorldMatrix.Forward;
                double angle = Math.Abs(AngleBetween(thrusterVector, desiredVector, true));
                

                if (angle <= 45)
                {
                    //account for gravity
                    var gravity = GetGravityDirection();
                    var gravityAngle = Math.Abs(AngleBetween(-gravity,thrusterVector,true));
                    if(gravityAngle>80)
                        thruster.SetValueFloat("Override", (float)(desiredThrust));
                    else
                    {
                        var maxPower = (90 - gravityAngle) / (90 * 10000);
                        thruster.SetValueFloat("Override", (float)(1));
                    }

                    thrusted = true;
                }

            }
            return thrusted;
        }

        public static double AngleBetween(Vector3D u, Vector3D v, bool returndegrees)
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

        private class GyroOverride
        {
            Base6Directions.Direction up = Base6Directions.Direction.Up;
            Base6Directions.Direction down = Base6Directions.Direction.Down;
            Base6Directions.Direction left = Base6Directions.Direction.Left;
            Base6Directions.Direction right = Base6Directions.Direction.Right;
            Base6Directions.Direction forward = Base6Directions.Direction.Forward;
            Base6Directions.Direction backward = Base6Directions.Direction.Backward;

            public IMyGyro gyro;
            String Pitch = "Pitch";
            String Yaw = "Yaw";
            String Roll = "Roll";
            int pitchdir = 1;
            int yawdir = 1;
            int rolldir = 1;
            Logger LOG;

            public void DisableOverride()
            {
                if (gyro.GyroOverride)
                {
                    gyro.GetActionWithName("Override").Apply(gyro);
                }
            }

            public void EnableOverride()
            {
                if (!gyro.GyroOverride)
                {
                    gyro.GetActionWithName("Override").Apply(gyro);
                }
            }

            public void TurnOff()
            {
                gyro.GetActionWithName("OnOff_Off").Apply(gyro);
            }

            public void TurnOn()
            {
                gyro.GetActionWithName("OnOff_On").Apply(gyro);
            }

            public void OverridePitch(float value)
            {
                GyroSetFloatValue(Pitch, pitchdir * value);
            }

            public void OverrideRoll(float value)
            {
                GyroSetFloatValue(Roll, rolldir * value);
            }

            public void OverrideYaw(float value)
            {
                GyroSetFloatValue(Yaw, yawdir * value);
            }

            private void GyroSetFloatValue(String dir, float value)
            {
                if (dir == "Yaw")
                {
                    gyro.Yaw = value;
                }
                else if (dir == "Pitch")
                {
                    gyro.Pitch = value;
                }
                else if (dir == "Roll")
                {
                    gyro.Roll = value;
                }
            }

            public GyroOverride(IMyGyro _gyro, Base6Directions.Direction _shipForward, Base6Directions.Direction _shipUp, Base6Directions.Direction _shipLeft, Base6Directions.Direction _shipDown, Base6Directions.Direction _shipRight, Base6Directions.Direction _shipBackward, Logger logger)
            {
                LOG = logger;
                Base6Directions.Direction gyroup = _gyro.Orientation.TransformDirectionInverse(_shipUp);
                Base6Directions.Direction gyroleft = _gyro.Orientation.TransformDirectionInverse(_shipLeft);
                Base6Directions.Direction gyroforward = _gyro.Orientation.TransformDirectionInverse(_shipForward);

                this.gyro = _gyro;
                if (gyroup == up)
                {
                    if (gyroforward == left)
                    {
                        //LOG.DebugStay(gyroup + ":" + gyroforward + " up:left"); 
                        Pitch = "Roll"; rolldir = 1;
                        Roll = "Pitch"; pitchdir = 1;
                        Yaw = "Yaw"; yawdir = 1;
                    }
                    if (gyroforward == right)
                    {
                        //LOG.DebugStay(gyroup + ":" + gyroforward + " up:right"); 
                        Pitch = "Roll"; rolldir = 1;
                        Roll = "Pitch"; pitchdir = -1;
                        Yaw = "Yaw"; yawdir = 1;
                    }
                    if (gyroforward == forward)
                    {
                        // LOG.DebugStay(gyroup + ":" + gyroforward + " up:forward"); 
                        Pitch = "Pitch"; pitchdir = -1;
                        Roll = "Roll"; rolldir = 1;
                        Yaw = "Yaw"; yawdir = 1;
                    }
                    if (gyroforward == backward)
                    {
                        // LOG.DebugStay(gyroup + ":" + gyroforward + " up:back"); 
                        Pitch = "Pitch"; pitchdir = 1;
                        Roll = "Roll"; rolldir = -1;
                        Yaw = "Yaw"; yawdir = 1;
                    }
                }
                else if (gyroup == down)
                {
                    if (gyroforward == left)
                    {
                        //LOG.DebugStay(gyroup + ":" + gyroforward + " down:left"); 
                        Pitch = "Roll"; rolldir = 1;
                        Roll = "Pitch"; pitchdir = -1;
                        Yaw = "Yaw"; yawdir = -1;
                    }
                    if (gyroforward == right)
                    {
                        //LOG.DebugStay(gyroup + ":" + gyroforward + " down:right"); 
                        Pitch = "Roll"; rolldir = -1;
                        Roll = "Pitch"; pitchdir = 1;
                        Yaw = "Yaw"; yawdir = -1;
                    }
                    if (gyroforward == forward)
                    {
                        //LOG.DebugStay(gyroup + ":" + gyroforward + " down:forward"); 
                        Pitch = "Pitch"; pitchdir = 1;
                        Roll = "Roll"; rolldir = 1;
                        Yaw = "Yaw"; yawdir = -1;
                    }
                    if (gyroforward == backward)
                    {
                        //LOG.DebugStay(gyroup + ":" + gyroforward + " down:back"); 
                        Pitch = "Pitch"; pitchdir = -1;
                        Roll = "Roll"; rolldir = -1;
                        Yaw = "Yaw"; yawdir = -1;
                    }
                }
                else if (gyroup == left)
                {
                    if (gyroforward == forward)
                    {
                        //LOG.DebugStay(gyroup + ":" + gyroforward + " left:forward"); 
                        Pitch = "Yaw"; yawdir = -1;
                        Yaw = "Pitch"; pitchdir = -1;
                        Roll = "Roll"; rolldir = 1;
                    }
                    if (gyroforward == backward)
                    {
                        //LOG.DebugStay(gyroup + ":" + gyroforward + " left:back"); 
                        Pitch = "Yaw"; yawdir = -1;
                        Yaw = "Pitch"; pitchdir = 1;
                        Roll = "Roll"; rolldir = -1;
                    }
                    if (gyroforward == up)
                    {
                        //LOG.DebugStay(gyroup + ":" + gyroforward + " left:up"); 
                        Pitch = "Roll"; yawdir = -1;
                        Yaw = "Pitch"; rolldir = -1;
                        Roll = "Yaw"; pitchdir = -1;
                    }
                    if (gyroforward == down)
                    {
                        //LOG.DebugStay(gyroup + ":" + gyroforward + " left:down"); 
                        Pitch = "Roll"; yawdir = -1;
                        Yaw = "Pitch"; rolldir = 1;
                        Roll = "Yaw"; pitchdir = 1;
                    }
                }
                else if (gyroup == right)
                {
                    if (gyroforward == forward)
                    {
                        //LOG.DebugStay(gyroup + ":" + gyroforward + " right:forward");
                        Pitch = "Yaw"; yawdir = 1;
                        Yaw = "Pitch"; pitchdir = 1;
                        Roll = "Roll"; rolldir = 1;
                    }
                    if (gyroforward == backward)
                    {
                        //LOG.DebugStay(gyroup + ":" + gyroforward + " right:back"); 
                        Pitch = "Yaw"; yawdir = 1;
                        Yaw = "Pitch"; pitchdir = -1;
                        Roll = "Roll"; rolldir = -1;
                    }
                    if (gyroforward == up)
                    {
                        //LOG.DebugStay(gyroup + ":" + gyroforward + " right:up"); 
                        Pitch = "Roll"; yawdir = 1;
                        Yaw = "Pitch"; rolldir = -1;
                        Roll = "Yaw"; pitchdir = 1;
                    }
                    if (gyroforward == down)
                    {
                        //LOG.DebugStay(gyroup + ":" + gyroforward + " right:down"); 
                        Pitch = "Roll"; yawdir = 1;
                        Yaw = "Pitch"; rolldir = 1;
                        Roll = "Yaw"; pitchdir = -1;
                    }
                }
                else if (gyroup == forward)
                {
                    if (gyroforward == down)
                    {
                        //LOG.DebugStay(gyroup + ":" + gyroforward + " forward:down"); 
                        Roll = "Yaw"; yawdir = -1;
                        Pitch = "Pitch"; pitchdir = -1;
                        Yaw = "Roll"; rolldir = 1;
                    }
                    if (gyroforward == up)
                    {
                        //LOG.DebugStay(gyroup + ":" + gyroforward + " forward:up"); 
                        Roll = "Yaw"; yawdir = -1;
                        Pitch = "Pitch"; pitchdir = 1;
                        Yaw = "Roll"; rolldir = -1;
                    }
                    if (gyroforward == left)
                    {
                        //LOG.DebugStay(gyroup + ":" + gyroforward + " forward:left"); 
                        Pitch = "Yaw"; rolldir = 1;
                        Roll = "Pitch"; yawdir = -1;
                        Yaw = "Roll"; pitchdir = 1;
                    }
                    if (gyroforward == right)
                    {
                        //LOG.DebugStay(gyroup + ":" + gyroforward + " forward:right"); 
                        Pitch = "Yaw"; rolldir = -1;
                        Roll = "Pitch"; yawdir = -1;
                        Yaw = "Roll"; pitchdir = -1;
                    }
                }
                else if (gyroup == backward)
                {
                    if (gyroforward == down)
                    {
                        //LOG.DebugStay(gyroup + ":" + gyroforward + " back:down"); 
                        Roll = "Yaw"; yawdir = 1;
                        Pitch = "Pitch"; pitchdir = 1;
                        Yaw = "Roll"; rolldir = 1;
                    }
                    if (gyroforward == up)
                    {
                        //LOG.DebugStay(gyroup + ":" + gyroforward + " back:up"); 
                        Roll = "Yaw"; yawdir = 1;
                        Pitch = "Pitch"; pitchdir = -1;
                        Yaw = "Roll"; rolldir = -1;
                    }
                    if (gyroforward == left)
                    {
                        //LOG.DebugStay(gyroup + ":" + gyroforward + " back:left"); 
                        Pitch = "Yaw"; rolldir = 1;
                        Roll = "Pitch"; yawdir = 1;
                        Yaw = "Roll"; pitchdir = -1;
                    }
                    if (gyroforward == right)
                    {
                        //LOG.DebugStay(gyroup + ":" + gyroforward + " back:right");
                        Pitch = "Yaw"; rolldir = -1;
                        Roll = "Pitch"; yawdir = 1;
                        Yaw = "Roll"; pitchdir = 1;
                    }
                }
            }

        }
    }

    //////
}
