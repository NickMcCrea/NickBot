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
        targetNearest,
        findTarget,
        findAmmo,
        findHealth,
        findSnitch,
    }

    public enum MoveBehaviour
    {
        moveToRandomPoint,
        moveToHealth,
        moveToAmmo,
        moveToGoal,
        moveToSnitch
    }


    public class BotStateMachine
    {
        private readonly NickBot bot;
        private int goalThreshold = 2;
        private int ammoThreshold = 2;
        private int healthThreshold = 2;
        private bool targetAvailable = false;
        public TurretBehaviour CurrentTurretBehaviour { get; private set; }
        public MoveBehaviour CurrentMoveBehaviour { get; private set; }
        DateTime randomPointMove;

        Dictionary<TurretBehaviour, bool> justTransitionedTurret = new Dictionary<TurretBehaviour, bool>();
        Dictionary<MoveBehaviour, bool> justTransitionedMove = new Dictionary<MoveBehaviour, bool>();

        public BotStateMachine(NickBot bot)
        {
            this.bot = bot;
        
            justTransitionedMove.Add(MoveBehaviour.moveToGoal, false);
            justTransitionedMove.Add(MoveBehaviour.moveToSnitch, false);
            justTransitionedMove.Add(MoveBehaviour.moveToRandomPoint, false);
            justTransitionedTurret.Add(TurretBehaviour.findAmmo, false);
            justTransitionedTurret.Add(TurretBehaviour.findHealth, false);
            justTransitionedTurret.Add(TurretBehaviour.findSnitch, false);
            justTransitionedTurret.Add(TurretBehaviour.findTarget, false);
            justTransitionedTurret.Add(TurretBehaviour.targetNearest, false);

            ReTransitionTo(MoveBehaviour.moveToRandomPoint);
            ReTransitionTo(TurretBehaviour.findTarget);

            randomPointMove = DateTime.Now;

        }


        public void Update(Dictionary<int, GameObjectState> currentVisibleObjects)
        {

            if (CurrentTurretBehaviour == TurretBehaviour.targetNearest)
            {
                if (!CanSeeObject(currentVisibleObjects, "Tank"))
                {
                    TransitionTo(TurretBehaviour.findTarget);
                    targetAvailable = false;
                }
            }
            if (CurrentTurretBehaviour == TurretBehaviour.findTarget)
            {
                if (CanSeeObject(currentVisibleObjects, "Tank"))
                {
                    targetAvailable = true;
                    TransitionTo(TurretBehaviour.targetNearest);
                }
            }
            //if (bot.ammo < ammoThreshold)
            //{
            //    if (!CanSeeObject(currentVisibleObjects, "AmmoPickup"))
            //    {
            //        TransitionTo(TurretBehaviour.findAmmo);
            //    }

            //}

            //if (bot.health < healthThreshold)
            //{
            //    if (!CanSeeObject(currentVisibleObjects, "HealthPickup"))
            //    {
            //        TransitionTo(TurretBehaviour.findHealth);
            //    }

            //}


            //if (CurrentMoveBehaviour == MoveBehaviour.moveToRandomPoint)
            //{
            //    if (bot.unbankedPoints > goalThreshold)
            //        TransitionTo(MoveBehaviour.moveToGoal);

            
            //}


            //if (bot.ammo > ammoThreshold && bot.health > healthThreshold && bot.unbankedPoints < goalThreshold)
            //{

            //    if (targetAvailable)
            //        TransitionTo(TurretBehaviour.targetNearest);
            //    if (!targetAvailable)
            //        TransitionTo(TurretBehaviour.findTarget);

            //    TransitionTo(MoveBehaviour.moveToRandomPoint);
            //}


           



        }

        private void TransitionTo(TurretBehaviour behaviour)
        {

            if (CurrentTurretBehaviour == behaviour)
                return;

            Console.WriteLine("TURRET: " + behaviour);
            CurrentTurretBehaviour = behaviour;

            //set all to false, then the one we want to true.
            foreach (var key in justTransitionedTurret.Keys.ToList())
            {
                justTransitionedTurret[key] = false;
            }

            justTransitionedTurret[behaviour] = true;


        }

        private void TransitionTo(MoveBehaviour behaviour)
        {

            if (CurrentMoveBehaviour == behaviour)
                return;

            Console.WriteLine("MOVE: " + behaviour);
            CurrentMoveBehaviour = behaviour;

            foreach (var key in justTransitionedMove.Keys.ToList())
            {
                justTransitionedMove[key] = false;
            }

            justTransitionedMove[behaviour] = true;
        }

        private void ReTransitionTo(TurretBehaviour behaviour)
        {

            Console.WriteLine("TURRET: " + behaviour);
            CurrentTurretBehaviour = behaviour;

            //set all to false, then the one we want to true.
            foreach (var key in justTransitionedTurret.Keys.ToList())
            {
                justTransitionedTurret[key] = false;
            }

            justTransitionedTurret[behaviour] = true;


        }

        private void ReTransitionTo(MoveBehaviour behaviour)
        {
            Console.WriteLine("MOVE: " + behaviour);
            CurrentMoveBehaviour = behaviour;

            foreach (var key in justTransitionedMove.Keys.ToList())
            {
                justTransitionedMove[key] = false;
            }

            justTransitionedMove[behaviour] = true;
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