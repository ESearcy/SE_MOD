using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using VRage.Game.ModAPI;
using VRage.ModAPI;

using IMyControllableEntity = VRage.Game.ModAPI.Interfaces.IMyControllableEntity;

namespace MiningDrones
{
    
    public class DroneCommandCenter
    {      
        private string onoff = "#Power#";
        private string mode = "#Guard/Sentry#";
        private string broadcasting = "#Broadcasting#";
        private string agressive = "#Agressive#";
        private string formation = "#Formation#";
        private string mining = "#Mining#";

        private static string logPath = "MiningDrones.txt";

        private Sandbox.ModAPI.Ingame.IMyTerminalBlock onoffBlock;
        private Sandbox.ModAPI.Ingame.IMyTerminalBlock modeBlock;
        private Sandbox.ModAPI.Ingame.IMyTerminalBlock broadcastingBlock;
        private Sandbox.ModAPI.Ingame.IMyTerminalBlock agressiveBlock;
        private Sandbox.ModAPI.Ingame.IMyTerminalBlock formationBlock;
        private Sandbox.ModAPI.Ingame.IMyTerminalBlock miningBlock;

        public long PlayerId;
        public IMyEntity MyEntity;

        public DroneCommandCenter(IMyCubeGrid entity)
        {
            MyEntity = entity;
            Sandbox.ModAPI.IMyGridTerminalSystem gridTerminal =
                    MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(entity);

            List<Sandbox.ModAPI.Ingame.IMyTerminalBlock> blocks = new List<Sandbox.ModAPI.Ingame.IMyTerminalBlock>();

            gridTerminal.SearchBlocksOfName(onoff, blocks);
            if (blocks.Count > 0)
            {
                onoffBlock = blocks.FirstOrDefault();
                Util.GetInstance().Log("OnOff Controls Found", "CommandCenterBootUp.txt");
            }
            blocks.Clear();
            gridTerminal.SearchBlocksOfName(mode, blocks);
            if (blocks.Count > 0)
            {
                modeBlock = blocks.FirstOrDefault();
                Util.GetInstance().Log("ModeBlock Controls Found", "CommandCenterBootUp.txt");
            }
            blocks.Clear();
            gridTerminal.SearchBlocksOfName(mining, blocks);
            if (blocks.Count > 0)
            {
                miningBlock = blocks.FirstOrDefault();
                Util.GetInstance().Log("MiningBlock Controls Found", "CommandCenterBootUp.txt");
            }
            blocks.Clear();
            gridTerminal.SearchBlocksOfName(broadcasting, blocks);
            if (blocks.Count > 0)
            {
                broadcastingBlock = blocks.FirstOrDefault();
                Util.GetInstance().Log("Broadcasting Controls Found", "CommandCenterBootUp.txt");
            }
            blocks.Clear();
            gridTerminal.SearchBlocksOfName(agressive, blocks);
            if (blocks.Count > 0)
            {
                agressiveBlock = blocks.FirstOrDefault();
                Util.GetInstance().Log("Agression Controls Found", "CommandCenterBootUp.txt");
            }
            blocks.Clear();
            gridTerminal.SearchBlocksOfName(formation, blocks);
            if (blocks.Count > 0)
            {
                formationBlock = blocks.FirstOrDefault();
                Util.GetInstance().Log("Formation Controls Found", "CommandCenterBootUp.txt");
            }

            var lstSlimBlock = new List<IMySlimBlock>();
            var myCubeGrid = MyEntity as IMyCubeGrid;
            if (myCubeGrid != null)
                myCubeGrid.GetBlocks(lstSlimBlock, (x) => x.FatBlock is Sandbox.ModAPI.IMyShipController);
            
            var ShipControls = lstSlimBlock[0].FatBlock as IMyControllableEntity;

            PlayerId = ((Sandbox.ModAPI.IMyTerminalBlock)ShipControls).OwnerId;
        }

        public DroneModes DroneMode = DroneModes.AtRange;
        public Standing Stance = Standing.Hostile;
        public Boolean BroadcastingEnabled = true;
        public Boolean DronesOnline = true;
        public ActionTypes StandingOrder = ActionTypes.Guard;
        public bool MiningDronesEnabled = false;

