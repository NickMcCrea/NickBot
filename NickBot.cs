using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Simple
{
    /// <summary>
    /// Simple bot which performs some movements to demo API, then rotates the turret, turns the tank
    /// and moves towards the center circle at 0,0.
    /// </summary>
    public class NickBot
    {
        public bool teamMode = true;
        private volatile int messageCount;
        private string ipAddress = "127.0.0.1";
        //private string ipAddress = "192.168.44.103";

        private int port = 8052;
        public string tankName;
        public GameObjectState ourMostRecentState;
        private Random random;
        Dictionary<int, GameObjectState> seenObjects = new Dictionary<int, GameObjectState>();
        Dictionary<int, DateTime> lastStateReceived = new Dictionary<int, DateTime>();
        public int unbankedPoints;
        public int health;
        public int ammo;
        public Tank snitchCarrier;
        public bool hasSnitch = false;
        private BotStateMachine botStateMachine;
        //private DateTime pointTurretToTargetTime;
        private Dictionary<Action, DateTime> actionToLastPerformedMap = new Dictionary<Action, DateTime>();

        //Our TCP client.
        private TcpClient client;

        //Thread used to listen to the TCP connection, so main thread doesn't block
        private Thread listeningThread;

        //store incoming messages on the listening thread,
        //before transfering them safely onto main thread.
        private Queue<byte[]> incomingMessages;

        public bool BotQuit { get; internal set; }

        public NickBot()
        {
            random = new Random();

            tankName = RandomString(8);


            incomingMessages = new Queue<byte[]>();

            ConnectToTcpServer();

            //wait for a bit to allow connection to establish before proceeding.
            Thread.Sleep(5000);


            //send the create tank request.
            if (teamMode)
            {

                int dice = random.Next(1, 3);
                if (dice == 1)
                    tankName = "Team A: " + tankName;
                if (dice == 2)
                    tankName = "Team B: " + tankName;
            }

            SendMessage(MessageFactory.CreateTankMessage(tankName));

            botStateMachine = new BotStateMachine(this);

        }


        private void DoAPITest()
        {
            int millisecondSleepTime = 500;
            Thread.Sleep(millisecondSleepTime);

            SendMessage(MessageFactory.CreateZeroPayloadMessage(NetworkMessageType.toggleForward));
            Thread.Sleep(millisecondSleepTime);

            SendMessage(MessageFactory.CreateZeroPayloadMessage(NetworkMessageType.toggleReverse));
            Thread.Sleep(millisecondSleepTime);

            SendMessage(MessageFactory.CreateZeroPayloadMessage(NetworkMessageType.stopMove));


            SendMessage(MessageFactory.CreateZeroPayloadMessage(NetworkMessageType.toggleLeft));
            Thread.Sleep(millisecondSleepTime);


            SendMessage(MessageFactory.CreateZeroPayloadMessage(NetworkMessageType.toggleRight));
            Thread.Sleep(millisecondSleepTime);

            SendMessage(MessageFactory.CreateZeroPayloadMessage(NetworkMessageType.stopTurn));


            SendMessage(MessageFactory.CreateZeroPayloadMessage(NetworkMessageType.toggleTurretLeft));
            Thread.Sleep(millisecondSleepTime);

            SendMessage(MessageFactory.CreateZeroPayloadMessage(NetworkMessageType.toggleTurretRight));
            Thread.Sleep(millisecondSleepTime);

            SendMessage(MessageFactory.CreateZeroPayloadMessage(NetworkMessageType.stopTurret));


            SendMessage(MessageFactory.CreateZeroPayloadMessage(NetworkMessageType.fire));




        }

        private void ConnectToTcpServer()
        {
            try
            {
                //set up a TCP client on a background thread
                listeningThread = new Thread(new ThreadStart(ConnectAndListen));
                listeningThread.IsBackground = true;
                listeningThread.Start();
            }
            catch (Exception e)
            {
                Console.WriteLine("On client connect exception " + e);
            }
        }

        private void ConnectAndListen()
        {
            try
            {
                client = new TcpClient(ipAddress, port);


                // Get a stream object for reading 				
                using (NetworkStream stream = client.GetStream())
                {

                    while (client.Connected)
                    {

                        int type = stream.ReadByte();
                        int length = stream.ReadByte();

                        Byte[] bytes = new Byte[length];

                        //there's a JSON package
                        if (length > 0)
                        {
                            // Read incoming stream into byte arrary. 					
                            //stream.Read(bytes, 0, length);

                            bytes = GetPayloadDataFromStream(stream, length);

                            Byte[] byteArrayCopy = new Byte[length + 2];
                            bytes.CopyTo(byteArrayCopy, 2);
                            byteArrayCopy[0] = (byte)type;
                            byteArrayCopy[1] = (byte)length;

                            lock (incomingMessages)
                            {
                                incomingMessages.Enqueue(byteArrayCopy);
                            }
                        }
                        else
                        {

                            //no JSON
                            lock (incomingMessages)
                            {
                                byte[] zeroPayloadMessage = new byte[2];
                                zeroPayloadMessage[0] = (byte)type;
                                zeroPayloadMessage[1] = 0;
                                incomingMessages.Enqueue(zeroPayloadMessage);
                            }

                        }

                    }

                }

            }
            catch (SocketException socketException)
            {
                Console.WriteLine("Socket exception: " + socketException);
            }
        }

        private byte[] GetPayloadDataFromStream(NetworkStream stream, int length)
        {
            byte[] buffer = new byte[length];
            int read = 0;

            int chunk;
            while ((chunk = stream.Read(buffer, read, buffer.Length - read)) > 0)
            {
                read += chunk;
            }
            return buffer;

        }

        private void DecodeMessage(NetworkMessageType messageType, int payloadLength, byte[] bytes)
        {
            try
            {
                string jsonPayload = "";
                if (payloadLength > 0)
                {
                    var payload = new byte[payloadLength];
                    Array.Copy(bytes, 2, payload, 0, payloadLength);
                    jsonPayload = Encoding.ASCII.GetString(payload);
                }

                if (messageType == NetworkMessageType.test)
                {
                    //Console.WriteLine("TEST ACK RECEIVED");
                }

                if (messageType == NetworkMessageType.objectUpdate)
                {
                    messageCount++;
                    GameObjectState objectState = JsonConvert.DeserializeObject<GameObjectState>(jsonPayload);
                    //Console.WriteLine("ID: " + objectState.Id + " Type: " + objectState.Type + " Name: " + objectState.Name + " ---- " + objectState.X + "," + objectState.Y + " : " + objectState.Heading + " : " + objectState.TurretHeading);

                    if (objectState.Name == tankName)
                    {
                        ourMostRecentState = objectState;
                        //Console.WriteLine("ID: " + objectState.Id + " Type: " + objectState.Type + " Name: " + objectState.Name + " ---- " + objectState.X + "," + objectState.Y + " : " + objectState.Heading + " : " + objectState.TurretHeading);
                    }
                    else
                    {
                        if (seenObjects.ContainsKey(objectState.Id))
                        {
                            seenObjects[objectState.Id] = objectState;
                            lastStateReceived[objectState.Id] = DateTime.Now;
                        }
                        else
                        {
                            seenObjects.Add(objectState.Id, objectState);

                            if (!lastStateReceived.ContainsKey(objectState.Id))
                                lastStateReceived.Add(objectState.Id, DateTime.Now);
                            else
                            {
                                lastStateReceived[objectState.Id] = DateTime.Now;
                            }
                        }
                    }
                }

                if (messageType == NetworkMessageType.kill)
                {
                    unbankedPoints++;
                    Console.WriteLine("KILL CONFIRMED");
                }
                if (messageType == NetworkMessageType.snitchPickup)
                {
                    snitchCarrier = JsonConvert.DeserializeObject<Tank>(jsonPayload);
                    Console.WriteLine("SNITCH CARRIER DETECTED");

                }
                if (messageType == NetworkMessageType.enteredGoal)
                {
                    botStateMachine.TransitionTo(TurretBehaviour.findTarget);
                    botStateMachine.TransitionTo(MoveBehaviour.moveToRandomPoint);
                    unbankedPoints = 0;
                }

            }
            catch (Exception e)
            {
                Console.WriteLine("Message decode exception " + e);
                Console.WriteLine("Messaage Type: " + messageType.ToString());
                Console.WriteLine("Payload Length: " + payloadLength);
                Console.WriteLine("Message Length: " + bytes.Length);

            }

        }

        private void SendMessage(byte[] message)
        {
            if (client == null)
            {
                return;
            }
            try
            {
                // Get a stream object for writing. 			
                NetworkStream stream = client.GetStream();
                if (stream.CanWrite)
                {
                    stream.Write(message, 0, message.Length);

                }
            }
            catch (SocketException socketException)
            {
                Console.WriteLine("Socket exception: " + socketException);
            }
        }

        public void Update()
        {
            //Console.WriteLine("Incoming Message Queue Size: " + incomingMessages.Count);

            while (incomingMessages.Count > 0)
            {
                var nextMessage = incomingMessages.Dequeue();
                DecodeMessage((NetworkMessageType)nextMessage[0], nextMessage[1], nextMessage);
            }


            if (ourMostRecentState != null)
            {
                health = ourMostRecentState.Health;
                ammo = ourMostRecentState.Ammo;

                botStateMachine.Update(seenObjects);

                if (botStateMachine.CurrentTurretBehaviour == TurretBehaviour.aimAndFire)
                {

                    DoCommandAtFrequency(PointTurretToEnemy, 300);
                    DoCommandAtFrequency(Fire, 2000);
                }

                if (botStateMachine.CurrentMoveBehaviour == MoveBehaviour.moveTowardsTarget)
                    DoCommandAtFrequency(MoveToNearestEnemy, 1000);

                if (botStateMachine.CurrentTurretBehaviour == TurretBehaviour.lookAtAmmo)
                    DoCommandAtFrequency(PointTurretToAmmo, 300);

                if (botStateMachine.CurrentTurretBehaviour == TurretBehaviour.lookAtHealth)
                    DoCommandAtFrequency(PointTurretToHealth, 300);

                if (botStateMachine.CurrentMoveBehaviour == MoveBehaviour.moveToAmmo)
                    DoCommandAtFrequency(MoveToNearestAmmo, 1000);

                if (botStateMachine.CurrentMoveBehaviour == MoveBehaviour.moveToHealth)
                    DoCommandAtFrequency(MoveToNearestHealth, 1000);





                //we've lost any target, so spin around.
                if (botStateMachine.CurrentTurretBehaviour == TurretBehaviour.findTarget
                    || botStateMachine.CurrentTurretBehaviour == TurretBehaviour.findAmmo
                    || botStateMachine.CurrentTurretBehaviour == TurretBehaviour.findHealth)
                {
                    DoCommandAtFrequency(SpinTurretABit, 300);
                }


                if (botStateMachine.CurrentMoveBehaviour == MoveBehaviour.moveToRandomPoint)
                {
                    DoCommandAtFrequency(MoveToRandomPoint, 5000);
                }

                if (botStateMachine.CurrentMoveBehaviour == MoveBehaviour.moveToGoal)
                {
                    DoCommandAtFrequency(MoveToNearestGoal, 1000);
                }




                ClearOldObjectState();

            }
            DoCommandAtFrequency(() =>
            {
                //Console.WriteLine("Message count: " + messageCount);
                messageCount = 0;

            }, 1000);




        }

        private void SpinTurretABit()
        {
            Console.WriteLine("SPIN TURRET");
            float newHeading = (ourMostRecentState.TurretHeading) + 30 % 360;
            SendMessage(MessageFactory.CreateMovementMessage(NetworkMessageType.turnTurretToHeading, newHeading));
        }

        private void PointTurretToEnemy()
        {
            Console.WriteLine("POINT TURRET TO TARGET");
            PointToNearestType("Tank");
        }

        private void PointTurretToAmmo()
        {
            Console.WriteLine("POINT TURRET TO AMMO");
            PointToNearestType("AmmoPickup");
        }

        private void PointTurretToHealth()
        {
            Console.WriteLine("POINT TURRET TO HEALTH");
            PointToNearestType("HealthPickup");
        }

        private void PointToNearestType(string type)
        {
            GameObjectState nearest = IdentifyNearest(type);
            if (nearest != null)
            {
                //var heading = GetHeading(ourMostRecentState.X, ourMostRecentState.Y, nearest.X, nearest.Y);
                TurnTurretToPoint(nearest.X, nearest.Y);
            }
        }

        private void MoveToRandomPoint()
        {
            Console.WriteLine("MOVE");
            //pick a random point, turn to it, move to it.
            float x = GetRandomArenaXPoint();
            float y = GetRandomArenaYPoint();
            TurnTankBodyToPoint(x, y);
            MoveToPoint(x, y);
        }

        private void MoveToNearestAmmo()
        {
            Console.WriteLine("MOVE TO AMMO");
            GameObjectState nearest = IdentifyNearest("AmmoPickup");

            if (nearest == null)
                return;

            TurnTankBodyToPoint(nearest.X, nearest.Y);
            MoveToPoint(nearest.X, nearest.Y);

        }

        private void MoveToNearestEnemy()
        {
            Console.WriteLine("MOVE TO ENEMY");
            GameObjectState nearest = IdentifyNearest("Tank");
            if (nearest == null)
                return;


            TurnTankBodyToPoint(nearest.X, nearest.Y);
            MoveToPoint(nearest.X, nearest.Y);

        }

        private void MoveToNearestGoal()
        {
            Console.WriteLine("MOVING TO GOAL");
            if (ourMostRecentState.Y > 0)
            {
                TurnTankBodyToPoint(0, 105);
                MoveToPoint(0, 105);
            }
            else
            {
                TurnTankBodyToPoint(0, -105);
                MoveToPoint(0, -105);
            }
        }

        private void MoveToNearestHealth()
        {
            Console.WriteLine("MOVE TO HEALTH");
            GameObjectState nearest = IdentifyNearest("HealthPickup");

            if (nearest == null)
                return;

            TurnTankBodyToPoint(nearest.X, nearest.Y);
            MoveToPoint(nearest.X, nearest.Y);
        }

        private void Fire()
        {
            Console.WriteLine("FIRE");
            SendMessage(MessageFactory.CreateZeroPayloadMessage(NetworkMessageType.fire));
        }

        private void ClearOldObjectState()
        {
            //who have we not seen for a while?

            foreach (KeyValuePair<int, DateTime> kvp in lastStateReceived)
            {
                if ((DateTime.Now - kvp.Value).TotalSeconds > 3)
                {
                    if (seenObjects.ContainsKey(kvp.Key))
                        seenObjects.Remove(kvp.Key);
                }
            }

        }

        private float GetRandomArenaXPoint()
        {
            return random.Next(-60, 60);
        }

        private float GetRandomArenaYPoint()
        {
            return random.Next(-90, 90);
        }

        private void MoveToPoint(float randomX, float randomY)
        {
            float distance = CalculateDistance(ourMostRecentState.X, ourMostRecentState.Y, randomX, randomY);
            SendMessage(MessageFactory.CreateMovementMessage(NetworkMessageType.moveForwardDistance, distance));
        }

        private void TurnTankBodyToPoint(float randomX, float randomY)
        {
            float targetHeading2 = GetHeading(ourMostRecentState.X, ourMostRecentState.Y, randomX, randomY);
            SendMessage(MessageFactory.CreateMovementMessage(NetworkMessageType.turnToHeading, targetHeading2));
        }

        private void TurnTurretToPoint(float randomTurretX, float randomTurretY)
        {
            float targetHeading = GetHeading(ourMostRecentState.X, ourMostRecentState.Y, randomTurretX, randomTurretY);
            SendMessage(MessageFactory.CreateMovementMessage(NetworkMessageType.turnTurretToHeading, targetHeading));
        }

        public float CheckDistanceTo(float x, float y)
        {
            return CalculateDistance(ourMostRecentState.X, ourMostRecentState.Y, x, y);
        }

        private float CalculateDistance(float ownX, float ownY, float otherX, float otherY)
        {
            float headingX = otherX - ownX;
            float headingY = otherY - ownY;
            return (float)Math.Sqrt((headingX * headingX) + (headingY * headingY));
        }

        private void AimTurretToTargetHeading(float targetHeading)
        {
            float turretDiff = targetHeading - ourMostRecentState.TurretHeading;
            if (Math.Abs(turretDiff) < 5)
            {
                SendMessage(MessageFactory.CreateZeroPayloadMessage(NetworkMessageType.stopTurret));

            }
            else if (IsTurnLeft(ourMostRecentState.TurretHeading, targetHeading))
            {
                SendMessage(MessageFactory.CreateZeroPayloadMessage(NetworkMessageType.toggleTurretLeft));

            }
            else if (!IsTurnLeft(ourMostRecentState.TurretHeading, targetHeading))
            {
                SendMessage(MessageFactory.CreateZeroPayloadMessage(NetworkMessageType.toggleTurretRight));
            }
        }

        private float CheckAngle(float x1, float y1, float x2, float y2)
        {
            float heading = (float)Math.Atan2(y2 - y1, x2 - x1);
            heading = (float)RadianToDegree(heading);
            return heading;
        }

        private float GetHeading(float x1, float y1, float x2, float y2)
        {
            float heading = (float)Math.Atan2(y2 - y1, x2 - x1);
            heading = (float)RadianToDegree(heading);
            heading = (heading - 360) % 360;
            return Math.Abs(heading);

        }

        private double RadianToDegree(double angle)
        {
            return angle * (180.0 / Math.PI);
        }


        public bool OnSameTeam(string name, string otherName)
        {
            if (name.Contains(":") && otherName.Contains(":"))
            {
                string myTeamName = name.Split(':')[0].ToUpper().Trim();
                string otherTeamName = otherName.Split(':')[0].ToUpper().Trim();
                return myTeamName == otherTeamName;
            }
            return false;
        }

        public GameObjectState IdentifyNearest(string type)
        {


            float closestDist = float.MaxValue;
            GameObjectState closest = null;
            foreach (GameObjectState s in seenObjects.Values)
            {
                if (s.Name == tankName)
                    continue;

                //if teammode, don't target own tanks
                if (teamMode && type == "Tank")
                {
                    if (OnSameTeam(tankName, s.Name))
                        continue;

                }

                if (s.Type == type)
                {
                    float distance = CalculateDistance(ourMostRecentState.X, ourMostRecentState.Y, s.X, s.Y);
                    if (distance < closestDist)
                    {
                        closestDist = distance;
                        closest = s;
                    }
                }
            }
            return closest;
        }


        bool IsTurnLeft(float currentHeading, float desiredHeading)
        {
            float diff = desiredHeading - currentHeading;
            return diff > 0 ? diff > 180 : diff >= -180;
        }

        private string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        private DateTime lastRun;
        private bool DoCommandAtFrequency(Action command, int freqInMilliseconds)
        {

            if (!actionToLastPerformedMap.ContainsKey(command))
                actionToLastPerformedMap.Add(command, DateTime.Now);


            lastRun = actionToLastPerformedMap[command];

            var timeSinceLastRun = DateTime.Now - lastRun;
            if (timeSinceLastRun.TotalMilliseconds > freqInMilliseconds)
            {
                command();
                actionToLastPerformedMap[command] = DateTime.Now;
                return true;
            }
            return false;
        }

    }







}
