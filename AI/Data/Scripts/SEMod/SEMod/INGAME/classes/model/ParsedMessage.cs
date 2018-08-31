using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using VRage.Game;
using VRage.Game.ModAPI.Ingame;
using VRageMath;
using SpaceEngineers.Game.ModAPI.Ingame;
using SEMod.INGAME.classes.model;

namespace SEMod.INGAME.classes
{
    //////
    public class ParsedMessage
    {
        Dictionary<String, String> messageElements = new Dictionary<string, string>();
        Logger LOG;
        public MessageCode MessageType = MessageCode.Unknown;
        public OrderType OrderType = OrderType.Unknown;
        public double RequestID = 0;
        public long EntityId = 0;
        public long TargetEntityId = 0;
        public String Name = null;
        public Vector3D Location = Vector3D.Zero;
        public Vector3D Velocity = Vector3D.Zero;
        public Vector3D AttackPoint = Vector3D.Zero;
        public String Status = null;
        public long CommanderId = 0;
        public String MessageString;
        public String BounceString;
        public int NumBounces = 0;
        public static int MaxNumBounces = 2;
        public bool IsAwakeningSignal = false;
        public int TargetRadius = 0;
        public Vector3D AlignForward = Vector3D.Zero;
        public Vector3D AlignUp = Vector3D.Zero;
        public bool Docked = false;
        public int ConnectorCount = 0;
        public int DrillCount = 0;
        public int SensorCount = 0;
        public int CameraCount = 0;
        public double ShipSize = 0;
        public int WeaponCount = 0;
        public int PercentCargo = 0;
        public string Type;
        public MyRelationsBetweenPlayerAndBlock Relationship = MyRelationsBetweenPlayerAndBlock.Neutral;

        // message flags
        const String MESSAGETYPE_FLAG = "11";
        const string ORDERTYPE_FLAG = "110";
        const String REQUESTID_FLAG = "12";
        const String NAME_FLAG = "13";
        const String LOCATION_FLAG = "14";
        const String ATTACKPOINT_FLAG = "16";
        const String VELOCITY_FLAG = "15";
        const String ENTITYID_FLAG = "17";
        const String TARGETID_FLAG = "19";
        const String COMMANDID_FLAG = "111";
        const String STATUS_FLAG = "18";
        const String MAXBOUNCE_FLAG = "113";
        const String NUMBOUNCES_FLAG = "114";
        const String TARGETRADIUS_FLAG = "115";
        const String ALIGNFORWARDVECTOR_FLAG = "116";
        const String ALIGNUPVECTOR_FLAG = "117";
        const String DOCKEDSTATUS_FLAG = "118";
        const String SHIPSIZE_FLAG = "122";
        const String PERCENTCARGO_FLAG = "546";
        const String AWAKENING_FLAG = "666";
        const String RELATIONSHIP_FLAG = "fof";
        const String TYPE_FLAG = "126";

        //message types
        const String REGISTER_FLAG = "21";
        const String CONFIRMATION_FLAG = "22";
        const String UPDATE_FLAG = "23";
        const String PINGENTITY_FLAG = "24";
        const String ORDER_FLAG = "25";

        //component counts
        const String NUMCONNECTORS_FLAG = "119";
        const String NUMMININGDRILLS_FLAG = "120";
        const String NUMSENSORS_FLAG = "121";
        const String NUMWEAPONS_FLAG = "124";
        const String NUMROCKETLAUNCHERS_FLAG = "123";
        const String NUMCAMERA_FLAG = "125";

        //ordertypes
        const String DOCKORDER = "26";
        const String UNDOCKORDER = "u26";
        const String ATTACKORDER = "27";
        const String MININGORDER = "28";
        const String ALIGNTOORDER = "30";
        const String FLYTOORDER = "29";
        const String SCANTOORDER = "31";
        const String STANDBYORDER = "34";

