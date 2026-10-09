using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using HarmonyLib;
using MelonLoader;
using Newtonsoft.Json;
using Silica.AI;

namespace Si_Formation
{
    public enum ChangeResult { Success, NotReady, WrongThread, InvalidPlayer, InvalidLimit, AdminOverride, InvalidFunction }

    /// <summary>Server main-thread API. IDs are connected players' SteamID64 values. Limits include the leader.</summary>
    public static class FormationApi
    {
        public static bool IsReady=>Formations.ApiReady;
        public static bool GlobalEnabled=>Formations.ApiEnabled;
        public static bool GlobalRequestedEnabled=>Formations.GlobalRequested;
        public static int CommanderFormationOrderLimit=>Formations.CommanderOrderLimit;
        public static ChangeResult TryGetPlayerActivation(ulong steamId,out bool personalEnabled,out bool effectiveEnabled)
            =>Formations.ApiActivation(steamId,out personalEnabled,out effectiveEnabled);
        public static ChangeResult TryGetFunctionEnabled(ulong steamId,string function,out bool enabled)
            =>Formations.ApiFunctionEnabled(steamId,function,out enabled);
        public static int MatchGroupLimit=>Formations.MatchLimit;
        public static ChangeResult TryGetPlayerLimit(ulong steamId,out int limit)=>Formations.ApiPlayerLimit(steamId,out limit);
        public static ChangeResult RequestGlobalEnabled(bool enabled)=>Formations.ApiGlobal(enabled);
        public static ChangeResult RequestMatchGroupLimit(int limit)=>Formations.ApiMatchLimit(limit);
        public static ChangeResult RequestPlayerGroupLimit(ulong steamId,int limit)=>Formations.ApiSetPlayerLimit(steamId,limit,false);
        public static ChangeResult ClearPlayerGroupLimit(ulong steamId)=>Formations.ApiSetPlayerLimit(steamId,0,true);
        public static ChangeResult TryGetCommanderEnabled(ulong steamId,out bool enabled)=>Formations.ApiCommanderEnabled(steamId,out enabled);
    }

