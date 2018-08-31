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
using SEMod.INGAME.classes.model;

namespace SEMod.INGAME.classes
{
    //////
    public class CommunicationSystem
    {
        List<String> PendingMessages = new List<String>();
        private String lastmessageOnHold = null;
        ShipComponents components;

        int messagesSent = 0;
        int messagesRecieved = 0;

        public ParsedMessage ParseMessage(string argument)
        {
            ParsedMessage pm = new ParsedMessage(argument, L);

            //L.AddRecievedMessage(argument);
            messagesRecieved++;

            return pm;
        }

        public void SendMessage(string m)
        {
            PendingMessages.Add(m);
        }

        public void TransmitOrder(DroneOrder m, long commandID)
        {
            PendingMessages.Add(ParsedMessage.CreateEncryptedOrder(m, commandID, m.RequestId));
        }

        Logger L;
        IMyCubeGrid grid;

        public CommunicationSystem(Logger l, IMyCubeGrid _grid, ShipComponents componets)
        {
            L = l;
            grid = _grid;
            this.components = componets;
        }

        public void Update()
        {

            int NumberMessagesSent = AttemptSendPendingMessages();
            //L.Debug("Number of Antennas: " + _radioAntennas.Count());
        }

        public void SendAwakeningMessage()
        {
            //L.Debug("Sending Awakening Call");
            PendingMessages.Add(ParsedMessage.CreateAwakeningMessage());
        }

        private bool TransmitMessage(String message)
        {
            foreach (var antenna in components.RadioAntennas)
            {
                //LOG.Debug(message);
                if (antenna.TransmitMessage(message, MyTransmitTarget.Owned))
                {
                    //L.Debug("Transmiting: " + message);
                    messagesSent++;
                    return true;
                }
            }
            return false;
        }

        public int AttemptSendPendingMessages()
        {
            //L.Debug("Sending Messages: "+ PendingMessages.Count());
            var sentMessageCount = 0;
            bool ableToTransmit = components.RadioAntennas.Any();

            if (lastmessageOnHold != null && ableToTransmit)
            {
                ableToTransmit = TransmitMessage(lastmessageOnHold);
                if (ableToTransmit)
                    lastmessageOnHold = null;
            }

            while (PendingMessages.Any() && ableToTransmit)
            {
                lastmessageOnHold = PendingMessages.First();
                PendingMessages.Remove(lastmessageOnHold);

                if (lastmessageOnHold != null)
                {
                    ableToTransmit = TransmitMessage(lastmessageOnHold);

                    if (ableToTransmit)
                        lastmessageOnHold = null;

                    break;
                }
                else
                    L.Error("Failed to transmit: (expected one pending message)" + lastmessageOnHold);
            }
            //L.Debug("Messages Sent: "+ sentMessageCount);
            return sentMessageCount;
        }

        internal bool IsOperational()
        {
            return components.RadioAntennas.Any();
        }

        //internal void TransmitOrder(Order order, IMyCubeGrid grid)
        //{
        //    L.Debug("OrderType: " + order.Type+""+ grid.EntityId);
        //    var encryptedOrder = ParsedMessage.CreateEncryptedOrder(order, grid.EntityId);
        //    SendMessage(encryptedOrder);
        //}

        int numberMessagesSent = 0;
        internal int GetMsgSntCount()
        {
            numberMessagesSent++;
            return numberMessagesSent;
        }
    }
    //////
}
