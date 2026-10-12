using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using MelonLoader;
using Silica.AI;
using UnityEngine;

namespace Si_Formation
{
    public sealed partial class Formations
    {
        private static MelonPreferences_Entry<bool> rallyFixDefault;
        private static Structure productionRallyFactory;
        private static Vehicle exitingRallyVehicle;
        private static bool issuingRally,movingRallyMarker;
        private static IEnumerator<bool> rallySearch;
        private static RallyTicket searchingRallyTicket;
        private static float nextRallyPrune;
        private static long rallySerial;
        private sealed class RallyFactory
        {
            internal Structure Factory;
            internal Vector3 DefaultPoint,RequestedPoint,Marker;
            internal bool Explicit,OwnsMarker;
            internal RallyArea Area;
        }
        private sealed class RallyArea
        {
            internal RallyFactory State;
            internal Team Team;
            internal Vector3 Anchor,Forward;
            internal string Shape;
            internal float Spacing,Radius;
            internal bool Automatic;
            internal IEnumerator<Vector3> Positions;
            internal RallySpot Preview;
        }
        private sealed class RallyTicket
        {
            internal Unit Unit;
            internal RallyArea Area;
            internal RallySpot Spot;
            internal float Created;
            internal long Serial;
            internal bool Assigned,Arrived;
        }
        private sealed class RallySpot {internal Vector3 Point;}
        private static readonly Dictionary<Unit,RallyTicket> RallyTickets=new Dictionary<Unit,RallyTicket>();
        private static readonly Dictionary<Structure,RallyFactory> RallyFactories=new Dictionary<Structure,RallyFactory>();
        private static bool RallyEnabled=>Running&&(rallyFixDefault?.Value??false);

