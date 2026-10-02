using System;
using System.Linq;
using UnityEngine;

namespace Si_Formation
{
    public sealed partial class Formations
    {
        private static void ApplyCentreCaps(LockedGroup g)
        {
            if(!g.Traveling||g.Members.Count<2)return;
            // Each position minus its captured slot estimates the same formation centre.
            // Subtracting slots preserves the reference when membership changes.
            Vector3 reference=Vector3.zero;
            foreach(var m in g.Members)reference+=Flat(m.Unit.transform.position)-Slot(g,m);
            reference/=g.Members.Count;
            foreach(var m in g.Members)
            {
                if(!m.HasDestination)continue;
                float arrival=Mathf.Clamp(m.Radius*0.5f,3f,8f);
                if(DistanceSq(m.Unit.transform.position,m.LastDestination)<=arrival*arrival)continue;
                float error=Vector3.Dot(reference+Slot(g,m)-Flat(m.Unit.transform.position),g.TravelForward);
                float tolerance=Math.Max(4,m.Radius*1.5f);
                // Signed error: ahead can only brake; behind can catch up to its own
                // maximum. Lateral detours never become a distance-based speed boost.
                float correction=error>tolerance?(error-tolerance)*0.5f:error< -tolerance?(error+tolerance)*0.5f:0;
                float cap=Mathf.Clamp(g.Baseline+correction,0,m.Maximum);
                // Centre pacing owns the travel ceiling; native lower limits still win.
                LockedSpeedCaps[m.Unit]=cap;
            }
        }
    }
}
