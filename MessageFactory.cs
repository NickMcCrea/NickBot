using Newtonsoft.Json;
using System.Text;

namespace Simple
{


    public static class MessageFactory
    {

        public static byte[] CreateTankMessage(string name)
        {

            string json = JsonConvert.SerializeObject(new { Name = name });
            byte[] clientMessageAsByteArray = Encoding.ASCII.GetBytes(json);
            return AddTypeAndLengthToArray(clientMessageAsByteArray, (byte)NetworkMessageType.createTank);
        }

        public static byte[] CreateMovementMessage(NetworkMessageType type, float amount)
        {
            string json = JsonConvert.SerializeObject(new { Amount = amount });
            byte[] clientMessageAsByteArray = Encoding.ASCII.GetBytes(json);
            return AddTypeAndLengthToArray(clientMessageAsByteArray, (byte)type);
        }

        public static byte[] AddTypeAndLengthToArray(byte[] bArray, byte type)
        {
            byte[] newArray = new byte[bArray.Length + 2];
            bArray.CopyTo(newArray, 2);
            newArray[0] = type;
            newArray[1] = (byte)bArray.Length;
            return newArray;
        }

        public static byte[] CreateZeroPayloadMessage(NetworkMessageType type)
        {

            byte[] message = new byte[2];
            message[0] = (byte)type;
            message[1] = 0;
            return message;
        }


    }


    public enum NetworkMessageType
    {
        test = 0,
        createTank = 1,
        despawnTank = 2,
        fire = 3,
        toggleForward = 4,
        toggleReverse = 5,
        toggleLeft = 6,
        toggleRight = 7,
        toggleTurretLeft = 8,
        toggleTurretRight = 9,
        turnTurretToHeading = 10,
        turnToHeading = 11,
        moveForwardDistance = 12,
        moveBackwardsDistance = 13,
        stopAll = 14,
        stopTurn = 15,
        stopMove = 16,
        stopTurret = 17,
        objectUpdate = 18,
        healthPickup = 19,
        ammoPickup = 20,
        snitchPickup = 21,
        destroyed = 22,
        enteredGoal = 23,
        kill = 24

    }

    public class GameObjectState
    {
        public int Id;
        public string Name;
        public string Type;
        public float X;
        public float Y;
        public float Heading;
        public float TurretHeading;
        public int Health;
        public int Ammo;
    }

    public class Tank
    {
        public int Id;
    }

}