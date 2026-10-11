using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Reflection.Emit;
using HarmonyLib;
using MelonLoader;

namespace Si_Formation
{
    public sealed partial class Formations
    {
        private static string PreferencesPath=>Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory,"Formations_cfg","si_formation.cfg");
        private static string preferenceLayout;
        private void InstallPreferenceLayout()
        {
            // Capture the administrator's comments/order before MelonLoader loads or saves.
            if(File.Exists(PreferencesPath))preferenceLayout=File.ReadAllText(PreferencesPath);
            var fileType=typeof(MelonPreferences).Assembly.GetType("MelonLoader.Preferences.IO.File",true);
            Patch(fileType,"Save",transpiler:nameof(PreferenceSaveLayout));
        }
        private static IEnumerable<CodeInstruction> PreferenceSaveLayout(IEnumerable<CodeInstruction> instructions)
        {
            var write=AccessTools.Method(typeof(File),nameof(File.WriteAllText),new[]{typeof(string),typeof(string)});
            var replacement=AccessTools.Method(typeof(Formations),nameof(WritePreferenceFile));
            int count=0;
            foreach(var instruction in instructions)
            {
                if(instruction.Calls(write)){instruction.opcode=OpCodes.Call;instruction.operand=replacement;count++;}
                yield return instruction;
            }
            if(count!=1)throw new InvalidOperationException("Expected one preferences file write.");
        }
        private static void WritePreferenceFile(string path,string contents)
        {
            // Format the serialized values before the write, including loader-wide saves.
            // Other preferences files use exactly the original writer and contents.
            if(string.Equals(Path.GetFullPath(path),Path.GetFullPath(PreferencesPath),StringComparison.OrdinalIgnoreCase))
                contents=FormatPreferenceText(contents);
            File.WriteAllText(path,contents);
        }
        private static void FormatPreferences()
        {
            try
            {
                if(!File.Exists(PreferencesPath))return;
                string original=File.ReadAllText(PreferencesPath),formatted=FormatPreferenceText(original);
                if(formatted!=original)File.WriteAllText(PreferencesPath,formatted,new UTF8Encoding(false));
            }
            catch(Exception e){DebugWarning("Could not format si_formation.cfg: "+e.Message);}
        }
        private static string FormatPreferenceText(string original)
        {
            try
            {
                var values=new Dictionary<string,string>(StringComparer.Ordinal);
                var entry=new Regex(@"^([A-Za-z][A-Za-z0-9_]*)\s*=\s*(.*)$");
                bool section=false;
                foreach(string raw in original.Split('\n'))
                {
                    string line=raw.Trim();
                    if(line.Length==0||line.StartsWith("#"))continue;
                    if(Regex.IsMatch(line,@"^\[\s*[""']?Si_Formation[""']?\s*\]\s*(#.*)?$")&&!section){section=true;continue;}
                    var match=entry.Match(line);
                    // Do not rewrite files with extra sections, duplicate keys or syntax
                    // outside this simple preferences layout; never touch other categories.
                    if(!section||!match.Success||values.ContainsKey(match.Groups[1].Value))return original;
                    string key=match.Groups[1].Value,value=match.Groups[2].Value;
                    if(key.StartsWith("StartDelay",StringComparison.Ordinal)&&float.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out float delay))
                        value=delay.ToString("G7",CultureInfo.InvariantCulture);
                    values.Add(key,value);
                }
                if(!section)return original;
                string template=preferenceLayout;
                if(string.IsNullOrEmpty(template))
                {
                    using(var input=typeof(Formations).Assembly.GetManifestResourceStream("Si_Formation.Preferences.cfg"))
                    using(var reader=new StreamReader(input))template=reader.ReadToEnd();
                }
                var result=new StringBuilder();
                foreach(string raw in template.Split('\n'))
                {
                    string line=raw.TrimEnd('\r');var match=entry.Match(line);
                    if(!match.Success){result.AppendLine(line);continue;}
                    string key=match.Groups[1].Value;
                    // Missing settings stay missing until MelonLoader creates them.
                    if(values.TryGetValue(key,out string value)){result.AppendLine(key+" = "+value);values.Remove(key);}
                }
                if(values.Count>0)
                {
                    result.AppendLine("# === Additional preserved settings ===");
                    foreach(var value in values)result.AppendLine(value.Key+" = "+value.Value);
                }
                string formatted=result.ToString().TrimEnd()+Environment.NewLine;
                return formatted;
            }
            catch(Exception e){DebugWarning("Could not format si_formation.cfg: "+e.Message);}
            return original;
        }
    }
}
