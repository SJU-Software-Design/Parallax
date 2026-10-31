using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ShadeLink.Editor
{
    public static class PhysicsPackageValidation
    {
        public static void RunAll()
        {
            var failures=new List<string>();
            var names=new List<string>{"ControlFeelValidation.RunAll","HeldCollisionTests.Run","ReferencePhysicsTests.Run"};
            if(typeof(PhysicsPackageValidation).Assembly.GetType("ShadeLink.Editor.ObjectInteractionTests")!=null)
                names.Add("ObjectInteractionTests.RunPhysicsPolicies");
            foreach(var name in names)
            {
                try
                {
                    int split=name.LastIndexOf('.');
                    var type=typeof(PhysicsPackageValidation).Assembly.GetType("ShadeLink.Editor."+name.Substring(0,split),true);
                    type.GetMethod(name.Substring(split+1)).Invoke(null,null);
                    Debug.Log("PHYSICS_PACKAGE_SUITE_PASS "+name);
                }
                catch(Exception error)
                {
                    var actual=error is TargetInvocationException?error.InnerException:error;
                    failures.Add(name+": "+actual.Message);Debug.LogError("PHYSICS_PACKAGE_SUITE_FAIL "+failures[failures.Count-1]);
                }
            }
            if(failures.Count>0)throw new Exception("PHYSICS_PACKAGE_VALIDATION_FAILED\n"+string.Join("\n",failures));
            Debug.Log("PHYSICS_PACKAGE_VALIDATION_OK");
        }
    }
}
