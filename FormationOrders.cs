using System;
using System.Collections.Generic;
using System.Linq;
using Silica.AI;
using UnityEngine;

namespace Si_Formation
{
    public sealed partial class Formations
    {
        private sealed class Placement
        {
            internal Unit Unit;
            internal int Slot;
            internal float Radius;
            internal Vector3 Point;
            internal uint OrderId;
        }
        private sealed class AttackOperation
        {
            internal Player Player;
            internal Team Team;
            internal AIGroup Group;
            internal Target Target;
            internal bool HadTarget;
            internal Vector3 OriginalPosition;
            internal float Deadline;
            internal readonly List<Placement> Members=new List<Placement>();
        }
        private static readonly List<AttackOperation> Operations=new List<AttackOperation>();
        private static bool FpsMove(Player player,Vector3 position)
        {
            if(!Running||generatedDepth>0) return true;
            CancelPlayer(player,true);
            try {return !TryFps(player,position,null,false);}
            catch(Exception e) {MelonLoader.MelonLogger.Error("FPS move formation: "+e);return true;}
        }
        private static bool FpsAttack(Player player,Target target)
        {
            if(!Running||generatedDepth>0) return true;
            CancelPlayer(player,true);
            if(!ValidTarget(target))return true;
            try {return !TryFps(player,target.transform.position,target,true);}
            catch(Exception e) {MelonLoader.MelonLogger.Error("FPS attack formation: "+e);return true;}
        }
        private static bool ValidTarget(Target t)=>t&&t.Owner&&!t.IsDestroyed&&t.NetworkComponent&&t.NetworkComponent.IsValid;
        private static bool TryFps(Player player,Vector3 centre,Target target,bool attack)
        {
            if(!player||player.IsCommander||!player.ControlledUnit||player.Group==null||player.Group.Leader!=player.ControlledUnit) return false;
            // FPS RPC includes a player ID; when received remotely it must agree with the authenticated sender.
            if(networkSender&&networkSender!=player) return false;
            if(!FunctionActive(player,attack?"attack":"move"))return false;
            var f=Selected(player,attack?"attack":"move");
            if(f==null)return false;
            var group=player.Group;
            bool allStopped=group.AllStopped();
            var members=group.Units.Where(u=>u&&!u.IsDestroyed&&!u.PlayerControlled&&
                (allStopped||!group.IgnoresTask(u,out var reason)||reason==AIGroup.EIgnoreTaskReason.FOLLOWING_LEADER)).ToArray();
            if(members.Length<=2){ClearFormationSelection(members);return false;}
            PrepareFormationSelection(player,members);
            if(members.Length>ConfiguredPlayerLimit(player)-1||members.Length>GroupLimit||members.Any(u=>u.Team!=player.Team||!Available(u)))
            {MoveReply(player,$"formation not applied: FPS limit is {ConfiguredPlayerLimit(player)-1} eligible ground units; original order retained.");return false;}
            Vector3 heading=Heading(centre-player.ControlledUnit.transform.position);

            // Validate attack intent before handing the selection to the staged placement job.
            if(attack)
            {
                var intent=new OrderTarget(centre,target);
                if(!members.Any(u=>u.OrderAgent.CanIssueOrder(OrderDefinitionRegistry.Attack,in intent)))return false;
            }
            generatedDepth++;
            try
            {
                group.Task=null;
                foreach(var u in members)
                {
                    group.StopIgnoringTask(u); Followers.Remove(u);Superseded.Remove(u);

                }
                var issued=new List<Placement>();
                AttackOperation pending=null;
                if(attack)
                {
                    pending=new AttackOperation {Player=player,Team=player.Team,Group=group,Target=target,HadTarget=true,
                        OriginalPosition=centre,Deadline=Time.unscaledTime+30};
                    pending.Members.AddRange(issued);Operations.Add(pending);
                }
                QueuePlacementRetries(player,attack?"attack":"move",f,members,issued,centre,heading,AgentMoveSpeed.Normal,pending);
                Trace((attack?"attack staging":"FPS move")+" group="+members.Length);
                return true;
            }
            catch(Exception e){MelonLoader.MelonLogger.Error("FPS formation application: "+e);return true;}
            finally {generatedDepth--;}
        }
        private static bool CommanderMove(ref List<BaseGameObject> objects,Vector3 worldPosition,Target target,AgentMoveSpeed moveSpeed,bool isAttack,bool remote,bool queueOrder)
        {
            if(!Running||generatedDepth>0)return true;
            Player issuer=remote?networkSender:Player.CurrentPlayer;
            try
            {
                if(issuer&&issuer.IsCommander&&objects!=null)
                {
                    var selection=objects.OfType<Unit>().Where(u=>u&&u.Team==issuer.Team).Distinct().ToArray();
                    // Apply before explicit or soft locks can consume a small selection.
                    if(selection.Length<=2&&Options(issuer).Pending!=2)
                    {
                        if(Options(issuer).Pending==1)Options(issuer).Pending=0;
                        ClearFormationSelection(selection);
                        return true;
                    }
                    if(!queueOrder&&!isAttack&&!target&&Options(issuer).Pending!=2)PrepareFormationSelection(issuer,selection);
                    if(HandleLockedOrder(issuer,ref objects,worldPosition,target,moveSpeed,isAttack,queueOrder))return false;
                }
            }
            catch(Exception e){Fault(e);return false;}
            if(!issuer||!issuer.IsCommander||queueOrder||isAttack||target)return true;
            var f=Selected(issuer,"commander");
            if(f==null||!FunctionActive(issuer,"commander"))return true;
            // This cap governs formation handling, not the native game's command capacity.
            if(objects==null||objects.Count<=2)return true;
            if(objects.Count>CommanderOrderLimit){MoveReply(issuer,"selection exceeds commander formation capacity "+CommanderOrderLimit+"; entire order left to game.");return true;}
            try
            {
                var units=objects.OfType<Unit>().Distinct().ToArray();
                if(units.Length!=objects.Count||units.Any(u=>!Available(u)||u.Team!=issuer.Team))return true;
                foreach(var u in units){CancelUnit(u,true);Followers.Remove(u);Superseded.Remove(u);}
                Vector3 centroid=Vector3.zero;foreach(var u in units)centroid+=u.transform.position;centroid/=units.Length;

                generatedDepth++;
                try
                {
                    var issued=new List<Placement>();
                    QueuePlacementRetries(issuer,"commander",f,units,issued,worldPosition,Heading(worldPosition-centroid),moveSpeed);
                    Trace("commander placement queued="+units.Length);
                    return false;
                }
                finally{generatedDepth--;}
            }
            catch(Exception e){MelonLoader.MelonLogger.Error("Commander formation: "+e);return false;}
        }
        private static float Radius(Unit u,FormationDefinition f,FormationSlot s)
        {
            // ObjectInfo caches physical bounds during game initialization. Use its horizontal
            // envelope, not the editor's historical dot-size multiplier or grid spacing.
            var size=u.ObjectInfo?u.ObjectInfo.PhysicalBounds.size:Vector3.zero;
            float radius=size.x>0&&size.z>0?0.5f*(float)Math.Sqrt(size.x*size.x+size.z*size.z):u.PhysicalRadius;
            return Math.Max(0.5f,radius)+0.5f;
        }
        private static bool Separated(Vector3 point,float radius,IEnumerable<Placement> reserved,Unit self=null)
            =>!reserved.Any(p=>p.Unit!=self&&DistanceSq(point,p.Point)<(radius+p.Radius)*(radius+p.Radius));
        private static Vector3 World(FormationSlot slot,FormationDefinition f,Vector3 centre,Vector3 forward,bool mirrored=false)
            =>f.DirectionSensitive?centre+Vector3.Cross(Vector3.up,forward)*(mirrored?-slot.X:slot.X)+forward*slot.Z:centre+new Vector3(slot.X,0,slot.Z);
        private static List<Placement> Commit(List<Placement> plan,AgentMoveSpeed speed)
        {
            var issued=new List<Placement>();
            foreach(var p in plan)
            {
                try
                {
                    var target=new OrderTarget(p.Point);
                    var parameters=OrderIssueParams.Commanded(speed);
                    if(!Available(p.Unit)||!p.Unit.OrderAgent.IssueOrder(OrderDefinitionRegistry.Move,in target,in parameters))continue;
                    p.OrderId=p.Unit.OrderAgent.Orders.Count>0?p.Unit.OrderAgent.Orders[0].Id:0;
                    issued.Add(p);
                }
                catch(Exception e){MelonLoader.MelonLogger.Warning("Formation unit order skipped: "+e.Message);}
            }
            return issued;
        }
        private static void CancelPlayer(Player player,bool keepSoft=false)
        {
            Operations.RemoveAll(o=>o.Player==player);PlacementRetries.RemoveAll(o=>o.Player==player);
            SoftMotions.RemoveAll(m=>m.Batch.Player==player);
            DepartureSpeeds.RemoveAll(g=>g.Batch.Player==player);RefreshDepartureSpeeds();
            if(!keepSoft)SoftGroups.RemoveAll(g=>g.Player==player);
        }
        private static void CancelUnit(Unit unit,bool keepSoft=false)
        {
            LockHeadings.Remove(unit);
            SoftMotions.RemoveAll(m=>m.Placement.Unit==unit);
            RemoveDepartureUnit(unit);
            if(!keepSoft)ForgetSoftUnit(unit);
            ReleaseLockedUnit(unit);
            foreach(var batch in PlacementRetries)
            {
                batch.Pending.RemoveAll(p=>p.Unit==unit);
                if(batch.Reserved.RemoveAll(p=>p.Unit==unit)>0)
                {batch.Search?.Dispose();batch.Search=null;batch.Attempt=Math.Max(0,batch.Attempt-1);}
            }
            if(Followers.TryGetValue(unit,out var follower)&&Frames.TryGetValue(follower.Leader,out var frame))
            {frame.Search?.Dispose();frame.Search=null;}
            foreach(var batch in PlacementRetries.Where(b=>b.Pending.Count==0).ToArray())FinishPlacement(batch);
            foreach(var o in Operations) o.Members.RemoveAll(p=>p.Unit==unit);
            Operations.RemoveAll(o=>o.Members.Count==0&&!PlacementRetries.Any(b=>b.Attack==o&&b.Pending.Count>0)&&!SoftMotions.Any(m=>m.Batch.Attack==o));
        }
        private static void TickOperations()
        {
            foreach(var o in Operations.ToArray())
            {
                if(!o.Player||!Player.Players.Contains(o.Player)||!FunctionActive(o.Player,"attack")||o.Player.Team!=o.Team||o.Player.Group!=o.Group||
                    o.Player.IsCommander||(o.HadTarget&&!ValidTarget(o.Target))) {Operations.Remove(o);continue;}
                o.Members.RemoveAll(p=>!Available(p.Unit)||p.Unit.Team!=o.Team||p.Unit.AIGroup!=o.Group||
                    p.Unit.OrderAgent.Orders.Count>1||
                    (p.Unit.OrderAgent.Orders.Count==1&&(!(p.Unit.OrderAgent.Orders[0] is AIMoveOrder)||p.Unit.OrderAgent.Orders[0].Id!=p.OrderId)));
                if(o.Members.Count==0)
                {
                    if(PlacementRetries.Any(b=>b.Attack==o&&b.Pending.Count>0)||SoftMotions.Any(m=>m.Batch.Attack==o))continue;
                    Operations.Remove(o);continue;
                }
                if(PlacementRetries.Any(b=>b.Attack==o&&b.Pending.Count>0)||SoftMotions.Any(m=>m.Batch.Attack==o))continue;
                bool arrived=o.Members.All(p=>DistanceSq(p.Unit.transform.position,p.Point)<=25&&Math.Abs(p.Unit.transform.position.y-p.Point.y)<=5);
                if(!arrived&&Time.unscaledTime<o.Deadline)continue;
                Operations.Remove(o); // remove before issuing: no re-entrant restoration or group replay
                generatedDepth++;
                try
                {
                    foreach(var p in o.Members)
                    {
                        if(!Available(p.Unit)||(o.HadTarget&&!ValidTarget(o.Target)))continue;
                        // Keep entity-target versus position-only Attack semantics; never ResolveDefault here.
                        var intent=new OrderTarget(o.OriginalPosition,o.HadTarget?o.Target:null);
                        var parameters=OrderIssueParams.Commanded(p.Unit.AIAgent.MoveSpeed);
                        if(p.Unit.OrderAgent.CanIssueOrder(OrderDefinitionRegistry.Attack,in intent))
                            p.Unit.OrderAgent.IssueOrder(OrderDefinitionRegistry.Attack,in intent,in parameters);
                    }
                }
                finally {generatedDepth--;}
                Trace("attack restored ("+(arrived?"arrived":"30s timeout")+") members="+o.Members.Count);
            }
        }
    }
}