        public ParsedMessage(String message, Logger log)
        {
            LOG = log;
            MessageString = message;
            String messageNoBrackets = message.Replace("{", "").Replace("}", "");
            ReadProperties(messageNoBrackets);

            foreach (var pair in messageElements)
            {
                try
                {
                    switch (pair.Key)
                    {
                        case MESSAGETYPE_FLAG:
                            ParseMessageType(pair.Value);
                            break;
                        case ORDERTYPE_FLAG:
                            ParseOrderType(pair.Value);
                            break;
                        case REQUESTID_FLAG:
                            RequestID = double.Parse(pair.Value);
                            break;
                        case ENTITYID_FLAG:
                            EntityId = long.Parse(pair.Value);
                            break;
                        case PERCENTCARGO_FLAG:
                            PercentCargo = int.Parse(pair.Value);
                            break;
                        case COMMANDID_FLAG:
                            CommanderId = long.Parse(pair.Value);
                            break;
                        case RELATIONSHIP_FLAG:
                            MyRelationsBetweenPlayerAndBlock.TryParse(pair.Value, out Relationship);
                            break;
                        case TARGETID_FLAG:
                            TargetEntityId = long.Parse(pair.Value);
                            break;
                        case NUMMININGDRILLS_FLAG:
                            DrillCount = int.Parse(pair.Value);
                            break;
                        case NUMSENSORS_FLAG:
                            SensorCount = int.Parse(pair.Value);
                            break;
                        case NUMCONNECTORS_FLAG:
                            ConnectorCount = int.Parse(pair.Value);
                            break;
                        case NUMBOUNCES_FLAG:
                            NumBounces = (int)double.Parse(pair.Value);
                            break;
                        case SHIPSIZE_FLAG:
                            ShipSize = double.Parse(pair.Value);
                            break;
                        case MAXBOUNCE_FLAG:
                            MaxNumBounces = (int)double.Parse(pair.Value);
                            break;
                        case TYPE_FLAG:
                            Type = pair.Value;
                            break;
                        case NUMCAMERA_FLAG:
                            CameraCount = int.Parse(pair.Value);
                            break;
                        case ATTACKPOINT_FLAG:
                            AttackPoint = TryParseVector(pair.Value);
                            break;
                        case NAME_FLAG:
                            Name = pair.Value;
                            break;
                        case LOCATION_FLAG:
                            Location = TryParseVector(pair.Value);
                            break;
                        case VELOCITY_FLAG:
                            Velocity = TryParseVector(pair.Value);
                            break;
                        case DOCKEDSTATUS_FLAG:
                            Docked = bool.Parse(pair.Value);
                            break;
                        case STATUS_FLAG:
                            Status = pair.Value;
                            break;
                        case ALIGNFORWARDVECTOR_FLAG:
                            AlignForward = TryParseVector(pair.Value);
                            break;
                        case ALIGNUPVECTOR_FLAG:
                            AlignUp = TryParseVector(pair.Value);
                            break;
                        case AWAKENING_FLAG:
                            IsAwakeningSignal = true;
                            break;
                        case TARGETRADIUS_FLAG:
                            TargetRadius = (int)double.Parse(pair.Value);
                            break;
                        case NUMWEAPONS_FLAG:
                            WeaponCount = int.Parse(pair.Value);
                            break;
                        default:
                            return;
                    }
                }
                catch (Exception e)
                {
                    log.Error("Error parsing Communications\n" + e.Message + " " + pair.Key + ":" + pair.Value);
                }
            }
        }

        public void ParseMessageType(String messaget)
        {
            switch (messaget)
            {
                case REGISTER_FLAG:
                    MessageType = MessageCode.Register;
                    break;
                case CONFIRMATION_FLAG:
                    MessageType = MessageCode.Confirmation;
                    break;
                case UPDATE_FLAG:
                    MessageType = MessageCode.Update;
                    break;
                case PINGENTITY_FLAG:
                    MessageType = MessageCode.PingEntity;
                    break;
                case ORDER_FLAG:
                    MessageType = MessageCode.Order;
                    break;
            }
        }

        public void ParseOrderType(string ordertype)
        {
            switch (ordertype)
            {
                case UNDOCKORDER:
                    OrderType = OrderType.Undock;
                    break;
                case DOCKORDER:
                    OrderType = OrderType.Dock;
                    break;
                case ATTACKORDER:
                    OrderType = OrderType.Attack;
                    break;
                case SCANTOORDER:
                    OrderType = OrderType.Scan;
                    break;
                case FLYTOORDER:
                    OrderType = OrderType.FlyTo;
                    break;
                case MININGORDER:
                    OrderType = OrderType.Mine;
                    break;
                case ALIGNTOORDER:
                    OrderType = OrderType.AlignTo;
                    break;
                case STANDBYORDER:
                    OrderType = OrderType.Standby;
                    break;
            }
        }

