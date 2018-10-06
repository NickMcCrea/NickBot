namespace Simple
{

    /*
     * 
     * Overall behaviour we want here is fairly simple.
     * 
     * Turret should be always aiming + firing at the nearest enemy. No downside to doing so, other than ammo.
     * Should also be 'radar sweeps' to get max knowledge of what's out there.
     * 
     * 
     * Movement strategy is more complex:
     * 
     * Generally keep moving. Maybe a wander? Maybe something like the snitch behaviour? Wander, with tank avoid?
     * 
     * If we get unbanked points above a certain threshold, we should aim for the goal.
     * If we get 
     * 
     */

    public enum StrategyStates
    {
        justSpawned,
        killMode,
        goalSeek,
        ammoSeek,
        healthSeek
    }

    public enum HealthState
    {
        alive,
        dead
    }

    public class BotStateMachine
    {

        public BotStateMachine()
        {

        }

    }

}