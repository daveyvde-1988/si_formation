using System;
using System.Collections.Generic;
using UnityEngine;

namespace SilicaFormationShared
{
    // Server-side dragged formation packing. Projection and eligibility are supplied by the caller.
    internal static class CommanderLineLayout
    {
        internal static float Multiplier(int percent)=>percent<=30?1+percent/15f:3+(percent-30)/10f;

        internal static Dictionary<Unit,Vector3> Repack(List<Unit> units,List<Vector3> slots,Vector3 start,Vector3 end,
            float scale,int influence,Func<Unit,Vector3,Vector3?> project,out float pitch,bool force=false)
        {
            var points=new Dictionary<Unit,Vector3>();pitch=0;
            if(!force&&influence==100&&Mathf.Abs(scale-1)<.001f)return points;
            Vector3 centre=(start+end)*.5f,axis=end-start;axis.y=0;
            float length=axis.magnitude;if(length<2)return points;axis.Normalize();
            Vector3 forward=Vector3.Cross(axis,Vector3.up);
            float depth=0,previousDepth=0,totalPitch=0;
            int first=0;
            while(first<units.Count&&first<slots.Count)
            {
                int rank=units[first].ObjectInfo?units[first].ObjectInfo.GetFormationRowPriority():0,last=first+1;
                while(last<units.Count&&(units[last].ObjectInfo?units[last].ObjectInfo.GetFormationRowPriority():0)==rank)last++;
                float largestWidth=0,largestDepth=0;
                for(int i=first;i<last;i++)
                {largestWidth=Mathf.Max(largestWidth,units[i].PhysicalBounds.size.x);largestDepth=Mathf.Max(largestDepth,units[i].PhysicalBounds.size.z);}
                while(first<last)
                {
                    int endRow=first;float width=0,rowDepth=0;
                    var widths=new List<float>();
                    while(endRow<last)
                    {
                        var bounds=units[endRow].PhysicalBounds.size;float effect=influence/100f;
                        float w=Mathf.Max(bounds.x+.5f,(Mathf.Lerp(Mathf.Max(1,bounds.x),Mathf.Max(1,largestWidth),effect)+4)*scale);
                        float d=Mathf.Max(bounds.z+.5f,(Mathf.Lerp(Mathf.Max(1,bounds.z),Mathf.Max(1,largestDepth),effect)+4)*scale);
                        if(endRow>first&&width+w>length)break;
                        widths.Add(w);width+=w;rowDepth=Mathf.Max(rowDepth,d);endRow++;
                    }
                    if(first>0)depth+=(previousDepth+rowDepth)*.5f;
                    float slack=widths.Count>1?Mathf.Max(0,length-width)/(widths.Count-1):0;
                    float x=widths.Count>1?-Mathf.Max(width,length)*.5f:-width*.5f;
                    for(int i=first;i<endRow&&i<slots.Count;i++)
                    {
                        float w=widths[i-first];x+=w*.5f;
                        var unit=units[i];Vector3 desired=centre+axis*x-forward*depth;
                        var point=project(unit,desired);
                        if(point.HasValue){slots[i]=point.Value;points[unit]=point.Value;}
                        totalPitch+=w;x+=w*.5f+slack;
                    }
                    previousDepth=rowDepth;first=endRow;
                }
            }
            pitch=Mathf.Max(1,totalPitch/Mathf.Max(1,units.Count));return points;
        }
    }
}