        public static String CreateAwakeningMessage()
        {
            String msgStr = "";

            msgStr += AWAKENING_FLAG + ":" + 0;
            msgStr += "," + NUMBOUNCES_FLAG + ":" + 0;


            return "{" + msgStr + "}";
        }

        private static String VectorToString(Vector3D vect)
        {
            String str = Math.Round(vect.X, 4) + "|" + Math.Round(vect.Y, 4) + "|" + Math.Round(vect.Z, 4);
            return str;
        }

        public bool IsValid()
        {
            if (MessageType != MessageCode.Unknown)
            {
                return true;
            }

            return false;
        }

        //should be formatted as x-y-z
        private Vector3D TryParseVector(String vector)
        {
            var splits = vector.Split('|');
            try
            {
                if (splits.Count() == 3)
                {
                    var loc = new Vector3D(double.Parse(splits[0]), double.Parse(splits[1]), double.Parse(splits[2]));
                    //LOG.Debug("Location Parsed: "+loc);
                    return loc;
                }
                else
                {
                    LOG.Error("Unable to parse into 3 splits: " + vector);
                }

            }
            catch
            {
                LOG.Error("Unable to parse Location: " + vector);
            }

            return Vector3D.Zero;
        }

        public void ReadProperties(String message)
        {
            message = message.Trim();
            if (message.Length < 3)
                return;

            String bouncemsg = "{";
            var splits = message.Split(',');
            int index = 0;

            foreach (var pair in splits)
            {
                var clean = pair.Trim();
                var keyval = clean.Split(':');
                if (keyval.Length == 2)
                {
                    var key = keyval[0];
                    var value = keyval[1];
                    messageElements.Add(key, value);

                    if (index == 0 && key == NUMBOUNCES_FLAG)
                        bouncemsg += key + ":" + value + "";
                    else if (index == 0)
                        bouncemsg += key + ":" + value + "";
                    else if (key == NUMBOUNCES_FLAG)
                        bouncemsg += "," + key + ":" + (int.Parse(value) + 1) + "";
                    else
                        bouncemsg += "," + key + ":" + value + "";
                    index++;
                }
                else
                {
                    LOG.Error("failed to parse message {" + message + "} @ " + clean);
                }
            }
            bouncemsg += "}";
            BounceString = bouncemsg;
        }

        public override String ToString()
        {
            return BounceString;
        }

        internal static string CreateConfirmationMessage(long entityId, long targetEntity, double requestID)
        {
            String msgStr = "";

            msgStr += MESSAGETYPE_FLAG + ":" + CONFIRMATION_FLAG;
            msgStr += "," + ENTITYID_FLAG + ":" + entityId;
            msgStr += "," + TARGETID_FLAG + ":" + targetEntity;
            msgStr += "," + NUMBOUNCES_FLAG + ":" + 1;
            msgStr += "," + REQUESTID_FLAG + ":" + requestID;


            return "{" + msgStr + "}";
        }

        internal static string CreateEncryptedOrder(DroneOrder order, long commandId, double requestID)
        {

            string ordertype = "";
            switch (order.Ordertype)
            {
                case OrderType.AlignTo:
                    ordertype = ALIGNTOORDER;
                    break;
                case OrderType.Dock:
                    ordertype = DOCKORDER;
                    break;
                case OrderType.Undock:
                    ordertype = UNDOCKORDER;
                    break;
                case OrderType.FlyTo:
                    ordertype = FLYTOORDER;
                    break;
                case OrderType.Mine:
                    ordertype = MININGORDER;
                    break;
                case OrderType.Scan:
                    ordertype = SCANTOORDER;
                    break;
                case OrderType.Attack:
                    ordertype = ATTACKORDER;
                    break;
                case OrderType.Standby:
                    ordertype = STANDBYORDER;
                    break;
            }
            var msgStr = MESSAGETYPE_FLAG + ":" + ORDER_FLAG +
                "," + ORDERTYPE_FLAG + ":" + ordertype +
                "," + TARGETID_FLAG + ":" + order.TargetEntityID +
                "," + ALIGNFORWARDVECTOR_FLAG + ":" + VectorToString(order.DirectionalVectorOne) +
                "," + ALIGNUPVECTOR_FLAG + ":" + VectorToString(order.DirectionalVectorOne) +
                "," + COMMANDID_FLAG + ":" + commandId +
                "," + ENTITYID_FLAG + ":" + order.DroneId +
                "," + REQUESTID_FLAG + ":" + requestID +
                "," + NUMBOUNCES_FLAG + ":" + 0 +
                "," + LOCATION_FLAG + ":" + VectorToString(order.PrimaryLocation);

            return "{" + msgStr + "}";
        }
        

