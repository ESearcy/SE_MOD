using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using VRage.Game;
using VRage.Game.ModAPI.Ingame;
using VRageMath;
using SpaceEngineers.Game.ModAPI.Ingame;
using SEMod.INGAME.classes.systems;

namespace SEMod.INGAME.classes
{
    //////
    public class ProductionSystem
    {
        Logger L;
        List<IMyShipWelder> welders = new List<IMyShipWelder>();
        List<IMyMotorStator> rotors = new List<IMyMotorStator>();
        List<IMyProjector> projectors = new List<IMyProjector>();
        List<IMyExtendedPistonBase> pistons = new List<IMyExtendedPistonBase>();
        List<IMyAirtightHangarDoor> doors = new List<IMyAirtightHangarDoor>();
        List<IMyShipMergeBlock> merges = new List<IMyShipMergeBlock>();
        FactoryState currentState = FactoryState.Unknown;

        const float rotationVelocity = 3f;

        public ProductionSystem(Logger log, IMyCubeGrid grid, ShipComponents components)
        { }

        public ProductionSystem(IMyBlockGroup group, Logger log)
        {
            L = log;
            group.GetBlocksOfType(welders);
            group.GetBlocksOfType(rotors);
            group.GetBlocksOfType(projectors);
            group.GetBlocksOfType(pistons);
            group.GetBlocksOfType(doors);
            group.GetBlocksOfType(merges);

            L.Debug("Group Parts");
        }

        DateTime LastStateChange = DateTime.Now.AddMinutes(-2);
        public void UpdateFactoryState()
        {
            if ((DateTime.Now - LastStateChange).TotalMinutes < 2)
                return;
            LastStateChange = DateTime.Now;

            switch (currentState)
            {
                case FactoryState.Unknown:
                    FindCurrentState();
                    break;
                case FactoryState.ReadyToBuild:
                    Build();
                    currentState = FactoryState.Building;
                    break;
                case FactoryState.Building:
                    break;
                case FactoryState.Releasing:
                    Release();
                    currentState = FactoryState.Reseting;
                    break;
                case FactoryState.Reseting:
                    Reset();
                    currentState = FactoryState.ReadyToBuild;
                    break;
            }
        }

        private void FindCurrentState()
        {
            var rotorsnotreset = rotors.Where(x => (x.Angle / (float)Math.PI * 180f) != 0).Count();
            var pistonsnotReset = pistons.Where(x => x.CurrentPosition != 0).Count();

            if (rotorsnotreset > 0 && pistonsnotReset > 0)
                currentState = FactoryState.Releasing;
            if (rotorsnotreset == 0 && pistonsnotReset > 0)
                currentState = FactoryState.Building;
            if (rotorsnotreset == 0 && pistonsnotReset == 0)
                currentState = FactoryState.ReadyToBuild;

        }

        public void Build()
        {
            foreach (var welder in welders)
                welder.GetActionWithName("OnOff_On").Apply(welder);

            foreach (var merge in merges)
                merge.GetActionWithName("OnOff_On").Apply(merge);

            foreach (var piston in pistons)
                piston.SetValue<float>("Velocity", 1);
        }

        public bool IsValid()
        {

            return welders.Any() && projectors.Any() && pistons.Any() && merges.Any();
        }

        internal bool IsOperational()
        {
            return true;
        }

        public void Reset()
        {
            var rotorsnotreset = 0;
            foreach (var rotor in rotors)
            {
                var angle = (rotor.Angle / (float)Math.PI * 180f);
                if ((int)angle != 0)
                {
                    rotorsnotreset++;
                    rotor.SetValue<float>("Velocity", -angle / Math.Abs(angle));
                }
            }

            if (rotorsnotreset == 0)
            {
                foreach (var piston in pistons)
                {
                    piston.SetValue<float>("Velocity", -1);
                }
            }

        }

        public void Release()
        {
            foreach (var welder in welders)
                welder.GetActionWithName("OnOff_Off").Apply(welder);

            foreach (var merge in merges)
                merge.GetActionWithName("OnOff_Off").Apply(merge);

            var rotorsnotreset = 0;
            foreach (var rotor in rotors)
            {
                var angle = (rotor.Angle / (float)Math.PI * 180f);
                if ((int)angle > -40)
                {
                    rotorsnotreset++;
                    rotor.SetValue<float>("Velocity", -2);
                }
                else rotor.SetValue<float>("Velocity", -0);
            }
        }
    }
    //////
}