    public sealed partial class Formations
    {
        private const int BuiltInLimit=10,MaximumLimit=50;
        private static MelonPreferences_Entry<bool> enabledDefault,debugDefault;
        private static MelonPreferences_Entry<int> groupLimitDefault,commanderOrderLimit;
        private static readonly Dictionary<string,MelonPreferences_Entry<int>> formationDefaults=new Dictionary<string,MelonPreferences_Entry<int>>();
        private static readonly Dictionary<string,FormationDefinition> matchFormationDefaults=new Dictionary<string,FormationDefinition>();
        private static string DefaultKey(string team,string function)=>"Default"+team+char.ToUpperInvariant(function[0])+function.Substring(1);
        private static void RegisterFormationDefaults()
        {
            formationDefaults.Clear();
            foreach(string team in new[]{"Sol","Centauri","Alien"})
                foreach(string function in new[]{"move","follow","attack","commander"})
                {
                    string key=DefaultKey(team,function);
                    formationDefaults[key]=prefs.CreateEntry(key,1,description:team+" "+function+" initial menu option: 1=native, 2..9999=exact menu number; applied each round and on fresh team/role state");
                }
        }
        private static void LoadFormationDefaults()
        {
            matchFormationDefaults.Clear();
            foreach(string team in new[]{"Sol","Centauri","Alien"})
                foreach(string function in new[]{"move","follow","attack","commander"})
                {
                    string key=DefaultKey(team,function);
                    int number=formationDefaults[key].Value;
                    if(number==1)continue;
                    var definition=Catalog.Definitions.Values.FirstOrDefault(f=>f.Team==team&&f.Function==function&&f.MenuOrder==number);
                    if(number<2||number>9999||definition==null)
                    {MelonLogger.Warning("[Si_Formation] "+key+"="+number+" has no valid formation menu option; using native (1).");continue;}
                    matchFormationDefaults[key]=definition;
                }
        }
        private static void ApplyFormationDefaults(Selection selection)
        {
            string team=TeamName(selection.Team);
            if(team==null)return;
            foreach(string function in selection.CommanderRole?new[]{"commander"}:new[]{"move","follow","attack"})
                if(matchFormationDefaults.TryGetValue(DefaultKey(team,function),out var definition))
                    selection.Functions[function]=definition;
            selection.PersonalEnabled=selection.CommanderRole||selection.Functions.Count>0;
        }
        internal static int CommanderOrderLimit=>commanderOrderLimit!=null&&ValidCommanderLimit(commanderOrderLimit.Value)?commanderOrderLimit.Value:75;
        private static bool ValidCommanderLimit(int value)=>value>=1&&value<=100;
        private static void CommanderSizeCommand(Player p,string args)
        {
            if(!IsAdministrator(p)){Reply(p,"permission denied: admin Generic power required.");return;}
            if(!ApiReady){Reply(p,"server not initialized.");return;}
            if(!CommandArgument.TryParse(args,out string value,out bool quoted)||quoted)
            {Reply(p,"usage: /formationsizecommander <1..100> | status");return;}
            if(value.Length==0||value.Equals("status",StringComparison.OrdinalIgnoreCase))
            {Reply(p,"commander order maximum: "+CommanderOrderLimit);return;}
            if(!int.TryParse(value,NumberStyles.None,CultureInfo.InvariantCulture,out int limit)||!ValidCommanderLimit(limit))
            {Reply(p,"usage: /formationsizecommander <1..100> | status");return;}
            commanderOrderLimit.Value=limit;
            try{prefs.SaveToFile(false);FormatPreferences();Reply(p,"commander order maximum: "+limit+" (saved)");}
            catch(Exception e){MelonLogger.Error("Saving CommanderOrderLimit: "+e);Reply(p,"commander order maximum: "+limit+"; saving failed, see server log.");}
        }
        private static int serverThread,configuredLimit=BuiltInLimit;
        private static bool configuredEnabled=true,adminGlobal;
        private static int? adminMatchLimit,apiMatchLimit;
        private static readonly Dictionary<ulong,int> AdminLimits=new Dictionary<ulong,int>();
        private static readonly Dictionary<ulong,int> ApiLimits=new Dictionary<ulong,int>();
        internal static bool ApiReady=>Ready&&Game.GetIsServer();
        internal static bool ApiEnabled=>Running;
        internal static bool GlobalRequested=>Enabled;
        internal static ChangeResult ApiActivation(ulong id,out bool personal,out bool effective)
        {
            personal=false;effective=false;var result=ApiCheck();if(result!=ChangeResult.Success)return result;
            var p=Connected(id);if(!p)return ChangeResult.InvalidPlayer;
            personal=For(p).PersonalEnabled;effective=EffectivePersonal(p);return ChangeResult.Success;
        }
        internal static ChangeResult ApiFunctionEnabled(ulong id,string function,out bool enabled)
        {
            enabled=false;var result=ApiCheck();if(result!=ChangeResult.Success)return result;
            if(!new[]{"move","follow","attack","commander"}.Contains(function))return ChangeResult.InvalidFunction;
            var p=Connected(id);if(!p)return ChangeResult.InvalidPlayer;
            enabled=FunctionActive(p,function);return ChangeResult.Success;
        }
        internal static int MatchLimit=>adminMatchLimit??apiMatchLimit??configuredLimit;
        private static bool ValidLimit(int value)=>value>=1&&value<=MaximumLimit;
        private static ulong PlayerId(Player p)=>p?p.PlayerID.SteamID.m_SteamID:0;
        private static Player Connected(ulong id)=>id==0?null:Player.Players.FirstOrDefault(p=>p&&PlayerId(p)==id);
        private static ChangeResult ApiCheck()
        {
            if(Thread.CurrentThread.ManagedThreadId!=serverThread)return ChangeResult.WrongThread;
            return ApiReady?ChangeResult.Success:ChangeResult.NotReady;
        }
        private static int ConfiguredPlayerLimit(Player p)
        {
            ulong id=PlayerId(p);
            if(AdminLimits.TryGetValue(id,out int limit))return limit;
            if(adminMatchLimit.HasValue)return adminMatchLimit.Value;
            return ApiLimits.TryGetValue(id,out limit)?limit:MatchLimit;
        }
        private static int EffectiveLimit(Player p)=>Running?ConfiguredPlayerLimit(p):
            (FPSCommanding.Instance?FPSCommanding.Instance.GroupUnitLimit:8);
        internal static ChangeResult ApiPlayerLimit(ulong id,out int limit)
        {
            limit=0;var result=ApiCheck();if(result!=ChangeResult.Success)return result;
            var p=Connected(id);if(!p)return ChangeResult.InvalidPlayer;
            limit=EffectiveLimit(p);return ChangeResult.Success;
        }
        internal static ChangeResult ApiCommanderEnabled(ulong id,out bool enabled)
        {
            enabled=false;var result=ApiCheck();if(result!=ChangeResult.Success)return result;
            var p=Connected(id);if(!p)return ChangeResult.InvalidPlayer;
            enabled=FunctionActive(p,"commander");return ChangeResult.Success;
        }
        internal static ChangeResult ApiGlobal(bool enabled)
        {
            var result=ApiCheck();if(result!=ChangeResult.Success)return result;
            if(adminGlobal)return ChangeResult.AdminOverride;
            SetGlobal(enabled);return ChangeResult.Success;
        }
        internal static ChangeResult ApiMatchLimit(int limit)
        {
            var result=ApiCheck();if(result!=ChangeResult.Success)return result;
            if(!ValidLimit(limit))return ChangeResult.InvalidLimit;
            if(adminMatchLimit.HasValue)return ChangeResult.AdminOverride;
            apiMatchLimit=limit;LimitsChanged();return ChangeResult.Success;
        }
        internal static ChangeResult ApiSetPlayerLimit(ulong id,int limit,bool clear)
        {
            var result=ApiCheck();if(result!=ChangeResult.Success)return result;
            if(!Connected(id))return ChangeResult.InvalidPlayer;
            if(!clear&&!ValidLimit(limit))return ChangeResult.InvalidLimit;
            if(adminMatchLimit.HasValue||AdminLimits.ContainsKey(id))return ChangeResult.AdminOverride;
            if(clear)ApiLimits.Remove(id);else ApiLimits[id]=limit;
            LimitsChanged();return ChangeResult.Success;
        }
        private static void SetGlobal(bool enabled)
        {
            ClearFollow();ClearOrders();Enabled=enabled;
            if(enabled)Faulted=false;
            nextCheck=0;EnforceLimits();
        }
        private static void LoadDefaults()
        {
            // Read only our registered entries. Reloading the shared preferences file here
            // would refresh every other mod's categories too. MelonLoader owns disk loading.
            configuredEnabled=enabledDefault?.Value??true;
            configuredLimit=groupLimitDefault?.Value??BuiltInLimit;
            debug=debugDefault?.Value??false;
            if(!ValidLimit(configuredLimit))
            {
                MelonLogger.Warning("[Si_Formation] GroupLimit must be 1..50; using 10.");
                configuredLimit=BuiltInLimit;
            }
        }
        private static void ResetMatchSettings()
        {
            LoadDefaults();LoadFormationDefaults();adminGlobal=false;adminMatchLimit=null;apiMatchLimit=null;
            AdminLimits.Clear();ApiLimits.Clear();Menus.Clear();Enabled=configuredEnabled;Faulted=false;
        }
        private static void GlobalSizeCommand(Player p,string args)
        {
            if(!IsAdministrator(p)){Reply(p,"permission denied: admin Generic power required.");return;}
            if(!ApiReady){Reply(p,"server not initialized.");return;}
            if(!CommandArgument.TryParse(args,out string value,out bool quoted)||quoted||!int.TryParse(value,NumberStyles.None,CultureInfo.InvariantCulture,out int limit)||!ValidLimit(limit))
            {Reply(p,"usage: /formationsizeglobal <1..50>; includes the controlled leader.");return;}
            adminMatchLimit=limit;LimitsChanged();Reply(p,"match-wide FPS group maximum: "+limit+" (including leader)");
        }
        private static void SizeCommand(Player p,string args)
        {
            if(!IsAdministrator(p)){Reply(p,"permission denied: admin Generic power required.");return;}
            if(!ApiReady){Reply(p,"server not initialized.");return;}
            string text=(args??"").Trim();int first=text.IndexOfAny(new[]{' ','\t'});
            text=first<0?"":text.Substring(first).Trim();
            int last=text.LastIndexOfAny(new[]{' ','\t'});
            if(last<0){Reply(p,"For the match-wide limit use /formationsizeglobal <1..50>. For one player use /formationsize \"Player Name\" <1..50>. Limits include the leader.");return;}
            string size=text.Substring(last+1);
            if(!int.TryParse(size,NumberStyles.None,CultureInfo.InvariantCulture,out int limit)||!ValidLimit(limit))
            {Reply(p,"usage: /formationsize <player name> <1..50>; includes leader.");return;}
            string name=text.Substring(0,last).Trim();
            if(name.StartsWith("\""))
            {try{name=JsonConvert.DeserializeObject<string>(name);}catch{Reply(p,"invalid quoted player name.");return;}}
            if(string.IsNullOrWhiteSpace(name)||name.Any(char.IsControl)){Reply(p,"invalid player name.");return;}
            var matches=Player.Players.Where(player=>player&&string.Equals(player.PlayerName,name,StringComparison.OrdinalIgnoreCase)).ToArray();
            if(matches.Length!=1){Reply(p,"use an exact, unique connected player name (spaces allowed); matches: "+matches.Length);return;}
            ulong id=PlayerId(matches[0]);
            if(id==0){Reply(p,"player has no stable Steam identifier.");return;}
            AdminLimits[id]=limit;LimitsChanged();Reply(p,SafeChat(matches[0].PlayerName)+" group maximum: "+limit);
        }
        private static void LimitsChanged(){ClearFollow();ClearOrders();EnforceLimits();nextCheck=0;}
        private static void EnforceLimits()
        {
            if(!Running)return;
            foreach(var p in Player.Players.ToArray())
            {
                if(!p||p.IsCommander||!p.ControlledUnit||p.Group==null||p.Group.Leader!=p.ControlledUnit)continue;
                var group=p.Group;int limit=ConfiguredPlayerLimit(p);
                foreach(var unit in group.Units.Where(u=>u&&u!=p.ControlledUnit&&!u.PlayerControlled&&!(u.Driver&&u.Driver.ControlledBy)).Reverse().ToArray())
                {
                    if(group.UnitCount<=limit)break;
                    CancelPlayer(p);ResetLeader(p.ControlledUnit);group.RemoveUnit(unit);
                }
            }
        }
        private void PatchAdminInviteLimit()
        {
            const string typeName="SilicaAdminMod.Event_Units+ApplyPatch_FPSCommanding_InviteToGroupServer";
            var type=typeof(SilicaAdminMod.Event_Units).Assembly.GetType(typeName);
            var method=type?.GetMethod("Prefix",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static,
                null,new[]{typeof(FPSCommanding),typeof(Player),typeof(Target)},null);
            if(method==null||method.ReturnType!=typeof(bool))
                throw new InvalidOperationException("Unsupported Admin Mod recruitment prefix: expected static bool "+typeName+".Prefix(FPSCommanding, Player, Target).");
            // Admin Mod rejects full groups before the vanilla method runs. Adapt only its
            // limit read, preserving validation, request events and other mods' vetoes.
            // Both this static prefix and the vanilla instance method put Player in arg 1.
            HarmonyInstance.Patch(method,transpiler:new HarmonyMethod(typeof(Formations),nameof(InviteLimit)));
        }
        // Replace only limit reads in vanilla recruitment and Admin Mod's prefix;
        // never mutate the shared FPSCommanding field. UnpatchSelf removes both patches.
        private static IEnumerable<CodeInstruction> InviteLimit(IEnumerable<CodeInstruction> instructions,MethodBase original)
        {
            int count=0;var field=AccessTools.Field(typeof(FPSCommanding),"GroupUnitLimit");
            foreach(var instruction in instructions)
            {
                if(instruction.opcode==OpCodes.Ldfld&&Equals(instruction.operand,field))
                {
                    // Reuse the instruction so branch labels and exception boundaries stay intact.
                    instruction.opcode=OpCodes.Ldarg_1;instruction.operand=null;
                    yield return instruction;yield return new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(Formations),nameof(RecruitLimit)));count++;
                }
                else yield return instruction;
            }
            if(count!=1)throw new InvalidOperationException("Expected one FPS recruitment limit field read in "+original.DeclaringType.FullName+"."+original.Name+"; found "+count+".");
        }
        private static int RecruitLimit(FPSCommanding instance,Player player)=>Running&&player?ConfiguredPlayerLimit(player):instance.GroupUnitLimit;
        private static bool GroupAdding(AIGroup __instance,Unit unit,ref bool __result)
        {
            if(!Running||!unit||unit.PlayerControlled||!__instance.Leader||!__instance.Leader.ControlledBy)return true;
            var player=__instance.Leader.ControlledBy;
            if(player.IsCommander||player.Group!=__instance||__instance.Units.Contains(unit))return true;
            if(__instance.UnitCount<ConfiguredPlayerLimit(player))return true;
            __result=false;return false;
        }
        private static bool RecruitAuthorized(Player player,Target target)
        {
            if(!Running)return true;
            return player&&!player.IsCommander&&(!networkSender||networkSender==player)&&target&&target.OwnerUnit&&
                target.OwnerUnit.Team==player.Team&&!target.OwnerUnit.PlayerControlled&&
                !(target.OwnerUnit.Driver&&target.OwnerUnit.Driver.ControlledBy)&&
                (target.OwnerUnit.AIGroup==null||!target.OwnerUnit.AIGroup.PlayerControlled);
        }
    }
}
