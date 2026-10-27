using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

// Explicit standalone pure C# execution. This does not host Unity or replace native Test Runner evidence.
public static class IndependentPureRunner
{
    private static string root;
    private static string Q(string s) { return "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t") + "\""; }
    public static int Main(string[] args)
    {
        root = Path.GetFullPath(args[0]);
        AppDomain.CurrentDomain.AssemblyResolve += (s,e) =>
        {
            string name = new AssemblyName(e.Name).Name + ".dll";
            string[] dirs = { Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                Path.Combine(root,"Library/ScriptAssemblies"),
                Path.Combine(root,"Library/PackageCache/com.unity.ext.nunit@1.0.6/net35/unity-custom"),
                "C:/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Data/Managed/UnityEngine",
                "C:/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Data/Managed" };
            foreach (string dir in dirs) { string p = Path.Combine(dir,name); if (File.Exists(p)) return Assembly.LoadFrom(p); }
            return null;
        };
        Assembly tests = Assembly.LoadFrom(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"Assembly-CSharp-Editor.dll"));
        var records = new List<string>(); int passed=0, failed=0;
        var timer = Stopwatch.StartNew();
        foreach(string fixture in args[2].Split(','))
        {
            Type type=tests.GetType(fixture,true);
            foreach(MethodInfo test in type.GetMethods().OrderBy(m=>m.Name))
            {
                object[] attrs=test.GetCustomAttributes(false);
                object[] cases=attrs.Where(a=>a.GetType().Name=="TestCaseAttribute").ToArray();
                if(cases.Length==0 && !attrs.Any(a=>a.GetType().Name=="TestAttribute")) continue;
                if(cases.Length==0) cases=new object[]{null};
                foreach(object c in cases)
                {
                    object[] values=c==null ? new object[0] : (object[])c.GetType().GetProperty("Arguments").GetValue(c,null);
                    string name=fixture+"."+test.Name+"("+string.Join(",",values.Select(v=>Convert.ToString(v)))+")";
                    string error=null; object instance=Activator.CreateInstance(type); var clock=Stopwatch.StartNew();
                    try
                    {
                        foreach(var setup in type.GetMethods().Where(m=>m.GetCustomAttributes(false).Any(a=>a.GetType().Name=="SetUpAttribute"))) setup.Invoke(instance,null);
                        object result=test.Invoke(instance,values);
                        if(result is System.Collections.IEnumerator) throw new InvalidOperationException("Coroutine cannot pass standalone runner.");
                    }
                    catch(Exception ex) { error=(ex is TargetInvocationException && ex.InnerException!=null ? ex.InnerException : ex).ToString(); }
                    finally
                    {
                        foreach(var teardown in type.GetMethods().Where(m=>m.GetCustomAttributes(false).Any(a=>a.GetType().Name=="TearDownAttribute")))
                            try{teardown.Invoke(instance,null);}catch(Exception ex){error=(error??"")+ex.ToString();}
                    }
                    clock.Stop(); if(error==null)passed++;else failed++;
                    records.Add("{\"name\":"+Q(name)+",\"state\":"+Q(error==null?"Passed":"Failed")+",\"seconds\":"+clock.Elapsed.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)+",\"message\":"+Q(error)+"}");
                }
            }
        }
        timer.Stop();
        string json="{\"kind\":\"standalone_bundled_mono_production_pure_tests_not_Unity_TestRunner\",\"total\":"+(passed+failed)+",\"passed\":"+passed+",\"failed\":"+failed+",\"skipped\":0,\"seconds\":"+timer.Elapsed.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)+",\"cases\":["+string.Join(",",records)+"]}";
        File.WriteAllText(args[1],json,new UTF8Encoding(false));
        Console.WriteLine("Standalone pure tests: total="+(passed+failed)+" passed="+passed+" failed="+failed+" seconds="+timer.Elapsed.TotalSeconds);
        return failed==0?0:1;
    }
}
