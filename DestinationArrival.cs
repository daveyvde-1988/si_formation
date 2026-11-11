using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Si_Formation
{
    public sealed partial class Formations
    {
        private static bool BuildDestinationArrivalLayout(CohesionDispatch context,List<Unit> nearby,List<Unit> ground,
            List<Unit> aircraft,Vector3 start,Vector3 end)
        {
            var settings=activeLayout.Settings;float scale=activeLayout.Scale;
            Vector3 forward=Flat(context.Anchor-context.Origin);
            if(context.Drag&&Flat(end-start).sqrMagnitude>=4)forward=Vector3.Cross(Flat(end-start).normalized,Vector3.up);
            if(forward.sqrMagnitude<.001f)forward=Vector3.forward;forward.Normalize();context.Heading=forward;
            Dictionary<Unit,Vector3> points=null;
            if(context.Drag&&Flat(end-start).sqrMagnitude>=4)
            {
                var ordered=new List<Unit>(nearby);var slots=new List<Vector3>();
                // Ask native code for its priority/assignment ordering on this local
                // cohort only. Suppress our outer dispatch's packing callback while
                // doing so; never mutate the full native selection list.
                var previous=activeLayout;bool built;
                activeLayout=null;
                try{built=Silica.LineFormation.BuildSlots(ordered,start,end,slots,out var nativePitch);}
                finally{activeLayout=previous;}
                if(built)points=SilicaFormationShared.CommanderLineLayout.Repack(ordered,slots,start,end,scale,settings.SizeInfluence,
                    (unit,desired)=>TerrainProjection.TryProject(unit,desired,out var point)?(Vector3?)point:null,out var pitch,true);
            }
            if(points==null||points.Count==0)points=GridSlots(nearby,context.Anchor,forward,settings,scale,out var gridPitch);
            context.Candidates.IntersectWith(points.Keys);
            if(context.Candidates.Count<2)return false;
            context.IgnoredGround=ground.Count-context.Candidates.Count;
            float rear=points.Min(item=>Vector3.Dot(Flat(item.Value-context.Anchor),forward)-Radius(item.Key));
            // Aircraft stay above the local group's rear, not the distant arrivals.
            AddAirFormationSlots(aircraft,points,context.Anchor,forward,settings,scale);
            var distant=ground.Where(unit=>FormationEligible(unit)&&!context.Candidates.Contains(unit)).ToList();
            if(distant.Count>0)
            {
                // TerrainProjection may snap a ground point laterally by 5m.
                // Reserve more than that in addition to each body's radius.
                float clearance=distant.Max(Radius)+8;
                Vector3 rearAnchor=context.Anchor+forward*(rear-clearance);
                var rearPoints=GridSlots(distant,rearAnchor,forward,settings,scale,out var distantPitch);
                foreach(var entry in rearPoints)points[entry.Key]=entry.Value;
            }
            // Kept separately from activeLayout.Points: the later full-selection
            // native drag callback must not overwrite this cohort's assignments.
            context.LocalPoints=points;
            return true;
        }
    }
}
