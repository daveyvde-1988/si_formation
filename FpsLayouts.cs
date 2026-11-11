using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Si_Formation
{
    public sealed partial class Formations
    {
        // Procedural shapes inspired by the examples; neither dot counts nor colours
        // are templates. Local footprints and squad size determine every row/ring.
        private static Dictionary<Unit,Vector3> FpsLayout(List<Unit> source,FpsState state,Vector3 anchor,Vector3 direction,FpsMode mode,bool moving)
        {
            var result=new Dictionary<Unit,Vector3>();if(source.Count==0)return result;
            direction.y=0;if(direction.sqrMagnitude<.001f)direction=state.Leader.transform.forward;
            direction.y=0;if(direction.sqrMagnitude<.001f)direction=Vector3.forward;direction.Normalize();
            Vector3 right=Vector3.Cross(Vector3.up,direction);
            var settings=ServerSettings;
            var units=source.OrderBy(u=>u.ObjectInfo?u.ObjectInfo.GetFormationRowPriority():0)
                .ThenByDescending(u=>u.ObjectInfo?u.ObjectInfo.GetFormationPriority():0).ThenBy(u=>u.GetInstanceID()).ToList();
            float largest=units.Max(Radius),scale=Multiplier(settings.FpsSpacing)*FactionScale(state.Team,settings);
            float standoff=Mathf.Max((5+Radius(state.Leader))*Multiplier(settings.FpsDistance),Radius(state.Leader)+largest+1);
            var pitches=units.Select(u=>Pitch(u,largest,settings,scale)).ToArray();
            var offsets=new Vector3[units.Count];
            if(mode==FpsMode.Move)FpsGridOffsets(pitches,offsets);
            else if(moving)FpsColumnOffsets(pitches,offsets,standoff);
            else FpsFanOffsets(pitches,offsets,standoff);
            for(int i=0;i<units.Count;i++)
            {
                var unit=units[i];var desired=anchor+right*offsets[i].x+direction*offsets[i].z;
                if(!TerrainProjection.TryProject(unit,desired,out var point))continue;
                if(FlatDistanceSq(point,state.Leader.transform.position)<Mathf.Pow(Radius(unit)+Radius(state.Leader)+1,2))continue;
                if(result.Any(p=>FlatDistanceSq(p.Value,point)<Mathf.Pow(Radius(p.Key)+Radius(unit)+.5f,2)))continue;
                result[unit]=point;
            }
            return result;
        }
        private static void FpsColumnOffsets(float[] pitches,Vector3[] offsets,float standoff)
        {
            float depth=standoff,previous=0;
            for(int i=0;i<pitches.Length;i+=2)
            {
                bool pair=i+1<pitches.Length;
                float row=Mathf.Max(pitches[i],pair?pitches[i+1]:0);
                if(i>0)depth+=(previous+row)*.625f;
                offsets[i]=new Vector3(pair?-pitches[i+1]*.5f:0,0,-depth);
                if(pair)offsets[i+1]=new Vector3(pitches[i]*.5f,0,-depth-row*.35f);
                previous=row;
            }
        }
        private static void FpsFanOffsets(float[] pitches,Vector3[] offsets,float standoff)
        {
            const float arc=150*Mathf.Deg2Rad;
            float radius=standoff,previous=0;int first=0;
            while(first<pitches.Length)
            {
                if(first>0)radius+=previous;
                int end=first;float width=0,depth=0;
                while(end<pitches.Length)
                {
                    if(end>first&&width+pitches[end]>radius*arc)break;
                    width+=pitches[end];depth=Mathf.Max(depth,pitches[end]);end++;
                }
                // Avoid overlapping the first/last cell on a very small inner ring.
                radius=Mathf.Max(radius,width/arc);
                float angle=-width/(2*radius);
                for(int i=first;i<end;i++)
                {
                    angle+=pitches[i]/(2*radius);
                    offsets[i]=new Vector3(Mathf.Sin(angle)*radius,0,-Mathf.Cos(angle)*radius);
                    angle+=pitches[i]/(2*radius);
                }
                previous=depth;first=end;
            }
        }
        private static void FpsGridOffsets(float[] pitches,Vector3[] offsets)
        {
            int columns=Mathf.Max(1,Mathf.CeilToInt(Mathf.Sqrt(pitches.Length))),rows=Mathf.CeilToInt((float)pitches.Length/columns);
            var depths=new float[rows];
            for(int i=0;i<pitches.Length;i++)depths[i/columns]=Mathf.Max(depths[i/columns],pitches[i]);
            float z=depths.Sum()*.5f;
            for(int row=0;row<rows;row++)
            {
                int start=row*columns,end=Math.Min(pitches.Length,start+columns);
                float width=0;for(int i=start;i<end;i++)width+=pitches[i];
                float x=-width*.5f;z-=depths[row]*.5f;
                for(int i=start;i<end;i++)
                {x+=pitches[i]*.5f;offsets[i]=new Vector3(x,0,z);x+=pitches[i]*.5f;}
                z-=depths[row]*.5f;
            }
        }
    }
}
