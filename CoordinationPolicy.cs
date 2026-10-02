using System;

namespace Si_Formation
{
    // Pure policy shared by live coordination and executable regression checks.
    internal static class CoordinationPolicy
    {
        internal static bool NeedsSpeedCap(float maximum,float slowest)=>slowest>0&&maximum>slowest+0.001f;
        internal static float Tolerance(float radius,float spacing,float percent)
            =>Math.Max(Math.Max(4,radius*1.5f),spacing*percent/100f);
        internal static bool Coordinated(float distance,float limit)=>distance<=limit;
        internal static bool Arrived(float issuedTargetToFinalSquared,float distanceToIssuedSquared,float arrivalRadius)
            =>issuedTargetToFinalSquared<=25f&&distanceToIssuedSquared<=arrivalRadius*arrivalRadius;
        internal static float AccelerationPace(float previous,float requested,float slowestObserved,float elapsed)
        {
            // Small headroom lets a stationary group start; otherwise zero speed would be a deadlock.
            // Follow measured progress instead of equating unrelated wheeled/hover acceleration fields.
            float target=Math.Min(requested,Math.Max(0,slowestObserved)+0.75f);
            return Math.Min(target,Math.Max(0,previous)+Math.Max(0,elapsed)*1.5f);
        }
        internal static float MemberPace(float groupPace,float maximum,bool recoveringStraggler,float boost,float longitudinal,float tolerance)
        {
            if(recoveringStraggler)return maximum*Math.Min(1.1f,Math.Max(1,boost));
            float pace=Math.Min(maximum,groupPace);
            // A unit ahead of its own slot must wait for the reference to catch up.
            // Matching maximum speeds alone would preserve an existing lead indefinitely.
            if(longitudinal < -tolerance*0.25f)
                pace*=Math.Max(0,1+(longitudinal+tolerance*0.25f)/Math.Max(1,tolerance));
            return pace;
        }
        internal static float RecoveryThreshold(float trigger)=>trigger*3f/5f;
        internal static bool UpdateRecovery(float outside,float trigger,float now,ref bool recovering,ref float started)
        {
            if(!recovering&&outside>trigger){recovering=true;started=now;}
            bool timedOut=recovering&&now-started>=10;
            if(recovering&&(outside<RecoveryThreshold(trigger)||timedOut))recovering=false;
            return timedOut;
        }
        internal static float NextPace(float pace,bool recovering,float elapsed)
        {
            float target=recovering?0.5f:1f,step=Math.Max(0,elapsed)*0.25f;
            return pace<target?Math.Min(target,pace+step):Math.Max(target,pace-step);
        }
    }
}
