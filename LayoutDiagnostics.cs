using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using UnityEngine;

namespace Si_Formation
{
    public sealed partial class Formations
    {
        private static bool ValidCapturedLayout(CohesionDispatch context,List<CohesionMember> members,bool watched)
        {
            for(int i=0;i<members.Count;i++)for(int j=i+1;j<members.Count;j++)
            {
                float minimum=Radius(members[i].Unit)+Radius(members[j].Unit)+.5f;
                if(FlatDistanceSq(members[i].Target,members[j].Target)+.001f>=minimum*minimum)continue;
                TraceLayoutFailure(context,"capture clearance",members[i].Unit,members[i].Target,members[i].Target,
                    members[j].Unit,members[j].Target,minimum,watched);
                return false;
            }
            return true;
        }
        private static void TraceLayoutFailure(CohesionDispatch context,string reason,Unit unit,Vector3 desired,Vector3? projected,
            Unit other=null,Vector3 otherPoint=default,float minimum=0,bool watched=false)
        {
            bool priorWatch=unit&&CohesionLayouts.TryGetValue(unit,out var prior)&&prior.Orders.Values.Any(member=>member.Group!=null&&member.Group.Watched);
            if(!CohesionDebugEnabled&&!watched&&!priorWatch&&!CohesionDebugNext.Contains(context.Owner)&&activeMovementDiagnostics?.Watched!=true)return;
            string detail="[Formation layout] "+reason+" unit="+MovementUnitName(unit)+" desired="+MovementPoint(desired)+
                " projected="+(projected.HasValue?MovementPoint(projected.Value):"unavailable");
            if(projected.HasValue)detail+=" snap="+MovementNumber(Vector3.Distance(Flat(desired),Flat(projected.Value)))+"m";
            if(other)detail+=" other="+MovementUnitName(other)+" otherPoint="+MovementPoint(otherPoint)+
                " separation="+MovementNumber(Vector3.Distance(Flat(projected??desired),Flat(otherPoint)))+
                "m minimum="+MovementNumber(minimum)+"m";
            MelonLogger.Msg(detail);
        }
    }
}
