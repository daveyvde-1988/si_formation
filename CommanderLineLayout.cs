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
            int count=Math.Min(units.Count,slots.Count);
            if(count==0)return points;
            Vector3 centre=(start+end)*.5f,axis=end-start;axis.y=0;
            float length=axis.magnitude;if(length<2)return points;axis.Normalize();
            Vector3 forward=Vector3.Cross(axis,Vector3.up);
            int rows=Mathf.Max(1,Mathf.RoundToInt(Mathf.Sqrt(count/1.8f)));
            int columns=Mathf.CeilToInt((float)count/rows);
            var widths=new float[count];var depths=new float[count];
            float minimumWidth=0,totalPitch=0;
            int band=0;
            while(band<count)
            {
                int rank=Rank(units[band]),last=band+1;
                while(last<count&&Rank(units[last])==rank)last++;
                float largestWidth=0,largestDepth=0;
                for(int i=band;i<last;i++)
                {largestWidth=Mathf.Max(largestWidth,units[i].PhysicalBounds.size.x);largestDepth=Mathf.Max(largestDepth,units[i].PhysicalBounds.size.z);}
                float rowWidth=0;int rowCount=0;
                for(int i=band;i<last;i++)
                {
                    var bounds=units[i].PhysicalBounds.size;float effect=influence/100f;
                    float diameter=Mathf.Max(.5f,units[i].PhysicalRadius)*2+.5f;
                    widths[i]=Mathf.Max(diameter,Mathf.Max(bounds.x+.5f,(Mathf.Lerp(Mathf.Max(1,bounds.x),Mathf.Max(1,largestWidth),effect)+4)*scale));
                    depths[i]=Mathf.Max(diameter,Mathf.Max(bounds.z+.5f,(Mathf.Lerp(Mathf.Max(1,bounds.z),Mathf.Max(1,largestDepth),effect)+4)*scale));
                    totalPitch+=widths[i];rowWidth+=widths[i];rowCount++;
                    if(rowCount==columns||i==last-1)
                    {minimumWidth=Mathf.Max(minimumWidth,rowWidth);rowWidth=0;rowCount=0;}
                }
                band=last;
            }
            // A short valid drag starts at the ordinary click rectangle's column
            // footprint instead of squeezing the group into a deep single file.
            // Wider drags admit more units per row, then spread a complete row.
            // Keep each native priority band in front of the following band.
            length=Mathf.Max(length,minimumWidth);
            float depth=0,previousDepth=0;
            int first=0;
            while(first<count)
            {
                int rank=Rank(units[first]),last=first+1;
                while(last<count&&Rank(units[last])==rank)last++;
                while(first<last)
                {
                    int endRow=first;float width=0,rowDepth=0;
                    while(endRow<last)
                    {
                        float w=widths[endRow],d=depths[endRow];
                        if(endRow>first&&width+w>length)break;
                        width+=w;rowDepth=Mathf.Max(rowDepth,d);endRow++;
                    }
                    if(first>0)depth+=(previousDepth+rowDepth)*.5f;
                    int rowCount=endRow-first;
                    float slack=rowCount>1?Mathf.Max(0,length-width)/(rowCount-1):0;
                    float x=rowCount>1?-Mathf.Max(width,length)*.5f:-width*.5f;
                    for(int i=first;i<endRow;i++)
                    {
                        float w=widths[i];x+=w*.5f;
                        var unit=units[i];Vector3 desired=centre+axis*x-forward*depth;
                        var point=project(unit,desired);
                        if(point.HasValue){slots[i]=point.Value;points[unit]=point.Value;}
                        x+=w*.5f+slack;
                    }
                    previousDepth=rowDepth;first=endRow;
                }
            }
            pitch=Mathf.Max(1,totalPitch/count);return points;
        }
        private static int Rank(Unit unit)=>unit.ObjectInfo?unit.ObjectInfo.GetFormationRowPriority():0;
    }
}
