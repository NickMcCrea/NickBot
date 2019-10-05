using System;
using System.Collections.Generic;
using System.Linq;

namespace Simple
{

    /*
     * 
     * Overall behaviour we want here is fairly simple.
     * 
     * Turret should be always aiming + firing at the nearest enemy. No downside to doing so, other than ammo.
     * Should also be 'radar sweeps' to get max knowledge of what's out there.
     * Snitch carriers should be preferentially targeted
     * 
     * 
     * Movement strategy is more complex:
     * 
     * Generally keep moving. Maybe a wander? Maybe something like the snitch behaviour? Wander, with tank avoid?
     * 
     * If we get unbanked points above a certain threshold, we should aim for the goal.
     * 
     * If the snitch is detected go for it.
     * 
     */


    public enum TurretBehaviour
    {
        aimAndFire,
        findTarget,
        findAmmo,
        lookAtAmmo,
        findHealth,
        lookAtHealth,
        findSnitch,
    }

    public enum MoveBehaviour
    {
        moveTowardsTarget,
        moveToRandomPoint,
        moveToHealth,
        moveToAmmo,
        moveToGoal,
        moveToSnitch
    }


    public class BotStateMachine
    {
        private readonly NickBot bot;
        private int goalThreshold = 1;
        private int ammoThreshold = 2;
        private int healthThreshold = 2;
        public TurretBehaviour CurrentTurretBehaviour { get; private set; }
        public MoveBehaviour CurrentMoveBehaviour { get; private set; }
        DateTime randomPointMove;


        public BotStateMachine(NickBot bot)
        {
            this.bot = bot;



            TransitionTo(MoveBehaviour.moveToRandomPoint);
            TransitionTo(TurretBehaviour.findTarget);

            randomPointMove = DateTime.Now;

        }


        public void Update(Dictionary<int, GameObjectState> currentVisibleObjects)
        {

            if (CurrentTurretBehaviour == TurretBehaviour.aimAndFire)
            {
                if (!CanSeeObject(currentVisibleObjects, "Tank"))
                {
                    TransitionTo(TurretBehaviour.findTarget);

                }

            }

            if (CurrentMoveBehaviour == MoveBehaviour.moveToRandomPoint)
            {
                if (CurrentTurretBehaviour == TurretBehaviour.aimAndFire)
                {
                    float distance = CheckDistanceToNearestEnemy();
                    if (distance > 60)
                    {
                        TransitionTo(MoveBehaviour.moveTowardsTarget);
                    }
                }

            }

            if (CurrentMoveBehaviour == MoveBehaviour.moveTowardsTarget)
            {
                if (!CanSeeObject(currentVisibleObjects, "Tank"))
                {
                    TransitionTo(MoveBehaviour.moveToRandomPoint);
                }
                else
                {
                    float distance = CheckDistanceToNearestEnemy();
                    if (distance < 40)
                    {
                        TransitionTo(MoveBehaviour.moveToRandomPoint);
                    }
                }
            }

            if (CurrentTurretBehaviour == TurretBehaviour.findTarget)
            {
                if (CanSeeObject(currentVisibleObjects, "Tank"))
                {
                    TransitionTo(TurretBehaviour.aimAndFire);
                }
            }

            if (CurrentTurretBehaviour == TurretBehaviour.findAmmo)
            {
                if (CanSeeObject(currentVisibleObjects, "AmmoPickup"))
                {
                    TransitionTo(TurretBehaviour.lookAtAmmo);
                }
            }
            if (CurrentTurretBehaviour == TurretBehaviour.findHealth)
            {
                if (CanSeeObject(currentVisibleObjects, "HealthPickup"))
                {
                    TransitionTo(TurretBehaviour.lookAtHealth);
                }
            }




            if (CurrentMoveBehaviour == MoveBehaviour.moveToAmmo)
            {
                if (bot.ammo >= ammoThreshold)
                {
                    TransitionTo(MoveBehaviour.moveToRandomPoint);
                }
            }
            if (CurrentTurretBehaviour == TurretBehaviour.lookAtAmmo || CurrentTurretBehaviour == TurretBehaviour.findAmmo)
            {
                if (bot.ammo >= ammoThreshold)
                {
                    TransitionTo(TurretBehaviour.findTarget);
                }
            }



            if (CurrentMoveBehaviour == MoveBehaviour.moveToHealth)
            {
                if (bot.health >= healthThreshold)
                {
                    TransitionTo(MoveBehaviour.moveToRandomPoint);
                }
            }
            if (CurrentTurretBehaviour == TurretBehaviour.lookAtHealth || CurrentTurretBehaviour == TurretBehaviour.findHealth)
            {
                if (bot.health >= healthThreshold)
                {
                    TransitionTo(TurretBehaviour.findTarget);
                }
            }


            if (bot.ammo < ammoThreshold)
            {
                //if you can't see ammo, wander and look around.
                if (!CanSeeObject(currentVisibleObjects, "AmmoPickup"))
                {

                    TransitionTo(TurretBehaviour.findAmmo);
                    TransitionTo(MoveBehaviour.moveToRandomPoint);
                }
                else //if you can, move towards it.
                {
                    TransitionTo(TurretBehaviour.lookAtAmmo);
                    TransitionTo(MoveBehaviour.moveToAmmo);
                }

            }


            if (bot.health < healthThreshold)
            {
                //if you can't see health, wander and look around.
                if (!CanSeeObject(currentVisibleObjects, "HealthPickup"))
                {

                    TransitionTo(TurretBehaviour.findHealth);
                    TransitionTo(MoveBehaviour.moveToRandomPoint);
                }
                else //if you can, move towards it.
                {
                    TransitionTo(TurretBehaviour.lookAtHealth);
                    TransitionTo(MoveBehaviour.moveToHealth);
                }
            }


            if (bot.unbankedPoints >= goalThreshold)
            {
                TransitionTo(MoveBehaviour.moveToGoal);
            }


        }

        private float CheckDistanceToNearestEnemy()
        {
            GameObjectState nearest = bot.IdentifyNearest("Tank");

            if (nearest == null)
                return 0;

            float distance = bot.CheckDistanceTo(nearest.X, nearest.Y);
            return distance;
        }

        public void TransitionTo(TurretBehaviour behaviour)
        {

            if (CurrentTurretBehaviour == behaviour)
                return;

            //Console.WriteLine(bot.tankName + "  TURRET: " + behaviour);
            CurrentTurretBehaviour = behaviour;




        }

        public void TransitionTo(MoveBehaviour behaviour)
        {

            if (CurrentMoveBehaviour == behaviour)
                return;

            //Console.WriteLine(bot.tankName + "  MOVE: " + behaviour);
            CurrentMoveBehaviour = behaviour;


        }


        private bool CanSeeObject(Dictionary<int, GameObjectState> visibleObjects, string type)
        {
            foreach (GameObjectState s in visibleObjects.Values)
            {
                if (s.Type == type)
                    return true;
            }
            return false;
        }

    }

}