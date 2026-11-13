using System;
using UnityEditor;
using UnityEngine;
using ShadeLink.Beach;

namespace ShadeLink.Editor
{
    public static partial class BeachBuild
    {
        internal static void ConfigureSpawnPirateShip(BeachGame game)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/StylizedPirateShip/StylShip_Unity.prefab");
            if(prefab==null)throw new InvalidOperationException("The spawn pirate ship prefab is missing.");
            var ship=(GameObject)PrefabUtility.InstantiatePrefab(prefab,game.transform);
            ship.name="스폰 왼쪽 · 해적선";
            ship.transform.SetPositionAndRotation(new Vector3(-25,2.5f,4),Quaternion.Euler(0,32,0));
            ship.transform.localScale=Vector3.one*.4f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(ship);
            PrefabUtility.RecordPrefabInstancePropertyModifications(ship.transform);
        }
    }
}
