﻿
using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Game.Gui;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Ingame;
using IMyTerminalBlock = Sandbox.ModAPI.IMyTerminalBlock;
using ITerminalAction = Sandbox.ModAPI.Interfaces.ITerminalAction;

namespace MiningDrones
{
    public class Util
    {

        private readonly string logFile = "DroneConquestLogFile.txt";
        private readonly string errorFile = "DroneConquestErrorFile.txt";
        private readonly string settingsFile = "DroneConquestSettingsFile.txt";
        Dictionary<string, List<string>> customPaths = new Dictionary<string, List<string>>();

        private static Util _instance;
        private List<string> _logs = new List<string>();
        private List<string> _errorlogs = new List<string>();
        public bool DebuggingOn { get; set; }


        public void SaveBlockActions(IMyTerminalBlock block)
        {
            List<ITerminalAction> actions = new List<ITerminalAction>();
            var b = block;
            b.GetActions(actions);
            actions.ForEach(x => { Util.GetInstance().Log("[Drone.IsAlive] IMySmallGatlingGun actions -> " + x.Name, "actions.txt"); });
        }

        public static Util GetInstance()
        {
            if (_instance == null)
                _instance = new Util();

            return _instance;
        }

        public Util()
        {
            DebuggingOn = false;
        }

        public void Log(string log, string path)
        {
            if (!DebuggingOn)
                return;

            if (!path.Equals("none"))
            {
                if (customPaths.ContainsKey(path))
                    customPaths[path].Add("[" + path + "][" + DateTime.Now + "] " + log + "\n");
                else
                    customPaths.Add(path, new List<string>() { "[" + path + "][" + DateTime.Now + "] " + log + "\n" });

                while (customPaths[path].Count >= 200)
                    customPaths[path].Remove(customPaths[path].First());
            }
            //_logs.Add("[" + path + "][" + DateTime.Now + "] " + log);

        }

        public void LogError(string error)
        {
            if (!DebuggingOn)
                return;
            while (_errorlogs.Count >= 1000)
                _errorlogs.Remove(_errorlogs.First());

            _errorlogs.Add(error);
        }

        public void Help()
        {
            return;
            string helpMsg =
                "Dont forget to give this mod a thumbs up!!!.\r\n" +
                "\r\n" +
                "Edit Game settings in: \r\n" +
                "%APPDATA%\\Roaming\\SpaceEngineers\\Storage\\DroneConquest_DroneConquest\\DroneConquestSettingsFile.txt \r\n" +
                "\r\n" +
                "Player Drone Instructions:\r\n" +
                "Name RemoteControl block : #PlayerDrone# Additional Arguments for RemoteControl name field\r\n" +
                "*******(none of these are required and all have default values)********\r\n" +
                "\r\n" +
                "#on/off:power# -set power {on, off}\r\n" +
                "Default value = on\r\n" +
                "on = player can not manually override their drones controls when drone is online\r\n" +
                "off = makes drone give up control of the remote control block for manual override\r\n" +
                "\r\n" +
                "#order:type# -set OrderName {guard, patrol, sentry}\r\n" +
                "Default value = Guard. Orders drone to follow and orbit you, engaging any nearby enemies\r\n" +
                "Patrol = Orders drone to patrol around their current location rather then follow their leader\r\n" +
                "Sentry = Orders drones to stay put and only move to attack enemies that come near its area\r\n" +
                "\r\n" +
                "#broadcast:Type# -set Type {antenna, beacon}\r\n" +
                "Default = Beacon\r\n" +
                "Antenna = stats will be broadcasted via antenna if you do not set this then the drones stats will be broadcasted via Beacon\r\n" +
                "\r\n" +
                "#standing:type# -set type (passive, agressive)\r\n" +
                "Default type = agressive\r\n" +
                "\r\n" +
                "#orbitrange:Number# - set Number (whole positive)\r\n" +
                "default value = based on mass/size\r\n" +
                "Number = Sets the drones orbit range.\r\n" +
                "must be a non-negative number, value will be rounded down if not a whole number\r\n" +
                "\r\n" +
                "mode:Number - set type (whole positive)\r\n" +
                "default value = AtRange - the drones will by default orbit you\r\n" +
                "fighter = this makes the drone pick a static location in relation to you and stay there relitive to you.\r\n";


            MyAPIGateway.Utilities.ShowMissionScreen("Drone Conquest", "", "Help Guide", helpMsg, null, "OK");
        }


        public static void SaveLogs()
        {
            //MyAPIGateway.Parallel.StartBackground(
                GetInstance().SubTaskSavLogs();
                //);
        }

        public void Notify(string message)
        {
            
            MyAPIGateway.Utilities.ShowNotification(message, 2000);
        }

        private void SubTaskSavLogs()
        {

            using (var mWriter = MyAPIGateway.Utilities.WriteFileInLocalStorage(logFile, typeof(Util)))
            {
                foreach (var s in _logs)
                {
                    mWriter.WriteLine(s + "\n");
                }
                mWriter.Flush();
            }


            foreach (var path in customPaths)
            {
                using (var mWriter = MyAPIGateway.Utilities.WriteFileInLocalStorage(path.Key, typeof(Util)))
                {
                    foreach (var log in path.Value)
                    {
                        mWriter.WriteLine(log + "\n");
                    }

                    mWriter.Flush();
                }
            }


            using (var mWriter = MyAPIGateway.Utilities.WriteFileInLocalStorage(errorFile, typeof(Util)))
            {
                foreach (var s in _errorlogs)
                {
                    mWriter.WriteLine(s + "\n");
                }

                mWriter.Flush();
            }
        }
    }
}
