using System;
using System.Collections.Generic;
using UnityEngine;

namespace Si_Formation
{
    // Arc-length route: memory and backward search both have fixed upper bounds.
    internal sealed class RouteHistory
    {
        internal const int MaxPoints=256;
        internal const float MaxLength=2000;
        private struct Node {internal Vector3 P;internal float S;internal Node(Vector3 p,float s){P=p;S=s;}}
        private readonly List<Node> points=new List<Node>();
        internal Vector3 Forward {get;private set;}
        internal Vector3 InitialForward {get;private set;}
        internal Vector3 Head=>points[points.Count-1].P;
        internal float Distance=>points[points.Count-1].S;
        internal int Count=>points.Count;
        internal RouteHistory(Vector3 p,Vector3 forward) {Forward=InitialForward=Normalize(forward);points.Add(new Node(p,0));}
        private static Vector3 Flat(Vector3 v){v.y=0;return v;}
        private static Vector3 Normalize(Vector3 v)=>Flat(v).sqrMagnitude>0.01f?Flat(v).normalized:Vector3.forward;
        internal bool Record(Vector3 p)
        {
            Vector3 delta=Flat(p-Head);float length=delta.magnitude;
            if(length>150) {points.Clear();points.Add(new Node(p,0));return false;} // teleport: do not join maps/vehicles with a route segment
            if(length<1)return true;
            Forward=Normalize(delta);
            points.Add(new Node(p,Distance+length));
            while(points.Count>MaxPoints||(points.Count>2&&Distance-points[1].S>MaxLength))points.RemoveAt(0);
            return true;
        }
        internal float Nearest(Vector3 p)
        {
            float best=float.MaxValue,result=Distance;
            for(int i=1;i<points.Count;i++)
            {
                Vector3 d=Flat(points[i].P-points[i-1].P);
                float t=Math.Max(0,Math.Min(1,Vector3.Dot(Flat(p-points[i-1].P),d)/Math.Max(0.001f,d.sqrMagnitude)));
                float squared=Flat(p-(points[i-1].P+d*t)).sqrMagnitude;
                if(squared<best){best=squared;result=points[i-1].S+(points[i].S-points[i-1].S)*t;}
            }
            return result;
        }
        internal bool Sample(float distance,float lateral,out Vector3 point)
        {
            point=Head;
            if(distance<Distance-MaxLength||distance>Distance+10000)return false;
            Vector3 forward=Forward;
            if(distance>=Distance) point=Head+forward*(distance-Distance);
            else if(distance<=points[0].S)
            {
                forward=points.Count>1?Normalize(points[1].P-points[0].P):InitialForward;
                point=points[0].P+forward*(distance-points[0].S);
            }
            else
            {
                int lo=1,hi=points.Count-1;
                while(lo<hi) {int mid=(lo+hi)/2;if(points[mid].S<distance)lo=mid+1;else hi=mid;}
                var a=points[lo-1];var b=points[lo];
                float t=(distance-a.S)/(b.S-a.S);
                point=a.P+(b.P-a.P)*t;forward=Normalize(b.P-a.P);
            }
            point+=Vector3.Cross(Vector3.up,forward)*lateral;
            return true;
        }
    }
}