        public static String CreateConfirmationMessage(String entityId, String requestId)
        {
            String msgStr = "";

            msgStr += MESSAGETYPE_FLAG + ":" + CONFIRMATION_FLAG;
            msgStr += "," + ENTITYID_FLAG + ":" + entityId;
            msgStr += "," + NUMBOUNCES_FLAG + ":" + 0;
            msgStr += "," + REQUESTID_FLAG + ":" + requestId;


            return "{" + msgStr + "}";
        }

        public static String CreateRegisterMessage(long entityId, int requestsSent)
        {
            String msgStr = "";
            msgStr += MESSAGETYPE_FLAG + ":" + REGISTER_FLAG;
            msgStr += "," + REQUESTID_FLAG + ":" + entityId + 10;
            msgStr += "," + NUMBOUNCES_FLAG + ":" + 0;
            msgStr += "," + ENTITYID_FLAG + ":" + entityId;
            ;
            msgStr = "{" + msgStr + "}";

            return msgStr;
        }

        public static String CreateUpdateMessage(long entityId, long targetCommandShip, Vector3D velocity, Vector3D location, int requestsSent, bool docked, int numberofCameras, int numberofConnectors, int numDrills, int numsensors, int numWeapons, double shipsize, int percentCargo)
        {
            String msgStr = "";

            msgStr += MESSAGETYPE_FLAG + ":" + UPDATE_FLAG;
            msgStr += "," + ENTITYID_FLAG + ":" + entityId;
            msgStr += "," + TARGETID_FLAG + ":" + targetCommandShip;
            msgStr += "," + SHIPSIZE_FLAG + ":" + shipsize;
            msgStr += "," + PERCENTCARGO_FLAG + ":" + percentCargo;
            msgStr += "," + NUMCONNECTORS_FLAG + ":" + numberofConnectors;
            msgStr += "," + NUMCAMERA_FLAG + ":" + numberofCameras;
            msgStr += "," + NUMSENSORS_FLAG + ":" + numsensors;
            msgStr += "," + NUMMININGDRILLS_FLAG + ":" + numDrills;
            msgStr += "," + NUMWEAPONS_FLAG + ":" + numWeapons;
            msgStr += "," + DOCKEDSTATUS_FLAG + ":" + docked;
            msgStr += "," + REQUESTID_FLAG + ":" + entityId + requestsSent;
            msgStr += "," + NUMBOUNCES_FLAG + ":" + 0;
            msgStr += "," + VELOCITY_FLAG + ":" + VectorToString(velocity);
            msgStr += "," + LOCATION_FLAG + ":" + VectorToString(location);
            msgStr = "{" + msgStr + "}";

            return msgStr;
        }

        public static String BuildPingEntityMessage(MyDetectedEntityInfo info, long entityid, int requestsSent)
        {
            String msgStr = "";

            var hitpos = info.HitPosition;

            msgStr += MESSAGETYPE_FLAG + ":" + PINGENTITY_FLAG;
            msgStr += "," + TARGETID_FLAG + ":" + info.EntityId;

            msgStr += "," + ENTITYID_FLAG + ":" + entityid;
            msgStr += "," + TARGETRADIUS_FLAG + ":" + (int)Math.Abs((info.BoundingBox.Min - info.BoundingBox.Max).Length());
            msgStr += "," + REQUESTID_FLAG + ":" + info.EntityId + requestsSent;
            msgStr += "," + TYPE_FLAG + ":" + info.Type.ToString();
            msgStr += "," + VELOCITY_FLAG + ":" + VectorToString(info.Velocity);
            msgStr += "," + LOCATION_FLAG + ":" + VectorToString(info.Position);
            msgStr += "," + RELATIONSHIP_FLAG + ":" + info.Relationship;
            msgStr += "," + NUMBOUNCES_FLAG + ":" + 0;
            msgStr += "," + NAME_FLAG + ":" + info.Name;


            if (hitpos.HasValue)
                msgStr += "," + ATTACKPOINT_FLAG + ":" + VectorToString(hitpos.Value);

            msgStr = "{" + msgStr + "}";

            return msgStr;
        }
 
        
    }
    //////
}
