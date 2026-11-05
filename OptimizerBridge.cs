using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Silica.AI;
using UnityEngine;

namespace Si_Formation
{
    public static partial class FormationApi
    {
        /// <summary>Main-thread, order-free commander placement search. Current is null for
        /// a work step or KeyValuePair&lt;Unit,Vector3&gt; for a reserved destination. Dispose to cancel.
        /// No selected custom commander layout yields no destinations; callers retain native fallback.</summary>
        public static IEnumerator BeginOptimizerPlacement(ulong steamId,Unit[] units,Vector3 centre,Vector3 forward)
            =>Formations.ApiOptimizerPlacement(steamId,units,centre,forward).GetEnumerator();

        public static bool IsOptimizerScoutPending(ulong steamId)=>Formations.ApiOptimizerScoutPending(steamId);
        public static ulong GetOptimizerCommanderId(bool remote,Team team)=>Formations.ApiOptimizerCommanderId(remote,team);
        public static bool IsOptimizerLocked(Unit unit)=>Formations.ApiOptimizerLocked(unit);

        /// <summary>Use native commander placement without custom Formation interception.
        /// Caller retains its own command scope and captures native per-unit destinations.</summary>
        public static void DispatchOptimizerNativeMove(Unit[] units,Vector3 point,Target target,AgentMoveSpeed speed,bool isAttack)
            =>Formations.ApiOptimizerNativeMove(units,point,target,speed,isAttack);

        /// <summary>Validated scout recipients, immediately before their native move dispatch.
        /// Listeners may release their own control; they must not issue scout orders themselves.</summary>
        public static event Action<Unit[]> OptimizerScoutDispatched;

        internal static void NotifyOptimizerScout(Unit unit)
        {
            var listeners=OptimizerScoutDispatched;
            if(listeners==null)return;
            foreach(Action<Unit[]> listener in listeners.GetInvocationList())
            {
                try{listener(new[]{unit});}
                catch(Exception error){MelonLoader.MelonLogger.Warning("Formation optimizer scout listener: "+error.Message);}
            }
        }
    }

    public sealed partial class Formations
    {
        internal static bool ApiOptimizerLocked(Unit unit)
            =>ApiCheck()==ChangeResult.Success&&unit&&LockedUnits.ContainsKey(unit);

        internal static void ApiOptimizerNativeMove(Unit[] units,Vector3 point,Target target,AgentMoveSpeed speed,bool isAttack)
        {
            if(ApiCheck()!=ChangeResult.Success||units==null||units.Length==0)return;
            generatedDepth++;
            try{StrategyMode.PerformMoveAttack(units.Where(u=>u&&!u.IsDestroyed).Cast<BaseGameObject>().ToList(),point,target,speed,isAttack,false,false);}
            finally{generatedDepth--;}
        }

        internal static ulong ApiOptimizerCommanderId(bool remote,Team team)
        {
            if(ApiCheck()!=ChangeResult.Success||!Running||!team)return 0;
            var player=remote?networkSender:Player.CurrentPlayer;
            if(!player||!player.IsCommander||player.Team!=team)
                player=Player.Players.FirstOrDefault(p=>p&&p.IsCommander&&p.Team==team);
            return player?PlayerId(player):0;
        }

        internal static bool ApiOptimizerScoutPending(ulong id)
        {
            if(ApiCheck()!=ChangeResult.Success||!Running)return false;
            var player=Connected(id);
            return player&&player.IsCommander&&CommanderSettings.TryGetValue(player,out var options)&&options.Pending==2;
        }

        internal static IEnumerable<object> ApiOptimizerPlacement(ulong id,Unit[] requested,Vector3 centre,Vector3 forward)
        {
            if(ApiCheck()!=ChangeResult.Success||!Running||requested==null||requested.Length==0)yield break;
            var units=requested.Where(u=>Available(u)&&u.CanMove&&!LockedUnits.ContainsKey(u)).Distinct().ToArray();
            if(units.Length==0)yield break;
            var team=units[0].Team;
            var player=id==0?Player.Players.FirstOrDefault(p=>p&&p.IsCommander&&p.Team==team):Connected(id);
            if(!player||!player.IsCommander||player.Team!=team)yield break;
            var definition=Selected(player,"commander");
            if(definition==null||!FunctionActive(player,"commander"))yield break;
            units=units.Where(u=>u.Team==team).ToArray();
            var selected=new HashSet<Unit>(units);
            var old=SoftGroups.Where(g=>g.Player==player&&g.Definition==definition&&g.Function=="commander"&&
                    g.Groups.Keys.All(u=>!Available(u)||selected.Contains(u)))
                .OrderByDescending(g=>units.Count(u=>g.Slots.ContainsKey(u))).FirstOrDefault();
            var heading=Heading(forward);
            bool mirrored=definition.DirectionSensitive&&old!=null&&(old.Mirrored^Reversed(old.Forward,heading));
            var saved=new Dictionary<Unit,int>();
            var groups=units.ToDictionary(u=>u,u=>u.AIGroup);
            var orders=units.ToDictionary(u=>u,u=>u.OrderAgent.Orders.Select(o=>o.Id).ToArray());
            foreach(var unit in units)
                if(old!=null&&old.Groups.TryGetValue(unit,out var group)&&unit.AIGroup==group&&old.Slots.TryGetValue(unit,out int slot))saved[unit]=slot;
            float scale=For(player).CommanderSpacing>0?For(player).CommanderSpacing/definition.Scale:1;
            var cache=new PlacementCache(definition,centre,heading,mirrored,spacingScale:scale);
            var ready=new Queue<KeyValuePair<Unit,Vector3>>();
            bool Current(Unit unit)=>Running&&player&&Player.Players.Contains(player)&&player.IsCommander&&player.Team==team&&
                Selected(player,"commander")==definition&&Available(unit)&&unit.CanMove&&unit.Team==team&&
                !LockedUnits.ContainsKey(unit)&&unit.AIGroup==groups[unit]&&
                (unit.OrderAgent.Orders.Count==0||orders[unit].SequenceEqual(unit.OrderAgent.Orders.Select(o=>o.Id)));
            bool Accept(Placement placement)
            {
                if(!Current(placement.Unit))return false;
                ready.Enqueue(new KeyValuePair<Unit,Vector3>(placement.Unit,placement.Point));
                return true;
            }
            using(var search=SearchRemembered(saved,definition,units,centre,heading,new List<Placement>(),Current,Accept,mirrored,cache).GetEnumerator())
                while(search.MoveNext())
                {
                    if(!Running||!player||player.Team!=team||!player.IsCommander||Selected(player,"commander")!=definition)yield break;
                    if(ready.Count>0)yield return ready.Dequeue();
                    else yield return null;
                }
            while(ready.Count>0)yield return ready.Dequeue();
        }
    }
}
