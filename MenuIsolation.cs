using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace Si_Formation
{
    public sealed partial class Formations
    {
        private sealed class ForeignMenu
        {
            internal string Name;
            internal FieldInfo States,Level;
            internal bool ProbeFailed;
        }
        private static readonly List<ForeignMenu> ForeignMenus=new List<ForeignMenu>();
        private static bool menusProbed;
        private static void ProbeMenus()
        {
            menusProbed=true;
            foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var koh=assembly.GetType("Si_KingOfTheHill.KingOfTheHill");
                if(koh!=null)ProbeMenu(koh,"_buyStates","King of the Hill /buy (close with /buy close)",true);
                var balance=assembly.GetType("Si_UnitBalance.UnitBalance");
                if(balance!=null)
                {ProbeMenu(balance,"_menuStates","Unit Balance /b (close with /b)",false);ProbeMenu(balance,"_statsStates","Unit Balance stats (close with /exit)",false);}
            }
        }
        private static void ProbeMenu(Type type,string field,string name,bool hasClosedLevel)
        {
            var probe=new ForeignMenu {Name=name,States=type.GetField(field,BindingFlags.Static|BindingFlags.NonPublic)};
            if(probe.States==null||!typeof(IDictionary).IsAssignableFrom(probe.States.FieldType))probe.ProbeFailed=true;
            else if(hasClosedLevel)
            {
                var state=probe.States.FieldType.GetGenericArguments()[1];probe.Level=state.GetField("Level");
                probe.ProbeFailed=probe.Level==null||!probe.Level.FieldType.IsEnum||Enum.GetName(probe.Level.FieldType,0)!="Closed";
            }
            ForeignMenus.Add(probe);
        }
        private static bool MenuAvailable(Player p,bool explain)
        {
            if(!menusProbed)ProbeMenus();
            foreach(var probe in ForeignMenus)
            {
                bool active=false,unknown=probe.ProbeFailed;
                try
                {
                    if(!unknown)
                    {
                        var states=probe.States.GetValue(null) as IDictionary;
                        if(states==null)unknown=true;
                        else
                        {
                            object state=states[(long)p.PlayerID.m_ID];
                            active=state!=null&&(probe.Level==null||Convert.ToInt32(probe.Level.GetValue(state))!=0);
                        }
                    }
                }
                catch {unknown=true;}
                if(!active&&!unknown)continue;
                if(explain)Reply(p,unknown?"Formation menu unavailable: cannot safely detect "+probe.Name+" menu ownership.":
                    "Close "+probe.Name+" first. No shared menu cancellation API is available.");
                return false;
            }
            return true;
        }
    }
}
