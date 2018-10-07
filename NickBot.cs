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


        private string ipAddress = "127.0.0.1";
        private int port = 8052;
        private string tankName;
        private GameObjectState ourMostRecentState;
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
                            stream.Read(bytes, 0, length);

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
                    GameObjectState objectState = JsonConvert.DeserializeObject<GameObjectState>(jsonPayload);
                    //Console.WriteLine("ID: " + objectState.Id + " Type: " + objectState.Type + " Name: " + objectState.Name + " ---- " + objectState.X + "," + objectState.Y + " : " + objectState.Heading + " : " + objectState.TurretHeading);

                    if (objectState.Name == tankName)
                        ourMostRecentState = objectState;
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
                }
                if (messageType == NetworkMessageType.snitchPickup)
                {
                    snitchCarrier = JsonConvert.DeserializeObject<Tank>(jsonPayload);
                }

            }
            catch (Exception e)
            {
                Console.WriteLine("Message decode exception " + e);
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
            botStateMachine.Update(seenObjects);

            if (incomingMessages.Count > 0)
            {
                var nextMessage = incomingMessages.Dequeue();
                DecodeMessage((NetworkMessageType)nextMessage[0], nextMessage[1], nextMessage);
            }


            if (ourMostRecentState != null)
            {
                health = ourMostRecentState.Health;
                ammo = ourMostRecentState.Ammo;


                if (botStateMachine.CurrentTurretBehaviour == TurretBehaviour.targetNearest)
                {

                    DoCommandAtFrequency(PointTurretToTarget, 300);
                    DoCommandAtFrequency(Fire, 2000);

                }


                //we've lost any target, so spin around.
                if (botStateMachine.CurrentTurretBehaviour == TurretBehaviour.findTarget)
                {
                    DoCommandAtFrequency(() =>
                    {
                        Console.WriteLine("SPIN TURRET");
                        SendMessage(MessageFactory.CreateZeroPayloadMessage(NetworkMessageType.toggleTurretLeft));
                    }, 1000);

                }
                

                if (botStateMachine.CurrentMoveBehaviour == MoveBehaviour.moveToRandomPoint)  
                {
                    DoCommandAtFrequency(MoveToRandomPoint, 3000);
                }


                ClearOldObjectState();

            }
        }

        private void PointTurretToTarget()
        {
            Console.WriteLine("POINT TURRET TO TARGET");
            GameObjectState nearestTank = IdentifyNearest("Tank");
            var heading = GetHeading(ourMostRecentState.X, ourMostRecentState.Y, nearestTank.X, nearestTank.Y);
            TurnTurretToPoint(nearestTank.X, nearestTank.Y);
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

        private float CheckDistanceTo(float x, float y)
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

        private GameObjectState IdentifyNearest(string type)
        {
            float closestDist = float.MaxValue;
            GameObjectState closest = null;
            foreach (GameObjectState s in seenObjects.Values)
            {
                if (s.Name == tankName)
                    continue;

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
            if(timeSinceLastRun.TotalMilliseconds > freqInMilliseconds)
            {
                command();
                actionToLastPerformedMap[command] = DateTime.Now;
                return true;
            }
            return false;
        }

    }







}
