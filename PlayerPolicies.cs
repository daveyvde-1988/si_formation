using System;
using System.Collections.Generic;
using System.Linq;

namespace Si_Formation
{
    public static partial class FormationApi
    {
        /// <summary>Store an owner's FPS policy. Category rules are native Type or Type:Weight (or *).
        /// Null rules permit all units; an empty array permits only explicit names. Denied names win.</summary>
        public static ChangeResult RequestPlayerPolicy(ulong steamId,string owner,int limit,string[] allowedCategories,string[] allowedObjectNames,string[] deniedObjectNames)
            =>Formations.ApiPolicy(steamId,owner,limit,allowedCategories,allowedObjectNames,deniedObjectNames);
        public static ChangeResult TryGetPlayerPolicy(ulong steamId,out string owner,out int limit,out string[] allowedCategories,out string[] allowedObjectNames,out string[] deniedObjectNames)
            =>Formations.ApiReadPolicy(steamId,out owner,out limit,out allowedCategories,out allowedObjectNames,out deniedObjectNames);
        public static ChangeResult ClearPlayerPolicy(ulong steamId,string owner)=>Formations.ApiClearPolicy(steamId,owner);
        public static ChangeResult ClearOwnerPolicies(string owner)=>Formations.ApiClearOwner(owner);
        /// <summary>Parallel arrays of initialized native ObjectInfo definitions; no editor category overrides.</summary>
        public static ChangeResult TryGetUnitDefinitions(out string[] objectNames,out string[] unitTypes,out string[] unitWeights)
            =>Formations.ApiDefinitions(out objectNames,out unitTypes,out unitWeights);
        public static string[] GetNativeUnitCategories()=>Enum.GetNames(typeof(UnitType)).SelectMany(t=>Enum.GetNames(typeof(UnitWeight)).Select(w=>t+":"+w)).ToArray();
    }

    public sealed partial class Formations
    {
        private sealed class PlayerPolicy
        {
            internal string Owner;
            internal int Limit;
            internal string[] Categories,Allowed,Denied;
        }
        private static readonly Dictionary<ulong,PlayerPolicy> PlayerPolicies=new Dictionary<ulong,PlayerPolicy>();
        private static bool AdminPolicyOverride(ulong id)=>adminMatchLimit.HasValue||AdminLimits.ContainsKey(id);
        private static bool ValidOwner(string owner)=>!string.IsNullOrWhiteSpace(owner)&&owner.Length<=128&&!owner.Any(char.IsControl);
        private static bool ValidNames(string[] names)=>names==null||names.All(n=>!string.IsNullOrWhiteSpace(n)&&!n.Any(char.IsControl));
        private static bool ValidCategory(string rule)
        {
            if(rule=="*")return true;
            if(rule==null)return false;
            var parts=rule.Split(':');
            return parts.Length>=1&&parts.Length<=2&&Enum.GetNames(typeof(UnitType)).Contains(parts[0])&&
                (parts.Length==1||parts[1]=="*"||Enum.GetNames(typeof(UnitWeight)).Contains(parts[1]));
        }
        private static string[] Copy(string[] values)=>values==null?null:(string[])values.Clone();
        private static bool Same(string[] first,string[] second)=>first==null?second==null:second!=null&&first.SequenceEqual(second);
        internal static ChangeResult ApiPolicy(ulong id,string owner,int limit,string[] categories,string[] allowed,string[] denied)
        {
            var result=ApiCheck();if(result!=ChangeResult.Success)return result;
            if(!Connected(id))return ChangeResult.InvalidPlayer;
            if(!ValidLimit(limit))return ChangeResult.InvalidLimit;
            if(!ValidOwner(owner)||!ValidNames(allowed)||!ValidNames(denied)||(categories!=null&&!categories.All(ValidCategory)))return ChangeResult.InvalidPolicy;
            if(PlayerPolicies.TryGetValue(id,out var existing)&&existing.Owner!=owner)return ChangeResult.OwnerConflict;
            if(existing!=null&&existing.Limit==limit&&Same(existing.Categories,categories)&&Same(existing.Allowed,allowed)&&Same(existing.Denied,denied))
                return AdminPolicyOverride(id)?ChangeResult.AdminOverride:ChangeResult.Success;
            // Store even while an administrator shadows this policy, so it remains inspectable.
            PlayerPolicies[id]=new PlayerPolicy {Owner=owner,Limit=limit,Categories=Copy(categories),Allowed=Copy(allowed),Denied=Copy(denied)};
            LimitsChanged();return AdminPolicyOverride(id)?ChangeResult.AdminOverride:ChangeResult.Success;
        }
        internal static ChangeResult ApiReadPolicy(ulong id,out string owner,out int limit,out string[] categories,out string[] allowed,out string[] denied)
        {
            owner=null;limit=0;categories=allowed=denied=null;
            var result=ApiCheck();if(result!=ChangeResult.Success)return result;
            if(!PlayerPolicies.TryGetValue(id,out var policy))return ChangeResult.NoPolicy;
            owner=policy.Owner;limit=policy.Limit;categories=Copy(policy.Categories);allowed=Copy(policy.Allowed);denied=Copy(policy.Denied);
            return AdminPolicyOverride(id)?ChangeResult.AdminOverride:ChangeResult.Success;
        }
        internal static ChangeResult ApiClearPolicy(ulong id,string owner)
        {
            var result=ApiCheck();if(result!=ChangeResult.Success)return result;
            if(!ValidOwner(owner))return ChangeResult.InvalidPolicy;
            if(!PlayerPolicies.TryGetValue(id,out var policy))return ChangeResult.NoPolicy;
            if(policy.Owner!=owner)return ChangeResult.OwnerConflict;
            PlayerPolicies.Remove(id);LimitsChanged();return ChangeResult.Success;
        }
        internal static ChangeResult ApiClearOwner(string owner)
        {
            var result=ApiCheck();if(result!=ChangeResult.Success)return result;
            if(!ValidOwner(owner))return ChangeResult.InvalidPolicy;
            foreach(var id in PlayerPolicies.Where(e=>e.Value.Owner==owner).Select(e=>e.Key).ToArray())PlayerPolicies.Remove(id);
            LimitsChanged();return ChangeResult.Success;
        }
        internal static ChangeResult ApiDefinitions(out string[] names,out string[] types,out string[] weights)
        {
            names=types=weights=Array.Empty<string>();
            var result=ApiCheck();if(result!=ChangeResult.Success)return result;
            if(!ObjectInfo.AllInitialized)return ChangeResult.NotReady;
            var infos=ObjectInfo.ObjectDatas.Where(i=>i&&i.Unit).OrderBy(i=>i.name,StringComparer.Ordinal).ToArray();
            names=infos.Select(i=>i.name).ToArray();types=infos.Select(i=>i.UnitType.ToString()).ToArray();weights=infos.Select(i=>i.UnitWeight.ToString()).ToArray();
            return ChangeResult.Success;
        }
        private static bool PolicyAllows(Player player,Unit unit)
        {
            ulong id=PlayerId(player);
            if(AdminPolicyOverride(id)||!PlayerPolicies.TryGetValue(id,out var policy))return true;
            var info=unit.ObjectInfo;if(!info)return false;
            if(policy.Denied!=null&&policy.Denied.Contains(info.name))return false;
            if(policy.Allowed!=null&&policy.Allowed.Contains(info.name))return true;
            if(policy.Categories==null)return true;
            string type=info.UnitType.ToString(),weight=info.UnitWeight.ToString();
            return policy.Categories.Any(rule=>rule=="*"||rule==type||rule==type+":*"||rule==type+":"+weight);
        }
    }
}
