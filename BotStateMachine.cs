using System.Collections.Generic;

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
        findAmmo,
        findHealth,
        findSnitch,
    }

    public enum MoveBehaviour
    {
        wander,
        moveToGoal,
        moveToSnitch
    }


    public class BotStateMachine
    {
        private readonly NickBot bot;
        private int goalThreshold = 2;
        private int ammoThreshold = 2;
        private int healthThreshold = 2;

        public TurretBehaviour CurrentTurretBehaviour { get; private set; }
        public MoveBehaviour CurrentMoveBehaviour { get; private set; }

        public BotStateMachine(NickBot bot)
        {
            this.bot = bot;
            CurrentMoveBehaviour = MoveBehaviour.wander;
            CurrentTurretBehaviour = TurretBehaviour.targetNearest;
        }


        public void Update(List<GameObjectState> currentVisibleObjects)
        {

            if (bot.ammo < ammoThreshold)
            {
                if (!CanSeeObject(currentVisibleObjects, "AmmoPickup"))
                {
                    CurrentTurretBehaviour = TurretBehaviour.findAmmo;
                }

            }

            if (bot.health < healthThreshold)
            {
                if (!CanSeeObject(currentVisibleObjects, "HealthPickup"))
                {
                    CurrentTurretBehaviour = TurretBehaviour.findHealth;
                }

            }


            if (CurrentMoveBehaviour == MoveBehaviour.wander)
            {
                if (bot.unbankedPoints > goalThreshold)
                    CurrentMoveBehaviour = MoveBehaviour.moveToGoal;
            }

             
            if(bot.ammo > ammoThreshold && bot.health > healthThreshold && bot.unbankedPoints < goalThreshold)
            {
                CurrentTurretBehaviour = TurretBehaviour.targetNearest;
                CurrentMoveBehaviour = MoveBehaviour.wander;
            }




        }

        private bool CanSeeObject(List<GameObjectState> visibleObjects, string type)
        {
            foreach (GameObjectState s in visibleObjects)
            {
                if (s.Type == type)
                    return true;
            }
            return false;
        }

    }

}