using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.ModAPI.Ingame;

namespace SEMod.INGAME.classes.systems
{
    //////
    public class ShipComponents
    {
        public List<IMyTextPanel> TextPanels = new List<IMyTextPanel>();
        public List<IMySensorBlock> Sensors = new List<IMySensorBlock>();
        public List<IMyCameraBlock> Cameras = new List<IMyCameraBlock>();
        public List<IMyProgrammableBlock> ProgramBlocks = new List<IMyProgrammableBlock>();
        public List<IMyRadioAntenna> RadioAntennas = new List<IMyRadioAntenna>();
        public List<IMyLaserAntenna> LaserAntennas = new List<IMyLaserAntenna>();
        public List<IMyRemoteControl> ControlUnits = new List<IMyRemoteControl>();
        public List<IMyShipConnector> Connectors = new List<IMyShipConnector>();
        public List<IMyShipDrill> MiningDrills = new List<IMyShipDrill>();
        public List<IMyThrust> Thrusters = new List<IMyThrust>();
        public List<IMyGyro> Gyros = new List<IMyGyro>();
        public List<IMyShipMergeBlock> MergeBlocks = new List<IMyShipMergeBlock>();
        public List<IMySmallGatlingGun> GatlingGuns = new List<IMySmallGatlingGun>();
        public List<IMySmallMissileLauncher> RocketLaunchers = new List<IMySmallMissileLauncher>();
        public List<IMyTerminalBlock> AllBlocks = new List<IMyTerminalBlock>();

        public void Sync(IMyGridTerminalSystem GridTerminalSystem, IMyCubeGrid grid)
        {
            MergeBlocks.Clear();
            MiningDrills.Clear();
            GatlingGuns.Clear();
            RocketLaunchers.Clear();
            ProgramBlocks.Clear();
            LaserAntennas.Clear();
            RadioAntennas.Clear();
            TextPanels.Clear();
            ControlUnits.Clear();
            Connectors.Clear();
            Sensors.Clear();
            Gyros.Clear();
            Thrusters.Clear();
            Cameras.Clear();
            AllBlocks.Clear();

            GridTerminalSystem.GetBlocks(AllBlocks);
            GridTerminalSystem.GetBlocksOfType(MergeBlocks, b => b.CubeGrid == grid);
            GridTerminalSystem.GetBlocksOfType(Cameras, b => b.CubeGrid == grid);
            GridTerminalSystem.GetBlocksOfType(Gyros, b => b.CubeGrid == grid);
            GridTerminalSystem.GetBlocksOfType(Thrusters, b => b.CubeGrid == grid);
            GridTerminalSystem.GetBlocksOfType(Sensors, b => b.CubeGrid == grid);
            GridTerminalSystem.GetBlocksOfType(Connectors, b => b.CubeGrid == grid);
            GridTerminalSystem.GetBlocksOfType(ProgramBlocks, b => b.CubeGrid == grid);
            GridTerminalSystem.GetBlocksOfType(LaserAntennas, b => b.CubeGrid == grid);
            GridTerminalSystem.GetBlocksOfType(RadioAntennas, b => b.CubeGrid == grid);
            GridTerminalSystem.GetBlocksOfType(TextPanels, b => b.CubeGrid == grid);
            GridTerminalSystem.GetBlocksOfType(ControlUnits, b => b.CubeGrid == grid);
            GridTerminalSystem.GetBlocksOfType(MiningDrills, b => b.CubeGrid == grid);
            GridTerminalSystem.GetBlocksOfType(GatlingGuns, b => b.CubeGrid == grid);
            GridTerminalSystem.GetBlocksOfType(RocketLaunchers, b => b.CubeGrid == grid);

            foreach (var sensor in Sensors)
            {
                sensor.DetectEnemy = true;
                sensor.DetectPlayers = true;
                sensor.DetectLargeShips = true;
                sensor.DetectSmallShips = true;
                sensor.DetectOwner = true;
                sensor.DetectStations = true;
                sensor.DetectAsteroids = true;

                sensor.BackExtend = 50;
                sensor.FrontExtend = 50;
                sensor.LeftExtend = 50;
                sensor.RightExtend = 50;
                sensor.TopExtend = 50;
                sensor.BottomExtend = 50;
            }
        }
    }
    //////
}
