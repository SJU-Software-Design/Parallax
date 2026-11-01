using System;
using System.IO;
using System.Collections.Generic;
using Mono.Cecil;
class Retarget {
 static readonly Dictionary<string,AssemblyNameDefinition> owners=new Dictionary<string,AssemblyNameDefinition>();
 static void Index(TypeDefinition type,AssemblyNameDefinition owner) { owners[type.FullName]=owner; foreach(var child in type.NestedTypes)Index(child,owner); }
 static void Main(string[] args) {
  Directory.CreateDirectory(args[1]); var resolver=new DefaultAssemblyResolver(); resolver.AddSearchDirectory(args[0]); resolver.AddSearchDirectory(args[2]);
  foreach(var path in Directory.GetFiles(args[2],"*.dll")) {
   if(Path.GetFileName(path).StartsWith("Unity")||Path.GetFileName(path)=="Assembly-CSharp.dll")continue;
   using(var bcl=AssemblyDefinition.ReadAssembly(path)) foreach(var type in bcl.MainModule.Types)Index(type,bcl.Name);
  }
  foreach(var path in Directory.GetFiles(args[0],"UnityEngine*.dll")) {
   using(var assembly=AssemblyDefinition.ReadAssembly(path,new ReaderParameters { AssemblyResolver=resolver })) {
    var module=assembly.MainModule;
    foreach(var type in module.GetTypeReferences()) {
     var scope=type.Scope as AssemblyNameReference;
     if(scope==null||scope.Name!="netstandard")continue;
     AssemblyNameDefinition owner;
     if(!owners.TryGetValue(type.FullName,out owner))continue;
     AssemblyNameReference target=null;
     foreach(var candidate in module.AssemblyReferences)if(candidate.Name==owner.Name)target=candidate;
     if(target==null){target=new AssemblyNameReference(owner.Name,owner.Version){PublicKeyToken=owner.PublicKeyToken};module.AssemblyReferences.Add(target);}
     type.Scope=target;
    }
    foreach(var reference in module.AssemblyReferences)if(reference.Name=="netstandard") {
     reference.Name="mscorlib";reference.Version=new Version(4,0,0,0);reference.PublicKeyToken=new byte[]{0xb7,0x7a,0x5c,0x56,0x19,0x34,0xe0,0x89};
    }
    assembly.Write(Path.Combine(args[1],Path.GetFileName(path)));
   }
  }
 }
}