        private void InstallRallyFix()
        {
            rallyFixDefault=prefs.CreateEntry("Rallyfix",true,description:"Growing faction-shaped production parking; /formation rallyfix on|off saves this setting");
            Patch(typeof(Structure),"OnInit",postfix:nameof(RallyFactoryInitialized));
            Patch(typeof(Structure),"MoveRallyPoint",postfix:nameof(RallyFactoryChanged));
            Patch(typeof(ConstructionSite),"SpawnObject",prefix:nameof(RallySiteBegin),postfix:nameof(RallyProducedSite),transpiler:nameof(RallySiteSpawn),finalizer:nameof(RallyProductionEnd));
            Patch(typeof(Structure),"ConstructNonPlaceableInstant",prefix:nameof(RallyInstantBegin),transpiler:nameof(RallyInstantSpawn),finalizer:nameof(RallyProductionEnd));
            Patch(typeof(Unit),"RallyTo",prefix:nameof(RallyNativeDestination));
            Patch(typeof(Vehicle),"RallyTo",prefix:nameof(RallyNativeDestination));
            Patch(typeof(Vehicle),"ClearForceMove",prefix:nameof(RallyExitBegin),finalizer:nameof(RallyExitEnd));
        }
        private static void RallyFactoryInitialized(Structure __instance)
        {
            if(!Game.GetIsServer())return;
            RallyFactories[__instance]=new RallyFactory {Factory=__instance,DefaultPoint=__instance.RallyPoint,RequestedPoint=__instance.RallyPoint};
        }
        private static RallyFactory RallyState(Structure factory)
        {
            if(RallyFactories.TryGetValue(factory,out var state))return state;
            Vector3 defaultPoint=factory.ProductionUnitMoveToTransform?factory.ProductionUnitMoveToTransform.position:Vector3.zero;
            bool explicitPoint=factory.RallyPoint!=Vector3.zero&&DistanceSq(factory.RallyPoint,defaultPoint)>1;
            state=new RallyFactory {Factory=factory,DefaultPoint=defaultPoint,RequestedPoint=factory.RallyPoint,Explicit=explicitPoint};
            RallyFactories.Add(factory,state);return state;
        }
        private static void RallyFactoryChanged(Structure __instance,Vector3 position)
        {
            if(movingRallyMarker||!Game.GetIsServer())return;
            var state=RallyState(__instance);
            state.RequestedPoint=position;
            state.Explicit=position!=Vector3.zero&&DistanceSq(position,state.DefaultPoint)>1;
            state.OwnsMarker=false;
            if(state.Area!=null){state.Area.Preview=null;state.Area.Positions.Dispose();state.Area=null;}
            // Existing produced units keep their assigned/captured destinations.
            Trace("Rallyfix new commander centre: "+__instance.name+" at "+position);
        }
        private static void RallySiteBegin(ConstructionSite __instance,out Structure __state)
        {__state=productionRallyFactory;productionRallyFactory=__instance.ParentStructure;}
        private static void RallyInstantBegin(Structure __instance,out Structure __state)
        {__state=productionRallyFactory;productionRallyFactory=__instance;}
        private static Exception RallyProductionEnd(Exception __exception,Structure __state)
        {productionRallyFactory=__state;return __exception;}
        private static IEnumerable<CodeInstruction> RallySiteSpawn(IEnumerable<CodeInstruction> instructions)
            =>ReplaceRallyProduced(instructions,nameof(RallySiteProduced));
        private static IEnumerable<CodeInstruction> RallyInstantSpawn(IEnumerable<CodeInstruction> instructions)
            =>ReplaceRallyProduced(instructions,nameof(RallyInstantProduced));
        private static IEnumerable<CodeInstruction> ReplaceRallyProduced(IEnumerable<CodeInstruction> instructions,string replacement)
        {
            var produced=AccessTools.Method(typeof(Unit),nameof(Unit.OnProduced));int count=0;
            foreach(var instruction in instructions)
            {
                if(instruction.Calls(produced))
                {instruction.opcode=OpCodes.Call;instruction.operand=AccessTools.Method(typeof(Formations),replacement);count++;}
                yield return instruction;
            }
            if(count!=1)throw new InvalidOperationException("Expected one production OnProduced call: "+replacement);
        }
        private static void RallySiteProduced(Unit unit,ConstructionSite site)
        {unit.OnProduced(site);QueueRally(unit,site?site.ParentStructure:productionRallyFactory);}
        private static void RallyInstantProduced(Unit unit,ConstructionSite site)
        {unit.OnProduced(site);QueueRally(unit,productionRallyFactory);}
        private static void RallyProducedSite(ConstructionSite __instance)
        {
            // Also observe factories without a native forced-exit transform.
            if(__instance.SpawnedGameObject is Unit unit&&!RallyTickets.ContainsKey(unit))QueueRally(unit,__instance.ParentStructure);
        }
        private static bool RallyNativeDestination(Unit __instance)
        {
            // Each unit gets its own destination asynchronously. Do not let the native
            // shared marker send a second vehicle to a spot already claimed at spawn.
            return !RallyEnabled||issuingRally||!productionRallyFactory||!RallyTickets.ContainsKey(__instance);
        }
        private static void RallyExitBegin(Vehicle __instance,out Vehicle __state)
        {__state=exitingRallyVehicle;exitingRallyVehicle=__instance;}
        private static Exception RallyExitEnd(Exception __exception,Vehicle __state)
        {exitingRallyVehicle=__state;return __exception;}
        private static bool RallyTierSpacing(Structure factory,out float spacing)
        {
            spacing=0;
            if(!factory||!factory.ObjectInfo||factory.ObjectInfo.StructureType!=StructureType.Production)return false;
            switch(factory.ObjectInfo.StructureSelectionType)
            {
                case StructureSelectionType.Units1:spacing=5;break;
                case StructureSelectionType.Units2:spacing=15;break;
                case StructureSelectionType.Units3:
                    var definition=Catalog.Definitions.Values.FirstOrDefault(f=>f.Team==TeamName(factory.Team)&&f.Function=="commander"&&f.MenuOrder==2);
                    spacing=Mathf.Max(20,definition?.Scale??30);break;
                case StructureSelectionType.Units4:spacing=30;break;
                default:return false;
            }
            return true;
        }
        private static float RallyRadius(Unit unit)=>RallyBoundsRadius(unit.PhysicalBounds.size);
        private static float RallyBoundsRadius(Vector3 size)
            =>Mathf.Max(0.5f,Mathf.Sqrt(size.x*size.x+size.z*size.z)*0.5f)+0.5f;
        private static RallyArea CreateRallyArea(RallyFactory state,Unit unit,float spacing)
        {
            var factory=state.Factory;
            float radius=RallyRadius(unit);
            // Uniform spacing for this facility's complete ground-unit roster avoids
            // changing the shape when a differently sized unit comes out next.
            foreach(var option in factory.ConstructionOptions)
                if(option&&option.ObjectInfo&&option.ObjectInfo.ObjectType==ObjectInfoType.Unit)
                    radius=Mathf.Max(radius,RallyBoundsRadius(option.ObjectInfo.PhysicalBounds.size));
            Vector3 exit=factory.ProductionUnitMoveToTransform?factory.ProductionUnitMoveToTransform.position:factory.transform.position;
            Vector3 forward=Heading(exit-factory.transform.position);
            if(DistanceSq(exit,factory.transform.position)<1)forward=Heading(factory.transform.forward);
            var area=new RallyArea {State=state,Team=unit.Team,Forward=forward,Shape=TeamName(unit.Team),Automatic=!state.Explicit,
                Radius=radius,Spacing=Mathf.Max(spacing,radius*2)};
            area.Anchor=area.Automatic?factory.transform.position+forward*(75+factory.PhysicalRadius+radius):state.RequestedPoint;
            area.Positions=RallyOffsets(area.Shape).GetEnumerator();
            return area;
        }
        private static void QueueRally(Unit unit,Structure factory)
        {
            try
            {
                if(!RallyEnabled||!unit||unit.IsFlyingType||TeamName(unit.Team)==null||!RallyTierSpacing(factory,out float spacing))return;
                ReleaseRally(unit);
                var state=RallyState(factory);
                if(state.Area==null||state.Area.Team!=unit.Team)state.Area=CreateRallyArea(state,unit,spacing);
                var ticket=new RallyTicket {Unit=unit,Area=state.Area,Created=Time.unscaledTime,Serial=++rallySerial};
                // Claim the prepared next position immediately, including rapid spawns.
                ticket.Spot=state.Area.Preview;state.Area.Preview=null;
                RallyTickets[unit]=ticket;
                Trace("Rallyfix queued "+unit.name+"; "+ticket.Area.Shape+" spacing "+ticket.Area.Spacing+"m; centre "+ticket.Area.Anchor);
            }
            catch(Exception e){DebugWarning("Rallyfix production observation: "+e.Message);}
        }
        private static bool RallyCurrent(RallyTicket ticket)=>RallyEnabled&&ticket.Unit&&ticket.Area.State.Factory&&
            !ticket.Area.State.Factory.IsDestroyed&&ticket.Unit.Team==ticket.Area.Team&&ticket.Area.State.Factory.Team==ticket.Area.Team&&Available(ticket.Unit)&&
            RallyTickets.TryGetValue(ticket.Unit,out var current)&&ReferenceEquals(current,ticket);
        private static void ReleaseRally(Unit unit,string reason=null)
        {
            if(ReferenceEquals(unit,null)||!RallyTickets.TryGetValue(unit,out var ticket))return;
            if(reason!=null)Trace("Rallyfix released "+(unit?unit.name:"destroyed unit")+": "+reason);
            RallyTickets.Remove(unit);
        }
        private static void RallyOtherOrder(AIOrderProcessor processor,bool success)
        {
            if(!success||issuingRally||!(processor.Owner is Unit unit))return;
            if(exitingRallyVehicle==unit||(productionRallyFactory&&RallyTickets.TryGetValue(unit,out var ticket)&&ticket.Area.State.Factory==productionRallyFactory))return;
            ReleaseRally(unit,"replacement order");
        }
        private static void SetRallyMarker(RallyFactory state,Vector3 point)
        {
            if(!state.Factory||state.Factory.IsDestroyed)return;
            movingRallyMarker=true;
            try{state.Factory.MoveRallyPoint(point,false);state.Marker=point;state.OwnsMarker=true;}
            finally{movingRallyMarker=false;}
        }
        private static void ClearRally()
        {
            rallySearch?.Dispose();rallySearch=null;searchingRallyTicket=null;
            foreach(var state in RallyFactories.Values)
            {
                if(Game.GetIsServer()&&state.OwnsMarker&&state.Factory&&DistanceSq(state.Factory.RallyPoint,state.Marker)<0.01f)
                {SetRallyMarker(state,state.RequestedPoint);state.OwnsMarker=false;}
                state.Area?.Positions.Dispose();state.Area=null;
            }
            RallyTickets.Clear();nextRallyPrune=0;
            foreach(var factory in RallyFactories.Keys.Where(f=>!f||f.IsDestroyed).ToArray())RallyFactories.Remove(factory);
        }
        private static void RallyFixCommand(Player player,string action)
        {
            if(!IsAdministrator(player)){Reply(player,"permission denied: admin Generic power required.");return;}
            if(!ApiReady){Reply(player,"server not initialized.");return;}
            var parts=action.Split(new[]{' ','\t'},StringSplitOptions.RemoveEmptyEntries);
            if(parts.Length==1||(parts.Length==2&&parts[1].Equals("status",StringComparison.OrdinalIgnoreCase)))
            {
                Reply(player,"Rallyfix requested "+(rallyFixDefault.Value?"ON":"OFF")+"; effective "+(RallyEnabled?"ON":"OFF")+
                    "; assigned "+RallyTickets.Values.Count(t=>t.Assigned)+"; pending "+RallyTickets.Values.Count(t=>!t.Assigned));
                foreach(var ticket in RallyTickets.Values.Where(t=>!t.Assigned).OrderBy(t=>t.Serial).Take(3))
                    Reply(player,(ticket.Unit?ticket.Unit.name:"removed unit")+": "+(ReferenceEquals(ticket,searchingRallyTicket)?"searching parking":"queued for parking")+
                        "; "+(int)(Time.unscaledTime-ticket.Created)+"s; "+ticket.Area.Shape+" "+ticket.Area.Spacing+"m");
                return;
            }
            if(parts.Length!=2||(!parts[1].Equals("on",StringComparison.OrdinalIgnoreCase)&&!parts[1].Equals("off",StringComparison.OrdinalIgnoreCase)))
            {Reply(player,"usage: /formation rallyfix on|off|status");return;}
            rallyFixDefault.Value=parts[1].Equals("on",StringComparison.OrdinalIgnoreCase);ClearRally();
            try{prefs.SaveToFile(false);FormatPreferences();Reply(player,"Rallyfix "+(rallyFixDefault.Value?"ON":"OFF")+" (saved; applies to new production)");}
            catch(Exception e){DebugError("Saving Rallyfix: "+e);Reply(player,"Rallyfix updated; CFG save failed.");}
        }
        private static void FormationDebugCommand(Player player,string action)
        {
            if(!IsAdministrator(player)){Reply(player,"permission denied: admin Generic power required.");return;}
            var parts=action.Split(new[]{' ','\t'},StringSplitOptions.RemoveEmptyEntries);
            if(parts.Length==1||(parts.Length==2&&parts[1].Equals("status",StringComparison.OrdinalIgnoreCase)))
            {Reply(player,"Debug "+(debug?"ON":"OFF"));return;}
            if(parts.Length!=2||(!parts[1].Equals("on",StringComparison.OrdinalIgnoreCase)&&!parts[1].Equals("off",StringComparison.OrdinalIgnoreCase)))
            {Reply(player,"usage: /formation debug on|off|status");return;}
            debug=parts[1].Equals("on",StringComparison.OrdinalIgnoreCase);traceBudget=80;
            Reply(player,"Debug "+(debug?"ON":"OFF")+" (until next round; CFG Debug supplies the default)");
        }
        private static void TickRally()
        {
            if(!RallyEnabled){if(RallyTickets.Count>0||rallySearch!=null||RallyFactories.Values.Any(s=>s.Area!=null))ClearRally();return;}
            if(Time.unscaledTime>=nextRallyPrune)
            {
                nextRallyPrune=Time.unscaledTime+1;
                foreach(var ticket in RallyTickets.Values.ToArray())
                {
                    if(!RallyCurrent(ticket)){ReleaseRally(ticket.Unit,"no longer eligible or factory/team changed");continue;}
                    if(!ticket.Assigned)continue;
                    float distance=DistanceSq(ticket.Unit.transform.position,ticket.Spot.Point);
                    if(distance<=Mathf.Pow(Mathf.Max(3,ticket.Area.Spacing*0.25f),2))ticket.Arrived=true;
                    if(ticket.Arrived&&distance>ticket.Area.Spacing*ticket.Area.Spacing)ReleaseRally(ticket.Unit,"left parking position");
                }
                foreach(var factory in RallyFactories.Keys.Where(f=>!f||f.IsDestroyed).ToArray())
                {RallyFactories[factory].Area?.Positions.Dispose();RallyFactories.Remove(factory);}
            }
            if(rallySearch==null)
            {
                var ticket=RallyTickets.Values.Where(t=>!t.Assigned).OrderBy(t=>t.Serial).FirstOrDefault();
                if(ticket!=null){searchingRallyTicket=ticket;rallySearch=FindRallyParking(ticket).GetEnumerator();}
            }
            if(rallySearch==null)return;
            var budget=System.Diagnostics.Stopwatch.StartNew();
            try
            {
                for(int step=0;step<8&&budget.Elapsed.TotalMilliseconds<2;step++)
                    if(!rallySearch.MoveNext()){rallySearch.Dispose();rallySearch=null;searchingRallyTicket=null;break;}
            }
            catch(Exception e)
            {
                DebugWarning("Rallyfix search: "+e.Message);
                if(searchingRallyTicket!=null&&!searchingRallyTicket.Assigned)ReleaseRally(searchingRallyTicket.Unit);
                rallySearch?.Dispose();rallySearch=null;searchingRallyTicket=null;
            }
        }
        private static IEnumerable<bool> FindRallyParking(RallyTicket ticket)
        {
            var area=ticket.Area;
            if(ticket.Spot==null&&ReferenceEquals(area.State.Area,area)&&area.Preview!=null)
            {ticket.Spot=area.Preview;area.Preview=null;}
            for(int attempt=0;attempt<2048&&RallyCurrent(ticket);attempt++)
            {
                Vector3 desired;
                if(ticket.Spot!=null){desired=ticket.Spot.Point;ticket.Spot=null;}
                else if(!NextRallyPosition(area,out desired))break;
                foreach(var step in ValidateRallyPosition(ticket,desired,false))yield return step;
                if(!rallyClearance)continue;
                Vector3 point=rallyProjectedPoint;
                if(!RallyCurrent(ticket))yield break;
                var target=new OrderTarget(point);var parameters=OrderIssueParams.Ai(AgentMoveSpeed.Fast);
                bool issued=false;issuingRally=true;generatedDepth++;
                try
                {
                    if(ticket.Unit is Vehicle vehicle&&vehicle.HasForceMove)
                    {vehicle.RallyTo(point);issued=vehicle.AI_QueueRallyToPoint==point;}
                    else issued=ticket.Unit.OrderAgent.IssueOrder(OrderDefinitionRegistry.Move,in target,in parameters);
                }
                finally{generatedDepth--;issuingRally=false;}
                if(!issued){yield return true;continue;}
                ticket.Spot=new RallySpot {Point=point};ticket.Assigned=true;
                Trace("Rallyfix assigned "+ticket.Unit.name+" to "+point+" (previous units retained)");
                // Validate one following destination and advertise it as the factory's
                // next marker. It is reserved until the next produced unit claims it.
                if(!ReferenceEquals(area.State.Area,area))yield break;
                SetRallyMarker(area.State,point);
                for(int next=0;next<2048&&RallyCurrent(ticket)&&ReferenceEquals(area.State.Area,area);next++)
                {
                    if(!NextRallyPosition(area,out desired))yield break;
                    foreach(var step in ValidateRallyPosition(ticket,desired,true))yield return step;
                    if(!rallyClearance)continue;
                    if(!RallyCurrent(ticket)||!ReferenceEquals(area.State.Area,area))yield break;
                    area.Preview=new RallySpot {Point=rallyProjectedPoint};SetRallyMarker(area.State,rallyProjectedPoint);
                    yield break;
                }
                yield break;
            }
            // Native shared RallyTo was suppressed for this unit; explicitly restore
            // its captured commander destination if all bounded candidates fail.
            if(RallyCurrent(ticket))
            {
                issuingRally=true;generatedDepth++;
                try{ticket.Unit.RallyTo(area.Automatic?area.State.DefaultPoint:area.Anchor);}
                finally{generatedDepth--;issuingRally=false;}
                Trace("Rallyfix no free destination within search limit; native rally restored.");
            }
            ReleaseRally(ticket.Unit);
        }
        private static bool NextRallyPosition(RallyArea area,out Vector3 desired)
        {
            desired=Vector3.zero;
            if(!area.Positions.MoveNext())return false;
            var offset=area.Positions.Current*area.Spacing;
            if(offset.sqrMagnitude>500*500)return false;
            desired=area.Anchor+Vector3.Cross(Vector3.up,area.Forward)*offset.x+area.Forward*offset.z;
            return true;
        }
        private static IEnumerable<Vector3> RallyOffsets(string shape)
        {
            yield return Vector3.zero;
            if(shape=="Sol")
            {
                // Nested integer grids complete at 1,4,9,16,25... positions.
                for(int side=2;;side++)
                {
                    int low=-(side-1)/2,high=side/2;
                    if(side%2==0)
                    {
                        for(int z=low;z<=high;z++)yield return new Vector3(high,0,z);
                        for(int x=high-1;x>=low;x--)yield return new Vector3(x,0,high);
                    }
                    else
                    {
                        for(int z=high;z>=low;z--)yield return new Vector3(low,0,z);
                        for(int x=low+1;x<=high;x++)yield return new Vector3(x,0,low);
                    }
                }
            }
            else if(shape=="Centauri")
            {
                int[] dq={0,-1,-1,0,1,1},dr={-1,0,1,1,0,-1};
                for(int ring=1;;ring++)
                {
                    int q=ring,r=0;
                    for(int edge=0;edge<6;edge++)for(int step=0;step<ring;step++)
                    {yield return new Vector3(q+r*0.5f,0,r*Mathf.Sqrt(3)*0.5f);q+=dq[edge];r+=dr[edge];}
                }
            }
            else
            {
                for(int ring=1;;ring++)
                {
                    int count=(int)Math.Floor(Math.PI/Math.Asin(1.0/(2*ring))+1e-9);
                    for(int index=0;index<count;index++)
                    {float angle=index*Mathf.PI*2/count;yield return new Vector3(Mathf.Sin(angle)*ring,0,Mathf.Cos(angle)*ring);}
                }
            }
        }
        private static bool rallyClearance;
        private static Vector3 rallyProjectedPoint;
        private static IEnumerable<bool> ValidateRallyPosition(RallyTicket ticket,Vector3 desired,bool preview)
        {
            rallyClearance=false;
            if(!RallyCurrent(ticket))yield break;
            bool valid=TerrainProjection.TryProject(ticket.Unit,desired,out Vector3 point)&&DistanceSq(desired,point)<=1;
            yield return true;
            if(!valid||!RallyCurrent(ticket))yield break;
            var target=new OrderTarget(point);
            valid=ticket.Unit.OrderAgent.CanIssueOrder(OrderDefinitionRegistry.Move,in target);
            yield return true;
            if(!valid||!RallyCurrent(ticket))yield break;
            var area=ticket.Area;int work=0;
            if(area.Automatic&&RallyOverlaps(area.State.Factory,point,75+area.Radius))yield break;
            foreach(var other in RallyTickets.Values.ToArray())
            {
                if((preview||other!=ticket)&&other.Spot!=null&&RallyTooClose(point,other.Spot.Point,
                    Mathf.Max(Mathf.Max(area.Spacing,other.Area.Spacing),area.Radius+other.Area.Radius)))yield break;
                if(++work%32==0){yield return true;if(!RallyCurrent(ticket))yield break;}
            }
            foreach(var state in RallyFactories.Values.ToArray())
            {
                var other=state.Area;
                if(other?.Preview!=null&&RallyTooClose(point,other.Preview.Point,Mathf.Max(Mathf.Max(area.Spacing,other.Spacing),area.Radius+other.Radius)))yield break;
                if(++work%32==0){yield return true;if(!RallyCurrent(ticket))yield break;}
            }
            foreach(var structure in Structure.Structures.ToArray())
            {
                if(structure&&!structure.IsDestroyed&&RallyOverlaps(structure,point,area.Radius))yield break;
                if(++work%32==0){yield return true;if(!RallyCurrent(ticket))yield break;}
            }
            foreach(var site in ConstructionSite.ConstructionSites.ToArray())
            {
                if(site&&!site.IsDestroyed&&RallyOverlaps(site,point,area.Radius))yield break;
                if(++work%32==0){yield return true;if(!RallyCurrent(ticket))yield break;}
            }
            foreach(var unit in Unit.Units.ToArray())
            {
                if(unit&&unit!=ticket.Unit&&!unit.IsDestroyed&&!unit.IsFlyingType&&!unit.InCompartment&&
                    RallyTooClose(point,unit.transform.position,area.Radius+RallyRadius(unit)))yield break;
                if(++work%32==0){yield return true;if(!RallyCurrent(ticket))yield break;}
            }
            rallyProjectedPoint=point;rallyClearance=true;
        }
        private static bool RallyTooClose(Vector3 first,Vector3 second,float minimum)
            =>DistanceSq(first,second)<Mathf.Pow(Mathf.Max(0,minimum-0.05f),2);
        private static bool RallyOverlaps(BaseGameObject obj,Vector3 point,float radius)
        {
            Bounds bounds=obj.PhysicalBounds;
            if(bounds.size==Vector3.zero)return DistanceSq(point,obj.transform.position)<radius*radius;
            Vector3 local=obj.transform.InverseTransformPoint(point);
            float x=Mathf.Max(0,Mathf.Abs(local.x-bounds.center.x)-bounds.extents.x);
            float z=Mathf.Max(0,Mathf.Abs(local.z-bounds.center.z)-bounds.extents.z);
            return x*x+z*z<radius*radius;
        }
    }
}
