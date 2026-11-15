using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Si_Formation
{
    public sealed partial class Formations
    {
        // Classify by movement capability, not the transient landed/flying state.
        // Flyer-capable Decapods therefore never inflate the ground footprint.
        private static bool IsFormationAir(Unit unit)=>unit&&(unit is VehicleAir||unit.IsFlyingType);
        private static float AirFormationPitch(List<Unit> aircraft)=>aircraft.Count==0?0:
            Mathf.Max(1,aircraft.Average(unit=>Radius(unit)*2+.5f));

        private static void AddAirFormationSlots(List<Unit> aircraft,Dictionary<Unit,Vector3> points,Vector3 anchor,
            Vector3 forward,Settings settings,float scale)
        {
            if(aircraft.Count==0)return;
            forward.y=0;if(forward.sqrMagnitude<.001f)forward=Vector3.forward;forward.Normalize();
            Vector3 right=Vector3.Cross(Vector3.up,forward);
            var ground=points.Where(item=>item.Key&&!IsFormationAir(item.Key)).ToArray();
            Vector3 airAnchor=anchor;
            float minX=0,maxX=0,rearEdge=0;
            if(ground.Length>0)
            {
                rearEdge=ground.Min(item=>Vector3.Dot(item.Value-anchor,forward)-Radius(item.Key));
                minX=ground.Min(item=>Vector3.Dot(item.Value-anchor,right)-Radius(item.Key));
                maxX=ground.Max(item=>Vector3.Dot(item.Value-anchor,right)+Radius(item.Key));
                airAnchor+=forward*(rearEdge-50);
            }
            foreach(var unit in aircraft)points.Remove(unit);
            var units=aircraft.Where(FormationEligible).OrderByDescending(unit=>Vector3.Dot(unit.transform.position,forward)).ToList();
            if(units.Count==0)return;
            float largest=units.Max(Radius);
            float cellWidth=units.Max(unit=>AirCellSize(unit,largest,settings,scale,true));
            float cellDepth=units.Max(unit=>AirCellSize(unit,largest,settings,scale,false));
            if(ground.Length==0){float width=cellWidth*Mathf.Max(0,Mathf.CeilToInt(Mathf.Sqrt(units.Count))-1);minX=-width*.5f;maxX=width*.5f;}
            int columns=Mathf.Min(units.Count,Mathf.Max(1,Mathf.FloorToInt((maxX-minX)/cellWidth)+1));
            var accepted=new List<KeyValuePair<Unit,Vector3>>();
            for(int start=0;start<units.Count;start+=columns)
            {
                var row=units.Skip(start).Take(columns).OrderBy(unit=>Vector3.Dot(unit.transform.position,right)).ToArray();
                for(int i=0;i<row.Length;i++)
                {
                    var unit=row[i];
                    float x=row.Length==1?(minX+maxX)*.5f:Mathf.Lerp(minX,maxX,(float)i/(row.Length-1));
                    Vector3 desired=airAnchor+right*x-forward*(start/columns*cellDepth);
                    if(TryAirFormationPoint(unit,desired,out var point)&&accepted.All(other=>
                        FlatDistanceSq(point,other.Value)>=Mathf.Pow(Radius(unit)+Radius(other.Key)+.5f,2))&&
                        (ground.Length==0||Vector3.Dot(point-anchor,forward)<=rearEdge-50+.1f&&
                        Vector3.Dot(point-anchor,right)>=minX-.1f&&Vector3.Dot(point-anchor,right)<=maxX+.1f))
                    {
                        points[unit]=point;accepted.Add(new KeyValuePair<Unit,Vector3>(unit,point));
                        if(activeLayout!=null)activeLayout.RejectedAir.Remove(unit);
                    }
                    else if(activeLayout!=null)
                    {
                        activeLayout.RejectedAir.Add(unit);
                        if(CohesionDebugEnabled||activeMovementDiagnostics?.Watched==true)
                            MelonLoader.MelonLogger.Msg("[Formation air] no safe rear slot for "+MovementUnitName(unit)+" desired="+MovementPoint(desired)+"; existing order retained.");
                    }
                }
            }
        }
        private static float AirCellSize(Unit unit,float largest,Settings settings,float scale,bool width)
        {
            float radius=Radius(unit),body=width?unit.PhysicalBounds.size.x:unit.PhysicalBounds.size.z;
            float influenced=Mathf.Lerp(radius,largest,settings.SizeInfluence/100f);
            return Mathf.Max(Mathf.Max(radius*2+.5f,body+.5f),(influenced+2)*scale);
        }
        private static bool TryAirFormationPoint(Unit unit,Vector3 desired,out Vector3 point)
        {
            point=Vector3.zero;
            if(!FormationEligible(unit))return false;
            var path=unit.AIAgent.AgentPathfinding;
            if(!path.PathfindingSeeker)return false;
            var terrain=Game.MainTerrain;
            if(!terrain||!terrain.terrainData)return false;
            var bounds=new Bounds(terrain.transform.position+terrain.terrainData.bounds.center,terrain.terrainData.bounds.size);
            if(desired.x<bounds.min.x||desired.x>bounds.max.x||desired.z<bounds.min.z||desired.z>bounds.max.z)return false;
            Vector3 seed=GamePhysics.GetTerrainPosition(desired,0,terrain);
            if(float.IsNaN(seed.y)||float.IsInfinity(seed.y))return false;
            // Like native commander destinations, return a navigation-ground seed.
            // Native air path postprocessing supplies the existing FlyPreferredHeight;
            // altitude and explicit grounded/stealth behavior stay native.
            return GameAI.GetPointValidForAI(seed,path.PathfindingSeeker.graphMask,out point,75f,5f,75f);
        }
    }
}
