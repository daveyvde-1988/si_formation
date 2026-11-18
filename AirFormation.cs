using System;
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
        private static bool SeparateLargeFlyer(Unit unit)
        {
            if(!unit.ObjectInfo||!IsAlienTeam(unit.Team))return false;
            string name=unit.ObjectInfo.name;
            return name.IndexOf("Colossus",StringComparison.OrdinalIgnoreCase)>=0||
                name.IndexOf("Defiler",StringComparison.OrdinalIgnoreCase)>=0;
        }
        private static bool IsLesserSpawnAir(Unit unit)
        {
            if(!unit.ObjectInfo||!IsAlienTeam(unit.Team))return false;
            string name=unit.ObjectInfo.name;
            return name=="ObjectInfo_Alien_Wasp"||name=="ObjectInfo_Alien_Dragonfly"||name=="ObjectInfo_Alien_Squid";
        }
        private static float AirFormationPitch(List<Unit> aircraft)
        {
            var ordinary=aircraft.Where(unit=>!SeparateLargeFlyer(unit)).ToList();
            var source=ordinary.Count>0?ordinary:aircraft;
            return source.Count==0?0:Mathf.Max(1,source.Average(unit=>Radius(unit)*2+.5f));
        }

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
            float formationMinX=minX,formationMaxX=maxX;
            foreach(var unit in aircraft)points.Remove(unit);
            var eligible=aircraft.Where(FormationEligible).ToList();
            var units=eligible.Where(unit=>!SeparateLargeFlyer(unit)).OrderByDescending(unit=>Vector3.Dot(unit.transform.position,forward)).ToList();
            var accepted=new List<KeyValuePair<Unit,Vector3>>();
            if(units.Count>0)
            {
                float largest=units.Max(Radius);
                float cellWidth=units.Max(unit=>AirCellSize(unit,largest,settings,scale,true));
                float cellDepth=units.Max(unit=>AirCellSize(unit,largest,settings,scale,false));
                bool compact=ground.Length==0||units.Count>ground.Length;
                if(compact)
                {
                    int compactColumns=Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(units.Count*cellDepth/cellWidth)),1,units.Count);
                    float width=cellWidth*(compactColumns-1);
                    float centre=(minX+maxX)*.5f;
                    minX=centre-width*.5f;
                    maxX=centre+width*.5f;
                }
                int columns=Mathf.Min(units.Count,Mathf.Max(1,Mathf.FloorToInt((maxX-minX)/cellWidth+.001f)+1));
                for(int start=0;start<units.Count;start+=columns)
                {
                    var row=units.Skip(start).Take(columns).OrderBy(unit=>Vector3.Dot(unit.transform.position,right)).ToArray();
                    float rowWidth=compact?Mathf.Min(maxX-minX,cellWidth*(row.Length-1)):maxX-minX;
                    for(int i=0;i<row.Length;i++)
                    {
                        float x=(minX+maxX)*.5f;
                        if(row.Length>1)x+=Mathf.Lerp(-rowWidth*.5f,rowWidth*.5f,(float)i/(row.Length-1));
                        Vector3 desired=airAnchor+right*x-forward*(start/columns*cellDepth);
                        StoreAirFormationSlot(row[i],desired,points,accepted,airAnchor,forward,right,
                            ground.Length>0,ground.Length>0,minX,maxX);
                    }
                }
            }
            // Pack large flyers independently behind the ordinary block. Keep the
            // original ground width even when ordinary-air packing widened itself.
            var largeFlyers=eligible.Where(SeparateLargeFlyer).OrderByDescending(Radius).ToList();
            if(largeFlyers.Count==0)return;
            float largestFlyer=largeFlyers.Max(Radius);
            float largeCellWidth=largeFlyers.Max(unit=>AirCellSize(unit,largestFlyer,settings,scale,true));
            float largeCellDepth=largeFlyers.Max(unit=>AirCellSize(unit,largestFlyer,settings,scale,false));
            float largeMinX=formationMinX,largeMaxX=formationMaxX;
            int largeColumns=Mathf.Min(largeFlyers.Count,Mathf.Max(1,Mathf.FloorToInt((largeMaxX-largeMinX)/largeCellWidth+.001f)+1));
            bool compactLarge=ground.Length==0||largeColumns<2;
            if(compactLarge)
            {
                largeColumns=largeFlyers.Count==1?1:Mathf.Clamp(
                    Mathf.CeilToInt(Mathf.Sqrt(largeFlyers.Count*largeCellDepth/largeCellWidth)),2,largeFlyers.Count);
                float width=largeCellWidth*(largeColumns-1),centre=(formationMinX+formationMaxX)*.5f;
                largeMinX=centre-width*.5f;
                largeMaxX=centre+width*.5f;
            }
            float largeRear=0;
            if(accepted.Count>0)
                largeRear=accepted.Min(item=>Vector3.Dot(item.Value-airAnchor,forward)-Radius(item.Key))-
                    Mathf.Max(largestFlyer+.5f,largeCellDepth*.5f);
            for(int start=0;start<largeFlyers.Count;start+=largeColumns)
            {
                var row=largeFlyers.Skip(start).Take(largeColumns).OrderBy(unit=>Vector3.Dot(unit.transform.position,right)).ToArray();
                float rowWidth=compactLarge?Mathf.Min(largeMaxX-largeMinX,largeCellWidth*(row.Length-1)):largeMaxX-largeMinX;
                for(int i=0;i<row.Length;i++)
                {
                    float x=(largeMinX+largeMaxX)*.5f;
                    if(row.Length>1)x+=Mathf.Lerp(-rowWidth*.5f,rowWidth*.5f,(float)i/(row.Length-1));
                    Vector3 desired=airAnchor+right*x+forward*(largeRear-start/largeColumns*largeCellDepth);
                    StoreAirFormationSlot(row[i],desired,points,accepted,airAnchor,forward,right,
                        ground.Length>0,false,largeMinX,largeMaxX);
                }
            }
        }
        private static void StoreAirFormationSlot(Unit unit,Vector3 desired,Dictionary<Unit,Vector3> points,
            List<KeyValuePair<Unit,Vector3>> accepted,Vector3 anchor,Vector3 forward,Vector3 right,
            bool constrainRear,bool constrainWidth,float minX,float maxX)
        {
            if(TryAirFormationPoint(unit,desired,out var point)&&
                (!constrainRear||Vector3.Dot(point-anchor,forward)<=.1f)&&
                (!constrainWidth||Vector3.Dot(point-anchor,right)>=minX-.1f&&Vector3.Dot(point-anchor,right)<=maxX+.1f)&&
                accepted.All(other=>FlatDistanceSq(point,other.Value)>=Mathf.Pow(Radius(unit)+Radius(other.Key)+.5f,2)))
            {
                points[unit]=point;
                accepted.Add(new KeyValuePair<Unit,Vector3>(unit,point));
                if(activeLayout!=null)activeLayout.RejectedAir.Remove(unit);
                return;
            }
            if(activeLayout==null)return;
            activeLayout.RejectedAir.Add(unit);
            if(CohesionDebugEnabled||activeMovementDiagnostics?.Watched==true)
                MelonLoader.MelonLogger.Msg("[Formation air] no safe rear slot for "+MovementUnitName(unit)+" desired="+MovementPoint(desired)+"; existing order retained.");
        }
        private static float AirCellSize(Unit unit,float largest,Settings settings,float scale,bool width)
        {
            float radius=Radius(unit),body=width?unit.PhysicalBounds.size.x:unit.PhysicalBounds.size.z;
            float physicalFloor=Mathf.Max(radius*2+.5f,body+.5f);
            if(IsLesserSpawnAir(unit))
            {
                // Native line packing uses the body footprint plus a 4m gap.
                // Use that baseline for both clicks and drags, independently of
                // the shared spacing controls, and retain full physical clearance.
                float baseline=Mathf.Max(Mathf.Max(1,body)+4,physicalFloor);
                return baseline*(1+settings.LesserSpawnAirSpacing/100f);
            }
            float influenced=Mathf.Lerp(radius,largest,settings.SizeInfluence/100f);
            return Mathf.Max(physicalFloor,(influenced+2)*scale);
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