        public void Update()
        {
            if (broadcastingBlock != null)
            {
                var temp = BroadcastingEnabled;

                BroadcastingEnabled = broadcastingBlock.IsWorking;

                if (temp != BroadcastingEnabled)
                    PlayAudioForBroadcastingChanged();
            }
            else
            {
                Sandbox.ModAPI.IMyGridTerminalSystem gridTerminal =
                    MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(MyEntity as IMyCubeGrid);

                List<Sandbox.ModAPI.Ingame.IMyTerminalBlock> blocks = new List<Sandbox.ModAPI.Ingame.IMyTerminalBlock>();
                gridTerminal.SearchBlocksOfName(broadcasting, blocks);
                if (blocks.Count > 0)
                    broadcastingBlock = blocks.FirstOrDefault();
            }

            if (miningBlock != null)
            {
                var temp = MiningDronesEnabled;

                MiningDronesEnabled = miningBlock.IsWorking;

                if (temp != MiningDronesEnabled)
                    PlayAudioForMiningChanged();
            }
            else
            {
                Sandbox.ModAPI.IMyGridTerminalSystem gridTerminal =
                    MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(MyEntity as IMyCubeGrid);

                List<Sandbox.ModAPI.Ingame.IMyTerminalBlock> blocks = new List<Sandbox.ModAPI.Ingame.IMyTerminalBlock>();
                gridTerminal.SearchBlocksOfName(mining, blocks);
                if (blocks.Count > 0)
                    miningBlock = blocks.FirstOrDefault();
            }

            if (formationBlock != null)
            {
                var temp = DroneMode.ToString();

                DroneMode = formationBlock.IsWorking ? DroneModes.Fighter : DroneMode = DroneModes.AtRange;

                if (temp != DroneMode.ToString())
                    PlayAudioForDroneModeChanged();
            }
            else
            {
                Sandbox.ModAPI.IMyGridTerminalSystem gridTerminal =
                    MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(MyEntity as IMyCubeGrid);

                List<Sandbox.ModAPI.Ingame.IMyTerminalBlock> blocks = new List<Sandbox.ModAPI.Ingame.IMyTerminalBlock>();
                gridTerminal.SearchBlocksOfName(formation, blocks);
                if (blocks.Count > 0)
                    formationBlock = blocks.FirstOrDefault();
            }

            if (agressiveBlock != null)
            {
                var temp = Stance;

                Stance = agressiveBlock.IsWorking?Standing.Hostile:Stance = Standing.Passive;

                if (temp != Stance)
                    PlayAudioForStanceChanged();
            }
            else
            {
                Sandbox.ModAPI.IMyGridTerminalSystem gridTerminal =
                    MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(MyEntity as IMyCubeGrid);

                List<Sandbox.ModAPI.Ingame.IMyTerminalBlock> blocks = new List<Sandbox.ModAPI.Ingame.IMyTerminalBlock>();
                gridTerminal.SearchBlocksOfName(agressive, blocks);
                if (blocks.Count > 0)
                    agressiveBlock = blocks.FirstOrDefault();
            }

            if (modeBlock != null)
            {
                var temp = StandingOrder;
                if (modeBlock.IsWorking)
                    StandingOrder = ActionTypes.Guard;
                else
                    StandingOrder = ActionTypes.Sentry;

                if (temp != StandingOrder)
                    PlayAudioForStandingOrderChanged();
            }
            else
            {
                Sandbox.ModAPI.IMyGridTerminalSystem gridTerminal =
                    MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(MyEntity as IMyCubeGrid);

                List<Sandbox.ModAPI.Ingame.IMyTerminalBlock> blocks = new List<Sandbox.ModAPI.Ingame.IMyTerminalBlock>();
                gridTerminal.SearchBlocksOfName(mode, blocks);
                if (blocks.Count > 0)
                    modeBlock = blocks.FirstOrDefault();
            }

            if (onoffBlock != null)
            {
                var temp = DronesOnline;
                if (onoffBlock.IsWorking)
                    DronesOnline = true;
                else
                    DronesOnline = false;

                if (temp != DronesOnline)
                    PlayAudioForDronesOnlineChanged();
            }
            else
            {
                Sandbox.ModAPI.IMyGridTerminalSystem gridTerminal =
                    MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(MyEntity as IMyCubeGrid);

                List<Sandbox.ModAPI.Ingame.IMyTerminalBlock> blocks = new List<Sandbox.ModAPI.Ingame.IMyTerminalBlock>();
                gridTerminal.SearchBlocksOfName(onoff, blocks);
                if (blocks.Count > 0)
                    onoffBlock = blocks.FirstOrDefault();
            }
        }

        private void PlayAudioForMiningChanged()
        {
            Util.GetInstance().Notify("Mining Changed");
        }

        private void PlayAudioForDronesOnlineChanged()
        {
            Util.GetInstance().Notify("Online Changed");
        }

        private void PlayAudioForStandingOrderChanged()
        {
            Util.GetInstance().Notify("Standing Changed");
        }

        private void PlayAudioForStanceChanged()
        {
            Util.GetInstance().Notify("Stance Changed");
        }

        private void PlayAudioForDroneModeChanged()
        {
            Util.GetInstance().Notify("DroneMode Changed");
        }

        private void PlayAudioForBroadcastingChanged()
        {
            Util.GetInstance().Notify("Broadcasting Changed");
        }
    }
}
