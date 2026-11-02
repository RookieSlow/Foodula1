from pathlib import Path
import subprocess,json,hashlib
r=Path('D:/UnityProject/Foodula1');a=r/'Library/AutomationReview/tech-driver-I04c3-20261102';out=a/'offline-compile';out.mkdir(exist_ok=True)
data=Path('C:/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Data');dotnet=data/'NetCoreRuntime/dotnet.exe';compiler=data/'DotNetSdkRoslyn/csc.dll'
results=[]
for name in ([] if '--harness-only' in __import__('sys').argv else ['Assembly-CSharp','Assembly-CSharp-Editor']):
 original=r/'Library/Bee/artifacts/1900b0aE.dag'/f'{name}.rsp';lines=original.read_text(encoding='utf-8-sig').splitlines();new=[]
 for line in lines:
  if line.startswith('-out:'):line=f'-out:"{out.as_posix()}/{name}.dll"'
  elif line.startswith('-refout:'):line=f'-refout:"{out.as_posix()}/{name}.ref.dll"'
  elif name.endswith('Editor') and line.startswith('-r:') and line.endswith('/Assembly-CSharp.ref.dll"'):line=f'-r:"{out.as_posix()}/Assembly-CSharp.ref.dll"'
  new.append(line)
 rsp=out/f'{name}.rsp';rsp.write_text('\n'.join(new)+'\n',encoding='utf-8')
 run=subprocess.run([str(dotnet),str(compiler),'@'+str(rsp)],cwd=r,capture_output=True,text=True,encoding='utf-8',errors='replace',timeout=90)
 (out/f'{name}.log').write_text(run.stdout+'\n'+run.stderr,encoding='utf-8');results.append({'assembly':name,'exit':run.returncode,'sources':sum(x.startswith('"Assets/') for x in new),'rsp_sha256':hashlib.sha256(rsp.read_bytes()).hexdigest(),'boundary':'Offline Roslyn C#9; never loaded into live Unity, not Editor TestRunner/visual evidence.'});print(name,run.returncode,(run.stdout+run.stderr)[-2000:])
 if run.returncode:break
if results:(a/'offline-compile.json').write_text(json.dumps(results,indent=2)+'\n',encoding='utf-8')
if any(x['exit'] for x in results):raise SystemExit(1)
harness=out/'PureHarness.cs'
harness.write_text('''using System; using System.IO; using System.Reflection; using System.Collections.Generic;
using NUnit.Framework.Api; using NUnit.Framework.Interfaces; using NUnit.Framework.Internal.Filters;
public sealed class QuietListener : ITestListener {
 public void TestStarted(ITest t) {} public void TestFinished(ITestResult t) {} public void TestOutput(TestOutput o) {} 
}
public static class Program {
 public static int Main(string[] args) {
  string root=Directory.GetCurrentDirectory(), here=Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
  AppDomain.CurrentDomain.AssemblyResolve += (sender,e) => {
   string name=new AssemblyName(e.Name).Name+".dll";
   foreach(string dir in new[]{here,Path.Combine(root,"Library/ScriptAssemblies"),Path.Combine(root,"Library/PackageCache/com.unity.ext.nunit@1.0.6/net35/unity-custom"),"C:/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Data/Managed","C:/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Data/Managed/UnityEngine"}) {
    string p=Path.Combine(dir,name); if(File.Exists(p))return Assembly.LoadFrom(p);
   } return null;
  };
  var runtime=Assembly.LoadFrom(Path.Combine(here,"Assembly-CSharp.dll"));
  var editor=Assembly.LoadFrom(Path.Combine(here,"Assembly-CSharp-Editor.dll"));
  var runner=new NUnitTestAssemblyRunner(new DefaultTestAssemblyBuilder());
  runner.Load(editor,new Dictionary<string,object>{{"WorkDirectory",here}});
  var result=runner.Run(new QuietListener(),new OrFilter(new FullNameFilter("DriverSkillRulesTests"),new FullNameFilter("DriverSkillRuntimeTests")));
  File.WriteAllText(Path.Combine(here,"standalone-rules-runtime-result.xml"),result.ToXml(true).OuterXml);
  Console.WriteLine("STANDALONE_RULES_RUNTIME runtime="+runtime.ManifestModule.ModuleVersionId+" editor="+editor.ManifestModule.ModuleVersionId+" total="+(result.PassCount+result.FailCount+result.SkipCount+result.InconclusiveCount)+" passed="+result.PassCount+" failed="+result.FailCount+" skipped="+result.SkipCount+" seconds="+result.Duration);
  return result.FailCount==0 && (result.PassCount+result.FailCount+result.SkipCount+result.InconclusiveCount)>0?0:1;
 }
}''',encoding='utf-8')
base=(out/'Assembly-CSharp-Editor.rsp').read_text(encoding='utf-8').splitlines();filtered=[x for x in base if not (x.startswith('"Assets/') or x.startswith(('-out:','-refout:','-target:','-analyzer:','/additionalfile:','-additionalfile:')))]
filtered += ['-target:exe',f'-out:"{out.as_posix()}/PureHarness.exe"',f'"{harness.as_posix()}"']
rsp=out/'PureHarness.rsp';rsp.write_text('\n'.join(filtered)+'\n',encoding='utf-8')
build=subprocess.run([str(dotnet),str(compiler),'@'+str(rsp)],cwd=r,capture_output=True,text=True,encoding='utf-8',errors='replace',timeout=60)
(out/'harness-build.log').write_text(build.stdout+build.stderr,encoding='utf-8');print('harness build',build.returncode,(build.stdout+build.stderr)[-2000:])
if build.returncode:raise SystemExit(build.returncode)
# Mono path resolves dependencies for NUnit before Main, and isolated assemblies always precede old editor outputs.
import os
env=dict(os.environ);env['MONO_PATH']=str(out)+';'+str(r/'Library/PackageCache/com.unity.ext.nunit@1.0.6/net35/unity-custom')+';'+str(data/'Managed')+';'+str(data/'Managed/UnityEngine')
run=subprocess.run([str(data/'MonoBleedingEdge/bin/mono.exe'),str(out/'PureHarness.exe')],cwd=r,env=env,capture_output=True,text=True,encoding='utf-8',errors='replace',timeout=60)
(out/'standalone-rules-runtime.log').write_text(run.stdout+run.stderr,encoding='utf-8');print('pure exit',run.returncode,(run.stdout+run.stderr)[-2200:])
