using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Deterministic stand-ins for engine navigation and unit lifetime. Links the actual
// production search and JSON definition code; does not claim Unity/pathfinding validation.
namespace UnityEngine { public struct Vector3 { public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;} } }
namespace Silica.AI
{
    public struct OrderTarget { public Vector3 Point;public OrderTarget(Vector3 p){Point=p;} }
    public static class OrderDefinitionRegistry { public static object Move=new object(); }
}
public class Unit
{
    public string Id,Type="Tank";public float Radius=1;public bool Live=true;
    public Agent OrderAgent=new Agent();
    public class Agent { public bool CanIssueOrder(object order,in Silica.AI.OrderTarget target)=>true; }
}
namespace Si_Formation
{
    internal static class EditorCategories { internal static HashSet<string> Names=new HashSet<string>{"Tank"}; }
    internal static class TerrainProjection
    {
        internal static int Calls;
        internal static Func<Unit,Vector3,bool> Accept=(u,p)=>true;
        internal static bool TryProject(Unit u,Vector3 point,out Vector3 projected){Calls++;projected=point;return Accept(u,point);}
    }
    public sealed partial class Formations
    {
        private sealed class Placement { internal Unit Unit;internal int Slot;internal float Radius;internal Vector3 Point; }
        private static string UnitId(Unit u)=>u.Id;
        private static string PreferenceType(Unit u)=>u.Type;
        private static float Radius(Unit u,FormationDefinition f,FormationSlot s)=>u.Radius;
        private static Vector3 World(FormationSlot s,FormationDefinition f,Vector3 c,Vector3 h)=>new Vector3(s.X,0,s.Z);
        private static bool Separated(Vector3 p,float r,IEnumerable<Placement> others)=>others.All(q=>Math.Pow(p.x-q.Point.x,2)+Math.Pow(p.z-q.Point.z,2)>=Math.Pow(r+q.Radius,2));
        private static FormationSlot Slot(int index,int colour,params string[] ids)=>new FormationSlot(index*20,0,"top",1,ids,colour);
        private static Unit[] Units(int n)=>Enumerable.Range(0,n).Select(i=>new Unit{Id="U"+i}).ToArray();
        private static void Check(bool pass,string text){if(!pass)throw new Exception(text);Console.WriteLine("PASS "+text);}
        private static List<Placement> Run(Unit[] units,FormationSlot[] slots,Func<Placement,bool> issue=null,List<Placement> reserved=null,Action<int> onStep=null)
        {
            var f=new FormationDefinition("test","Sol","move",false,20,slots);
            reserved=reserved??new List<Placement>();var emitted=new List<Placement>();
            var search=SearchPositions(f,units,new Vector3(),new Vector3(),reserved,u=>u.Live,p=>{if(issue!=null&&!issue(p))return false;emitted.Add(p);return true;}).GetEnumerator();
            int steps=0;
            while(true){int before=TerrainProjection.Calls;if(!search.MoveNext())break;if(TerrainProjection.Calls-before>1)throw new Exception("unbounded navigation step");onStep?.Invoke(++steps);if(steps>2000000)throw new Exception("search stalled");}
            return emitted;
        }
        public static void Main()
        {
            var units=Units(18);var slots=Enumerable.Range(0,20).Select(i=>Slot(i,new[]{0,1,2,4}[i/5])).ToArray();
            var placed=Run(units,slots);
            Check(placed.GroupBy(p=>FormationDefinition.Colour(slots[p.Slot])).Select(g=>g.Count()).SequenceEqual(new[]{5,5,5,3}),"18 units: 5 green, 5 yellow, 5 orange, 3 red");
            Check(placed.Select(p=>FormationDefinition.Colour(slots[p.Slot])).SequenceEqual(placed.Select(p=>FormationDefinition.Colour(slots[p.Slot])).OrderBy(x=>x)),"colour order is global");
            units=Units(2);slots=new[]{Slot(0,0,"U0"),Slot(1,0)};
            placed=Run(units,slots);Check(placed.Single(p=>p.Unit.Id=="U0").Slot==0,"explicit match survives flexible fallback");
            TerrainProjection.Accept=(u,p)=>u.Id=="U0"||p.x==0;
            placed=Run(units,new[]{Slot(0,0),Slot(1,0)});
            Check(placed.Count==2&&placed.Single(p=>p.Unit.Id=="U1").Slot==0,"augmenting path reserves scarce reachable slot");
            TerrainProjection.Accept=(u,p)=>p.x>=6000;
            slots=Enumerable.Range(0,303).Select(i=>Slot(i,i<300?0:i==300?1:i==301?2:4)).ToArray();
            placed=Run(Units(3),slots);Check(placed.Select(p=>p.Slot).SequenceEqual(new[]{300,301,302}),"failed early candidates cannot hide yellow, orange or red");
            TerrainProjection.Accept=(u,p)=>true;
            placed=Run(Units(1),new[]{Slot(0,0,"another"),Slot(1,1,"U0")});Check(placed[0].Slot==0,"colour outranks individual preference");
            placed=Run(Units(1),new[]{Slot(0,0),Slot(1,0)},p=>p.Slot!=0);Check(placed[0].Slot==1,"failed order leaves alternative candidates available");
            units=Units(2);var held=new Placement{Unit=units[0],Slot=0,Point=new Vector3(0,0,0),Radius=1};
            placed=Run(units,new[]{Slot(0,0),Slot(1,1)},reserved:new List<Placement>{held});Check(placed.Count==1&&placed[0].Unit==units[1]&&placed[0].Slot==1,"successful reservation survives retry");
            units=Units(2);placed=Run(units,new[]{Slot(0,0),Slot(1,0)},onStep:n=>{if(n==1)units[0].Live=false;});Check(placed.All(p=>p.Unit!=units[0]),"cancellation during staged search prevents orders");
            units=Units(2);units[0].Radius=20;
            placed=Run(units,new[]{Slot(0,0),Slot(1,0),Slot(2,1)});Check(placed.Count==2&&placed[1].Slot==2,"physical spacing may reject a higher-colour position");
            Check(FormationDefinition.Colour(Slot(0,3))==FormationDefinition.Colour(Slot(0,4)),"legacy and current red markers share a colour tier");
        }
    }
}
