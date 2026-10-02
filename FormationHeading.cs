using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Silica.AI;

namespace Si_Formation
{
    public sealed partial class Formations
    {
        private static bool Reversed(Vector3 before,Vector3 after)
            =>Flat(before).sqrMagnitude>0.01f&&Flat(after).sqrMagnitude>0.01f&&Vector3.Dot(Heading(before),Heading(after))< -0.8660254f;
        private sealed class LockHeading
        {
            internal Player Owner;
            internal Team Team;
            internal AIGroup Group;
            internal Vector3 Forward;
        }
        private static readonly Dictionary<Unit,LockHeading> LockHeadings=new Dictionary<Unit,LockHeading>();
        private static void PruneLockHeadings()
        {
            foreach(var entry in LockHeadings.ToArray())
                if(!entry.Value.Owner||!Player.Players.Contains(entry.Value.Owner)||!entry.Value.Owner.IsCommander||
                    entry.Value.Owner.Team!=entry.Value.Team||!LockEligible(entry.Key,entry.Value.Owner)||entry.Key.AIGroup!=entry.Value.Group)
                    LockHeadings.Remove(entry.Key);
        }
        private static Vector3 PreviousLockHeading(Player owner,Unit[] units)
        {
            Vector3 sum=Vector3.zero;
            foreach(var u in units)
                sum+=LockHeadings.TryGetValue(u,out var memory)&&memory.Owner==owner&&memory.Team==u.Team&&memory.Group==u.AIGroup?
                    memory.Forward:Heading(u.transform.forward);
            return Heading(sum);
        }
    }
}
