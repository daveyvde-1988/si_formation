using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Si_Formation
{
    public sealed partial class Formations
    {
        private sealed class LayoutContext
        {
            internal Settings Settings;
            internal Team Team;
            internal float Scale,Pitch;
            internal Dictionary<Unit,Vector3> Points=new Dictionary<Unit,Vector3>();
        }
        private static LayoutContext activeLayout;
        private static bool FormationEligible(Unit u)=>u&&!u.IsDestroyed&&u.AIControlled&&!u.PlayerControlled&&!u.ControlledBy&&!u.InCompartment&&!(u.Driver&&u.Driver.ControlledBy)&&u.CanMove&&u.OrderAgent&&u.AIAgent&&u.AIAgent.AgentPathfinding;
        private static float Radius(Unit u)=>Mathf.Max(.5f,u.PhysicalRadius);
        private static float FactionScale(Team team,Settings settings)=>team&&(team.name=="Team_Alien"||team.TeamShortName=="Alien")?settings.AlienSpread/100f:1;
        private static float Pitch(Unit unit,float largest,Settings settings,float scale)
        {
            float radius=Radius(unit),influenced=Mathf.Lerp(radius,largest,settings.SizeInfluence/100f);
            return Mathf.Max(radius*2+.5f,(influenced+2)*scale);
        }
        private static Dictionary<Unit,Vector3> GridSlots(List<Unit> source,Vector3 anchor,Vector3 direction,Settings settings,float scale,out float pitch)
        {
            var result=new Dictionary<Unit,Vector3>();pitch=0;
            if(source.Count==0)return result;
            direction.y=0;if(direction.sqrMagnitude<.001f)direction=Vector3.forward;direction.Normalize();
            Vector3 right=Vector3.Cross(Vector3.up,direction);
            var units=source.OrderByDescending(u=>Vector3.Dot(u.transform.position,direction)).ToList();
            float largest=units.Max(Radius);
            int rows=Mathf.Max(1,Mathf.RoundToInt(Mathf.Sqrt(units.Count/1.8f))),columns=Mathf.CeilToInt((float)units.Count/rows);
            float depth=0,previousDepth=0;
            for(int start=0;start<units.Count;start+=columns)
            {
                var row=units.Skip(start).Take(columns).OrderBy(u=>Vector3.Dot(u.transform.position,right)).ToArray();
                var widths=row.Select(u=>Pitch(u,largest,settings,scale)).ToArray();
                float rowDepth=widths.Max(),width=widths.Sum(),x=-width*.5f;
                if(start>0)depth+=(previousDepth+rowDepth)*.5f;
                for(int i=0;i<row.Length;i++)
                {
                    x+=widths[i]*.5f;
                    Vector3 desired=anchor+right*x-direction*depth;
                    if(TerrainProjection.TryProject(row[i],desired,out var projected))result[row[i]]=projected;
                    x+=widths[i]*.5f;
                }
                pitch+=width;previousDepth=rowDepth;
            }
            pitch/=units.Count;return result;
        }
        private void InstallSpacing()
        {
            HarmonyInstance.Patch(AccessTools.Method(typeof(StrategyMode),"IssueFormationOrder"),prefix:new HarmonyMethod(typeof(Formations),nameof(PlaceFormationUnit)));
            HarmonyInstance.Patch(AccessTools.Method(typeof(Silica.LineFormation),"BuildSlots"),postfix:new HarmonyMethod(typeof(Formations),nameof(ScaleLineSlots)));
        }
        private static void PlaceFormationUnit(BaseGameObject obj,ref Vector3 worldPosition)
        {
            if(Active&&Game.GetIsServer()&&activeLayout!=null&&obj is Unit unit&&activeLayout.Points.TryGetValue(unit,out var point))worldPosition=point;
        }
        private static void ScaleLineSlots(List<Unit> units,Vector3 start,Vector3 end,List<Vector3> slotsOut,ref float spacing,bool __result)
        {
            if(!Active||!Game.GetIsServer()||!__result||activeLayout==null)return;
            var context=activeLayout;
            var points=SilicaFormationShared.CommanderLineLayout.Repack(units,slotsOut,start,end,context.Scale,context.Settings.SizeInfluence,
                (unit,desired)=>FormationEligible(unit)&&TerrainProjection.TryProject(unit,desired,out var point)?(Vector3?)point:null,out var pitch);
            foreach(var item in points)context.Points[item.Key]=item.Value;
            if(pitch>0){spacing=pitch;context.Pitch=pitch;}
        }
        private static void TuningCommand(Player p,string args,string command)
        {
            if(!ready||!Game.GetIsServer()||!p)return;
            var settings=ServerSettings;string value=Argument(args);
            if(value!=""&&value!="status")
            {
                if(!DemandAdmin(p))return;
                {
                    int minimum=command=="fpsallowance"?1:0;
                    if(!int.TryParse(value,NumberStyles.Integer,CultureInfo.InvariantCulture,out int percent)||percent<minimum||percent>100)
                    {Reply(p,"/"+command+" <"+minimum+"..100> | status");return;}
                    switch(command)
                    {
                        case "sizeinfluence":settings.SizeInfluence=percent;break;
                        case "alienspread":settings.AlienSpread=percent;break;
                        case "fpsspace":settings.FpsSpacing=percent;break;
                        case "fpsdistance":settings.FpsDistance=percent;break;
                        case "fpsallowance":settings.FpsAllowance=percent;break;
                    }
                    if(!SaveSettings(p))return;
                }
            }
            int current=command=="sizeinfluence"?settings.SizeInfluence:command=="alienspread"?settings.AlienSpread:command=="fpsdistance"?settings.FpsDistance:command=="fpsallowance"?settings.FpsAllowance:settings.FpsSpacing;
            Reply(p,command+": "+current+(command=="fpsallowance"?"m":"%")+"; new placements only.");
        }
    }
}
